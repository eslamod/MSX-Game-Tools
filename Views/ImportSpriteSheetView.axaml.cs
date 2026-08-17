using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Views;

/// <summary>
/// El formulario de traer una hoja de sprites: la hoja con su retícula y el rectángulo elegido.
/// </summary>
/// <remarks>
/// La retícula se dibuja aquí y no se enlaza celda a celda: una hoja de trescientas celdas
/// serían trescientos controles enlazados que se rehacen al cambiar el lado de celda, y lo único
/// que hace falta son unas líneas y un recuadro.
/// </remarks>
public partial class ImportSpriteSheetView : UserControl
{
    /// <summary>A cuánto se ve un pixel de la hoja. Fijo: a 1 no se acierta la celda.</summary>
    private const int Zoom = 2;

    /// <summary>Dónde empezó el arrastre, en celdas.</summary>
    private (int Column, int Row)? _from;

    /// <summary>Al que se le está escuchando, para poder soltarlo si cambia el contexto.</summary>
    private ImportSpriteSheetViewModel? _watching;

    public ImportSpriteSheetView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Build();
    }

    private ImportSpriteSheetViewModel? Model => DataContext as ImportSpriteSheetViewModel;

    /// <summary>Pone la hoja a su tamaño y dibuja la retícula encima.</summary>
    private void Build()
    {
        if (Model is not { } model)
            return;

        SheetImage.Width = model.Size.Width * Zoom;
        SheetImage.Height = model.Size.Height * Zoom;

        SheetPanel.Width = SheetImage.Width;
        SheetPanel.Height = SheetImage.Height;

        // Soltando el anterior: el panel se hace nuevo cada vez, pero si algún día se
        // reaprovechara, cada cambio de contexto dejaría otro oyente pegado al de antes.
        if (_watching is not null)
            _watching.PropertyChanged -= OnModelChanged;

        _watching = model;
        model.PropertyChanged += OnModelChanged;

        Grid();
        Mark();
    }

    /// <summary>
    /// El lado de celda rehace las líneas; el rectángulo sólo mueve el recuadro.
    /// </summary>
    /// <remarks>
    /// Separado porque la retícula de una hoja grande son cientos de líneas y el rectángulo se
    /// mueve con cada pixel del arrastre: rehacerla entera en cada movimiento se notaría.
    /// </remarks>
    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImportSpriteSheetViewModel.CellSize))
            Grid();

        if (e.PropertyName is nameof(ImportSpriteSheetViewModel.Selection)
            or nameof(ImportSpriteSheetViewModel.CellSize))
        {
            Mark();
        }
    }

    /// <summary>Las líneas de la retícula del lado de celda que esté elegido.</summary>
    private void Grid()
    {
        if (Model is not { } model)
            return;

        CellOverlay.Children.Clear();

        double side = model.CellSize * Zoom;

        for (int column = 1; column < model.Columns; column++)
            CellOverlay.Children.Add(Line(column * side, 0, 1, SheetPanel.Height));

        for (int row = 1; row < model.Rows; row++)
            CellOverlay.Children.Add(Line(0, row * side, SheetPanel.Width, 1));

        // El recuadro del rectángulo elegido va el último para que quede por encima.
        CellOverlay.Children.Add(SelectionMark);
    }

    private static Border Line(double left, double top, double width, double height)
    {
        var line = new Border
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            IsHitTestVisible = false,
        };

        Canvas.SetLeft(line, left);
        Canvas.SetTop(line, top);

        return line;
    }

    /// <summary>El recuadro rojo de lo que se va a traer, el mismo que marca la selección.</summary>
    private Border SelectionMark { get; } = new()
    {
        BorderThickness = new Thickness(2),
        BorderBrush = Brushes.Red,
        IsHitTestVisible = false,
    };

    private void Mark()
    {
        if (Model is not { } model)
            return;

        double side = model.CellSize * Zoom;

        Canvas.SetLeft(SelectionMark, model.Selection.Left * side);
        Canvas.SetTop(SelectionMark, model.Selection.Top * side);

        SelectionMark.Width = model.Selection.Columns * side;
        SelectionMark.Height = model.Selection.Rows * side;
    }

    /// <summary>En qué celda de la hoja cae un punto de la capa.</summary>
    private (int Column, int Row) CellAt(Point point)
    {
        if (Model is not { } model)
            return (0, 0);

        double side = Math.Max(1, model.CellSize * Zoom);

        return (
            Math.Clamp((int)(point.X / side), 0, Math.Max(0, model.Columns - 1)),
            Math.Clamp((int)(point.Y / side), 0, Math.Max(0, model.Rows - 1)));
    }

    private void OnSheetPressed(object? sender, PointerPressedEventArgs e)
    {
        _from = CellAt(e.GetPosition(CellOverlay));

        Drag(_from.Value);

        e.Pointer.Capture(CellOverlay);
    }

    private void OnSheetMoved(object? sender, PointerEventArgs e)
    {
        if (_from is not null)
            Drag(CellAt(e.GetPosition(CellOverlay)));
    }

    private void OnSheetReleased(object? sender, PointerReleasedEventArgs e)
    {
        _from = null;

        e.Pointer.Capture(null);
    }

    /// <summary>Deja el rectángulo entre donde empezó el arrastre y donde está el ratón.</summary>
    private void Drag((int Column, int Row) to)
    {
        if (Model is not { } model || _from is not { } from)
            return;

        int left = Math.Min(from.Column, to.Column);
        int top = Math.Min(from.Row, to.Row);

        model.Select(
            left,
            top,
            Math.Abs(to.Column - from.Column) + 1,
            Math.Abs(to.Row - from.Row) + 1);
    }
}
