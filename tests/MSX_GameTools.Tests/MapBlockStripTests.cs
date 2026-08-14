using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// La tira de bloques del editor de mapas, con bloques de alturas distintas.
/// </summary>
/// <remarks>
/// Se mide la posición de las celdas ya montadas en vez de deducirla del XAML: lo que
/// fallaba era el reparto del alto sobrante, y eso sólo existe después de medir y colocar.
/// </remarks>
public class MapBlockStripTests : IDisposable
{
    private readonly Window _window;
    private readonly MapEditorView _view;

    public MapBlockStripTests()
    {
        var tileSet = new TileSet("Bosque");

        // Uno de dos filas y otro de tres, en ese orden: el alto de la fila lo marca el
        // mas alto, asi que el de dos es el que se estiraba.
        tileSet.Blocks.Add(new TileBlock("Dos") { [0, 0] = 1, [1, 0] = 2, [0, 1] = 3, [1, 1] = 4 });
        tileSet.Blocks.Add(new TileBlock("Tres") { [0, 0] = 5, [0, 1] = 6, [0, 2] = 7 });

        Editor = new MapEditorViewModel(
            new TileMap("Mapa", 40, 30),
            new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard()));

        _view = new MapEditorView { DataContext = Editor };
        _window = new Window { Content = _view, Width = 1100, Height = 800 };

        _window.Show();
        Pump();

        // La pestaña de bloques, que es la que trae la tira.
        _view.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex = 1;
        Pump();
    }

    private MapEditorViewModel Editor { get; }

    public void Dispose() => _window.Close();

    /// <summary>
    /// Las filas de un bloque van pegadas, aunque al lado haya uno más alto.
    /// </summary>
    /// <remarks>
    /// Un bloque de dos filas junto a uno de tres salía partido: se veía su fila de arriba,
    /// un hueco, y su fila de abajo. La imagen de cada celda mide lo que mide, así que el
    /// hueco no está dentro de las celdas sino entre ellas.
    /// </remarks>
    [AvaloniaFact]
    public void Un_bloque_bajo_no_se_parte_al_lado_de_uno_alto()
    {
        Border shorter = BlockOf("Dos");
        List<Point> cells = CellsOf(shorter);

        Assert.Equal(4, cells.Count);

        // La segunda fila empieza justo donde acaba la primera.
        Assert.Equal(cells[0].Y + _view.TileSize, cells[2].Y, 1);
        Assert.Equal(cells[1].Y + _view.TileSize, cells[3].Y, 1);
    }

    /// <summary>Y el bloque ocupa lo suyo, no lo del vecino.</summary>
    [AvaloniaFact]
    public void Un_bloque_bajo_no_crece_hasta_el_alto_del_alto()
    {
        Border shorter = BlockOf("Dos");
        Border taller = BlockOf("Tres");

        // El borde de la casilla, dos pixeles por lado.
        Assert.Equal((2 * _view.TileSize) + 4, shorter.Bounds.Height, 1);
        Assert.Equal((3 * _view.TileSize) + 4, taller.Bounds.Height, 1);
    }

    /// <summary>
    /// La casilla de un bloque de la tira, por el nombre del bloque.
    /// </summary>
    /// <remarks>
    /// Por la clase y no sólo por el DataContext: dentro de la casilla hay más bordes y
    /// todos heredan el mismo, así que sin esto salen varios y no se mide ninguno.
    /// </remarks>
    private Border BlockOf(string name) =>
        _view.GetVisualDescendants()
            .OfType<Border>()
            .Single(border => border.Classes.Contains("choice")
                && border.DataContext is BlockChoiceViewModel choice
                && choice.Block.Name == name);

    /// <summary>Dónde cae cada celda dentro de su casilla, por filas.</summary>
    private static List<Point> CellsOf(Border block) =>
        [.. block.GetVisualDescendants()
            .OfType<Image>()
            .Select(cell => cell.TranslatePoint(new Point(0, 0), block)!.Value)];

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
