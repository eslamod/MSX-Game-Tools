using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;
using MSX_SpritesEditor.Views;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// Monta un <see cref="SpritesEditorView"/> real dentro de una ventana headless y
/// permite dirigir el ratón sobre el lienzo en coordenadas de celda del sprite.
/// </summary>
internal sealed class SpriteCanvasHarness : IDisposable
{
    /// <summary>Lado de una celda con el zoom X1 (lienzo de 256 / 16 celdas).</summary>
    private const double CellSize = 16.0;

    private readonly Window _window;
    private readonly Point _origin;

    public SpriteCanvasHarness(PaintMode mode)
    {
        Bank = new SpriteBank();
        ViewModel = new SpritesEditorViewModel(Bank, GlobalSettings.CurrentColorPalette);

        var view = new SpritesEditorView { DataContext = ViewModel };
        _window = new Window { Content = view, Width = 900, Height = 700 };
        _window.Show();
        Pump();

        RadioButton modeButton = view.GetVisualDescendants()
            .OfType<RadioButton>()
            .Single(r => (r.Tag as string) == mode.ToString());
        modeButton.IsChecked = true;
        Pump();

        Canvas canvas = view.GetVisualDescendants().OfType<Canvas>().Single();
        _origin = canvas.TranslatePoint(new Point(0, 0), _window)
                  ?? throw new InvalidOperationException("El lienzo no está en el árbol visual.");

        Thumbnails = view.GetVisualDescendants().OfType<ListBox>().Single();
    }

    /// <summary>Debe coincidir con los Tag de los RadioButton de modo en SpritesEditorView.axaml.</summary>
    public enum PaintMode
    {
        Click,
        Drag,
    }

    public SpriteBank Bank { get; }

    public SpritesEditorViewModel ViewModel { get; }

    /// <summary>La tira de miniaturas del banco.</summary>
    public ListBox Thumbnails { get; }

    public int PaintedCount => Bank.SpritesList[0].ArraySpriteRows
        .Sum(row => row.ArrayColumns.Count(on => on));

    public void Press(int cellX, int cellY, MouseButton button = MouseButton.Left)
    {
        _window.MouseDown(At(cellX, cellY), button);
        Pump();
    }

    public void MoveTo(int cellX, int cellY)
    {
        _window.MouseMove(At(cellX, cellY));
        Pump();
    }

    /// <summary>Lleva el puntero fuera del lienzo, por la izquierda.</summary>
    public void MoveOutside(int cellY)
    {
        _window.MouseMove(new Point(_origin.X - 80, _origin.Y + (cellY * CellSize) + (CellSize / 2)));
        Pump();
    }

    public void Release(int cellX, int cellY, MouseButton button = MouseButton.Left)
    {
        _window.MouseUp(At(cellX, cellY), button);
        Pump();
    }

    /// <summary>Índices de las columnas encendidas en una fila de un sprite del banco.</summary>
    public int[] PaintedCellsInRow(int row, int sprite = 0) =>
        [.. Enumerable.Range(0, SpriteRow.Columns)
            .Where(i => Bank.SpritesList[sprite].ArraySpriteRows[row].ArrayColumns[i])];

    public ListBoxItem ThumbnailContainer(int index)
    {
        Pump();
        return Thumbnails.ContainerFromIndex(index) as ListBoxItem
               ?? throw new InvalidOperationException($"La miniatura {index} no está realizada.");
    }

    public void ClickThumbnail(int index)
    {
        Point centre = ThumbnailCentre(index);
        _window.MouseDown(centre, MouseButton.Left);
        Pump();
        _window.MouseUp(centre, MouseButton.Left);
        Pump();
    }

    public void HoverThumbnail(int index)
    {
        _window.MouseMove(ThumbnailCentre(index));
        Pump();
    }

    public void Dispose()
    {
        _window.Close();
        Pump();
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    /// <summary>Centro de una celda, en coordenadas de la ventana.</summary>
    private Point At(int cellX, int cellY) => new(
        _origin.X + (cellX * CellSize) + (CellSize / 2),
        _origin.Y + (cellY * CellSize) + (CellSize / 2));

    /// <summary>Centro de una miniatura, en coordenadas de la ventana.</summary>
    private Point ThumbnailCentre(int index)
    {
        ListBoxItem container = ThumbnailContainer(index);
        return container.TranslatePoint(
                   new Point(container.Bounds.Width / 2, container.Bounds.Height / 2), _window)
               ?? throw new InvalidOperationException($"La miniatura {index} no está en el árbol visual.");
    }
}
