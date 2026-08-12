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
/// El panel del mapa con el ratón de verdad.
/// </summary>
/// <remarks>
/// Los comandos se prueban aparte; esto comprueba el cableado, que es lo que no se ve
/// leyendo: que el lienzo llegue al ViewModel, que el selector de abajo cambie lo que se
/// estampa, y que la rueda con control y con mayúsculas haga cosas distintas.
/// </remarks>
public class MapEditorViewTests : IDisposable
{
    private readonly Window _window;
    private readonly MapEditorView _view;

    public MapEditorViewTests()
    {
        var tileSet = new TileSet("Bosque");
        tileSet.Blocks.Add(new TileBlock("Arbol") { [0, 0] = 5, [1, 0] = 6 });
        tileSet.Blocks.Add(new TileBlock("Roca") { [0, 0] = 9 });

        Editor = new MapEditorViewModel(
            new TileMap("Mapa", 40, 30),
            new TileSetEditorViewModel(tileSet, new PaletteLibrary()));

        _view = new MapEditorView { DataContext = Editor };
        _window = new Window { Content = _view, Width = 1100, Height = 800 };

        _window.Show();
        Pump();
    }

    private MapEditorViewModel Editor { get; }

    public void Dispose() => _window.Close();

    [AvaloniaFact]
    public void Pulsar_en_el_mapa_estampa_lo_cogido()
    {
        Editor.PickTile(TilePatch.Single(4), "Tile 4");

        MapCanvas canvas = Canvas();
        Click(canvas, new Point(40, 24));

        Assert.Equal(4, Editor.ActiveLayer!.Layer.Grid[2, 1]);
    }

    /// <summary>En modo selección arrastrar marca, no pinta.</summary>
    [AvaloniaFact]
    public void Arrastrar_en_modo_seleccion_marca_el_rectangulo()
    {
        Editor.Tool = MapTool.Select;
        Pump();

        MapCanvas canvas = Canvas();
        Point origin = canvas.TranslatePoint(new Point(0, 0), _window)!.Value;

        _window.MouseDown(origin + new Point(8, 8), MouseButton.Left);
        Pump();
        _window.MouseMove(origin + new Point(56, 40), RawInputModifiers.LeftMouseButton);
        Pump();
        _window.MouseUp(origin + new Point(56, 40), MouseButton.Left);
        Pump();

        Assert.Equal(new MapRegion(0, 0, 4, 3), Editor.Selection);
        Assert.True(Editor.ActiveLayer!.Layer.Grid.IsEmpty);
    }

    [AvaloniaFact]
    public void El_raton_sobre_el_mapa_dice_en_que_celda_esta()
    {
        MapCanvas canvas = Canvas();
        Point origin = canvas.TranslatePoint(new Point(0, 0), _window)!.Value;

        _window.MouseMove(origin + new Point(40, 24));
        Pump();

        Assert.Equal("2, 1", Editor.HoverLabel);
    }

    // ------------------------------------------------------------------ el selector

    [AvaloniaFact]
    public void Pulsar_en_un_tile_de_abajo_lo_deja_cogido()
    {
        ItemsControl choices = _view.FindControl<ItemsControl>("TileChoices")!;

        Click(Container(choices, Editor.TileChoices[7]));

        Assert.Equal(7, Editor.Brush[0, 0]);
        Assert.Equal("Tile 7", Editor.BrushName);

        // Y se marca, que sin señal hay que acordarse de lo que se cogio.
        Assert.True(Editor.TileChoices[7].IsSelected);
    }

    [AvaloniaFact]
    public void Pulsar_en_un_bloque_lo_deja_cogido_entero()
    {
        // Los bloques no existen hasta que su pestaña esta delante, igual que para el
        // usuario: el TabControl no monta lo que no se ve.
        ShowBlocksTab();

        BlockChoiceViewModel choice = Editor.BlockChoices[0];

        Click(Container(_view, choice));

        Assert.Equal((2, 1), (Editor.Brush.Width, Editor.Brush.Height));
        Assert.Equal(5, Editor.Brush[0, 0]);
        Assert.Contains("Arbol", Editor.BrushName);
        Assert.True(choice.IsSelected);
    }

