using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El panel de bloques con el ratón de verdad.
/// </summary>
/// <remarks>
/// Los comandos del ViewModel se prueban aparte; esto comprueba lo que sólo se ve con la
/// ventana montada: que las celdas y los tiles reciban el puntero y que arrastrar llegue
/// a los vecinos. Un control sin fondo no recibe eventos, y capturar el puntero se lleva
/// los de al lado, así que son fallos que no se ven de otra forma.
/// </remarks>
public class TileBlocksViewTests : IDisposable
{
    private readonly Window _window;
    private readonly TileBlocksView _view;

    public TileBlocksViewTests()
    {
        var editor = new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

        Panel = new TileBlocksViewModel(editor);
        _view = new TileBlocksView { DataContext = Panel };
        _window = new Window { Content = _view, Width = 500, Height = 900 };

        _window.Show();
        Pump();
    }

    private TileBlocksViewModel Panel { get; }

    public void Dispose() => _window.Close();

    [AvaloniaFact]
    public void Pulsar_en_una_celda_estampa_el_tile_cogido()
    {
        Panel.AddBlockCommand.Execute(null);
        Panel.SelectTile(3);
        Pump();

        Click(CellAt(0, 0));

        Assert.Equal(3, Panel.SelectedBlock!.Block[0, 0]);
    }

    /// <summary>
    /// Arrastrar tiene que pintar también las celdas por las que pasa. Es lo que se
    /// rompería capturando el puntero en la primera.
    /// </summary>
    [AvaloniaFact]
    public void Arrastrar_por_la_rejilla_pinta_las_celdas_por_las_que_pasa()
    {
        Panel.AddBlockCommand.Execute(null);
        Panel.SelectTile(7);
        Pump();

        _window.MouseDown(Centre(CellAt(0, 0)), MouseButton.Left);
        Pump();
        _window.MouseMove(Centre(CellAt(1, 0)));
        Pump();
        _window.MouseMove(Centre(CellAt(2, 0)));
        Pump();
        _window.MouseUp(Centre(CellAt(2, 0)), MouseButton.Left);
        Pump();

        Assert.Equal(7, Panel.SelectedBlock!.Block[0, 0]);
        Assert.Equal(7, Panel.SelectedBlock.Block[1, 0]);
        Assert.Equal(7, Panel.SelectedBlock.Block[2, 0]);
    }

    /// <summary>Soltado el botón, pasar por encima ya no pinta.</summary>
    [AvaloniaFact]
    public void Pasar_por_encima_sin_pulsar_no_pinta()
    {
        Panel.AddBlockCommand.Execute(null);
        Panel.SelectTile(7);
        Pump();

        Click(CellAt(0, 0));

        _window.MouseMove(Centre(CellAt(4, 0)));
        Pump();

        Assert.Null(Panel.SelectedBlock!.Block[4, 0]);
    }

    /// <summary>El botón derecho vacía la celda, como en Tiled.</summary>
    [AvaloniaFact]
    public void El_boton_derecho_borra_la_celda()
    {
        Panel.AddBlockCommand.Execute(null);
        Panel.SelectTile(7);
        Pump();

        Click(CellAt(1, 1));

        Assert.Equal(7, Panel.SelectedBlock!.Block[1, 1]);

        Click(CellAt(1, 1), MouseButton.Right);

        Assert.Null(Panel.SelectedBlock.Block[1, 1]);
    }

    [AvaloniaFact]
    public void Pulsar_en_un_tile_lo_coge()
    {
        Click(TileAt(5));

        Assert.Equal(5, Panel.Selection[0, 0]);
        Assert.True(Panel.Tiles[5].IsSelected);
    }

    /// <summary>
    /// Arrastrar coge el rectángulo de la disposición de 32 columnas: del tile 1 al 34
    /// hay un cuadrado de 2x2.
    /// </summary>
    [AvaloniaFact]
    public void Arrastrar_por_los_tiles_coge_el_rectangulo()
    {
        _window.MouseDown(Centre(TileAt(1)), MouseButton.Left);
        Pump();
        _window.MouseMove(Centre(TileAt(34)));
        Pump();
        _window.MouseUp(Centre(TileAt(34)), MouseButton.Left);
        Pump();

        Assert.Equal((2, 2), (Panel.Selection.Width, Panel.Selection.Height));
        Assert.Equal(34, Panel.Selection[1, 1]);
        Assert.True(Panel.Tiles[33].IsSelected);
    }

    private void Click(Visual target, MouseButton button = MouseButton.Left)
    {
        Point centre = Centre(target);

        _window.MouseDown(centre, button);
        Pump();
        _window.MouseUp(centre, button);
        Pump();
    }

    private Point Centre(Visual target)
    {
        Point origin = target.TranslatePoint(new Point(0, 0), _window)
                       ?? throw new InvalidOperationException("El control no está en el árbol visual.");

        return new Point(origin.X + (target.Bounds.Width / 2), origin.Y + (target.Bounds.Height / 2));
    }

    /// <summary>El control de una celda de la rejilla de composición.</summary>
    private Visual CellAt(int column, int row)
    {
        BlockCellViewModel cell = Panel.Cells.Single(c => c.Column == column && c.Row == row);

        return Container(_view.FindControl<ItemsControl>("BlockGrid")!, cell);
    }

    private Visual TileAt(int index) =>
        Container(_view.FindControl<ItemsControl>("TileChoices")!, Panel.Tiles[index]);

    /// <summary>
    /// El control que dibuja ese elemento. Se busca por su DataContext y no por posición
    /// porque quien recibe el puntero es la plantilla, no el contenedor.
    /// </summary>
    private static Visual Container(ItemsControl items, object item) =>
        items.GetVisualDescendants()
            .OfType<Border>()
            .First(border => ReferenceEquals(border.DataContext, item));

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
