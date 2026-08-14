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
/// Copiar y estampar tiles con el ratón de verdad.
/// </summary>
/// <remarks>
/// La operación se prueba aparte; esto comprueba lo que sólo se ve con la ventana montada:
/// que el punto del ratón caiga en la celda que toca. La rejilla lleva el borde del
/// ListBox, el de cada tile y un zoom que la cambia de tamaño, así que la cuenta de dónde
/// empieza una celda es justo lo que no se puede deducir leyendo.
/// </remarks>
public class TileCopyViewTests : IDisposable
{
    private readonly Window _window;
    private readonly TileSetEditorView _view;

    public TileCopyViewTests()
    {
        Editor = new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

        _view = new TileSetEditorView { DataContext = Editor };
        _window = new Window { Content = _view, Width = 1400, Height = 900 };

        _window.Show();
        Pump();
    }

    private TileSetEditorViewModel Editor { get; }

    public void Dispose() => _window.Close();

    [AvaloniaFact]
    public void Arrastrar_en_modo_seleccionar_marca_el_rectangulo()
    {
        Choose("Select");

        Drag(From(2, 1), To(4, 3));

        Assert.Equal(new MapRegion(2, 1, 3, 3), Editor.Selection);
    }

    /// <summary>Y de derecha a izquierda marca lo mismo, no un rectángulo del revés.</summary>
    [AvaloniaFact]
    public void Arrastrar_hacia_atras_marca_el_mismo_rectangulo()
    {
        Choose("Select");

        Drag(From(4, 3), To(2, 1));

        Assert.Equal(new MapRegion(2, 1, 3, 3), Editor.Selection);
    }

    [AvaloniaFact]
    public void Pulsar_en_modo_estampar_suelta_lo_marcado()
    {
        // Un tile reconocible en la celda (2,1), que es la 34.
        TileRow row = Editor.TileSet.ListOfTiles[34].ArrayTileRows[0];
        row.ForeColor = 7;

        for (int column = 0; column < TileRow.Columns; column++)
            row.ArrayPattern[column] = true;

        Choose("Select");
        Drag(From(2, 1), To(2, 1));

        Choose("Stamp");
        Click(From(10, 5));

        Tile stamped = Editor.TileSet.ListOfTiles[(5 * TileSet.Columns) + 10];

        Assert.Equal(0xFF, stamped.ArrayTileRows[0].PatternByte);
        Assert.Equal(7, stamped.ArrayTileRows[0].ForeColor);
    }

    /// <summary>
    /// En el modo de siempre la rejilla sigue eligiendo el tile que se edita.
    /// </summary>
    /// <remarks>
    /// La capa que recibe el ratón se pone encima del ListBox, así que dejarla activa en
    /// el modo de editar se comería las pulsaciones y no se podría cambiar de tile.
    /// </remarks>
    [AvaloniaFact]
    public void En_modo_editar_la_rejilla_sigue_eligiendo_el_tile()
    {
        Choose("Select");
        Choose("Edit");

        Click(From(3, 0));

        Assert.Equal(4, Editor.CurrentTilePosition);
        Assert.Null(Editor.Selection);
    }

    /// <summary>Elige un modo por su botón, como haría quien lo usa.</summary>
    private void Choose(string tool)
    {
        RadioButton button = _view.GetVisualDescendants()
            .OfType<RadioButton>()
            .Single(radio => radio.GroupName == "TileTool" && (string?)radio.Tag == tool);

        button.IsChecked = true;
        Pump();
    }

    /// <summary>El centro de una celda de la rejilla, en coordenadas de la ventana.</summary>
    private Point From(int column, int row)
    {
        var grid = (ListBox)_view.GetVisualDescendants().OfType<ListBox>().First(list => list.Name == "TileGrid");
        Control cell = (Control)grid.ContainerFromIndex((row * TileSet.Columns) + column)!;

        return cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), _window)!.Value;
    }

    private Point To(int column, int row) => From(column, row);

    private void Click(Point point)
    {
        _window.MouseDown(point, MouseButton.Left);
        Pump();
        _window.MouseUp(point, MouseButton.Left);
        Pump();
    }

    private void Drag(Point from, Point to)
    {
        _window.MouseDown(from, MouseButton.Left);
        Pump();
        _window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        Pump();
        _window.MouseUp(to, MouseButton.Left);
        Pump();
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