    // ------------------------------------------------------------------ la rueda

    [AvaloniaFact]
    public void Control_y_rueda_cambian_el_zoom()
    {
        int before = Editor.Zoom;

        Wheel(1, KeyModifiers.Control);

        Assert.Equal(before + 1, Editor.Zoom);

        Wheel(-1, KeyModifiers.Control);
        Wheel(-1, KeyModifiers.Control);

        Assert.Equal(before - 1, Editor.Zoom);
    }

    /// <summary>Con mayúsculas la rueda pasa de bloque, que es lo cómodo al estampar.</summary>
    [AvaloniaFact]
    public void Mayusculas_y_rueda_pasan_de_bloque()
    {
        Editor.PickBlock(Editor.Blocks[0]);

        Wheel(-1, KeyModifiers.Shift);

        Assert.Contains("Roca", Editor.BrushName);
    }

    [AvaloniaFact]
    public void La_rueda_sola_no_toca_ni_el_zoom_ni_el_bloque()
    {
        int zoom = Editor.Zoom;
        Editor.PickBlock(Editor.Blocks[0]);

        Wheel(1, KeyModifiers.None);

        Assert.Equal(zoom, Editor.Zoom);
        Assert.Contains("Arbol", Editor.BrushName);
    }

    // ------------------------------------------------------------------ zoom del selector

    /// <summary>Los tiles y los bloques de abajo crecen con el mismo zoom.</summary>
    [AvaloniaFact]
    public void Los_botones_de_abajo_cambian_el_tamano_de_los_tiles()
    {
        Assert.Equal(16, _view.TileSize);

        Check("MapTileZoom", 4);

        Assert.Equal(32, _view.TileSize);
        Assert.Equal(4, Editor.Preferences.MapTileZoom);

        Check("MapTileZoom", 1);

        Assert.Equal(8, _view.TileSize);
    }

    /// <summary>
    /// El TabControl reconstruye la vista al cambiar de pestaña, así que el zoom tiene que
    /// vivir en los ajustes y no en el control.
    /// </summary>
    [AvaloniaFact]
    public void El_zoom_del_selector_sobrevive_a_montar_la_vista_de_nuevo()
    {
        Check("MapTileZoom", 3);

        var other = new MapEditorView { DataContext = Editor };
        var window = new Window { Content = other, Width = 1100, Height = 800 };

        window.Show();
        Pump();

        Assert.Equal(24, other.TileSize);

        window.Close();
        Pump();
    }

    private void Check(string group, int factor)
    {
        RadioButton button = _view.GetVisualDescendants()
            .OfType<RadioButton>()
            .First(r => r.GroupName == group && (string?)r.Tag == factor.ToString());

        button.IsChecked = true;
        Pump();
    }

    private MapCanvas Canvas() => _view.GetVisualDescendants().OfType<MapCanvas>().Single();

    private void ShowBlocksTab()
    {
        TabControl tabs = _view.GetVisualDescendants().OfType<TabControl>().Single();

        tabs.SelectedIndex = 1;
        Pump();
    }

    private void Click(Visual target, Point? offset = null)
    {
        Point origin = target.TranslatePoint(new Point(0, 0), _window)!.Value;

        Point point = offset is { } inner
            ? origin + inner
            : origin + new Point(target.Bounds.Width / 2, target.Bounds.Height / 2);

        _window.MouseDown(point, MouseButton.Left);
        Pump();
        _window.MouseUp(point, MouseButton.Left);
        Pump();
    }

    private void Wheel(int delta, KeyModifiers modifiers)
    {
        Point centre = Canvas().TranslatePoint(new Point(20, 20), _window)!.Value;

        _window.MouseWheel(centre, new Vector(0, delta), (RawInputModifiers)modifiers);
        Pump();
    }

    /// <summary>El control que dibuja ese elemento, buscado por su DataContext.</summary>
    private static Visual Container(Visual root, object item) =>
        root.GetVisualDescendants()
            .OfType<Control>()
            .First(control => ReferenceEquals(control.DataContext, item) && control.Bounds.Width > 0);

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
