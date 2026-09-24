using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
/// The screens of the game over the map.
/// </summary>
/// <remarks>
/// A game of fixed screens is drawn as a whole map and then loaded screen by screen. The map
/// knows nothing about that: what a screen measures is in the preferences, and with it the
/// editor shows where each one starts and says which one the mouse is in.
/// </remarks>
public class MapScreensTests
{
    [AvaloniaFact]
    public void La_rejilla_parte_el_mapa_por_donde_dice_la_configuracion()
    {
        MapEditorViewModel map = NewMap(64, 48);

        map.Preferences.ScreenWidth = 32;
        map.Preferences.ScreenHeight = 22;
        map.ShowScreenGrid = true;

        Assert.Equal(32, map.ScreenGridColumns);
        Assert.Equal(22, map.ScreenGridRows);
    }

    /// <summary>
    /// Without the grid no lines are drawn and no screen is said.
    /// </summary>
    /// <remarks>
    /// The number on its own means nothing: without the lines it cannot be seen where each
    /// screen starts, so a «3-1» next to the coordinates would be a number with nothing to go
    /// by.
    /// </remarks>
    [AvaloniaFact]
    public void Sin_rejilla_no_hay_ni_lineas_ni_numero_de_pantalla()
    {
        MapEditorViewModel map = NewMap(64, 48);

        Assert.False(map.ShowScreenGrid);

        Assert.Equal(0, map.ScreenGridColumns);
        Assert.Equal(0, map.ScreenGridRows);
        Assert.Equal(string.Empty, map.ScreenAt(40, 30));
    }

    /// <summary>Column first and counting from one, the way they are named when asked for.</summary>
    [AvaloniaTheory]
    [InlineData(0, 0, "1-1")]
    [InlineData(31, 23, "1-1")]
    [InlineData(32, 0, "2-1")]
    [InlineData(0, 24, "1-2")]
    [InlineData(95, 47, "3-2")]
    public void La_pantalla_se_dice_por_columna_y_fila_desde_uno(int column, int row, string screen)
    {
        MapEditorViewModel map = NewMap(96, 48);

        map.ShowScreenGrid = true;

        Assert.Equal(screen, map.ScreenAt(column, row));
    }

    /// <summary>
    /// In a map of super tiles the grid goes in cells, which is what the map counts.
    /// </summary>
    /// <remarks>
    /// The screen is measured in tiles, which is what the player sees, but a cell of a map of
    /// super tiles is several tiles. Taking it in tiles would draw a line every two cells.
    /// </remarks>
    [AvaloniaFact]
    public void Con_supertiles_la_rejilla_va_en_celdas_y_no_en_tiles()
    {
        MapEditorViewModel map = NewMap(32, 24, superTile: 2);

        map.ShowScreenGrid = true;

        Assert.Equal(16, map.ScreenGridColumns);
        Assert.Equal(12, map.ScreenGridRows);
        Assert.Equal("2-2", map.ScreenAt(16, 12));
    }

    /// <summary>
    /// A screen that cuts a super tile in half is not shown.
    /// </summary>
    /// <remarks>
    /// With super tiles of 2x2 and a screen of 21 rows —three of scoreboard— the line would fall
    /// half-way through a cell, and there is nowhere to put it: a grid that lies by half a super
    /// tile is worse than none. One of 22 does fit, and that one is drawn.
    /// </remarks>
    [AvaloniaFact]
    public void Una_pantalla_que_parte_un_supertile_no_se_ensena()
    {
        MapEditorViewModel map = NewMap(32, 24, superTile: 2);

        map.Preferences.ScreenHeight = 21;
        map.ShowScreenGrid = true;

        Assert.Equal(16, map.ScreenGridColumns);
        Assert.Equal(0, map.ScreenGridRows);

        // And without both, the screen is not said either: the row would have nothing to come
        // from.
        Assert.Equal(string.Empty, map.ScreenAt(0, 0));
    }

    /// <summary>An impossible size is cut down instead of leaving the editor dividing by zero.</summary>
    [AvaloniaTheory]
    [InlineData(0, 1)]
    [InlineData(-8, 1)]
    [InlineData(9000, 256)]
    public void Un_tamano_de_pantalla_imposible_se_recorta(int asked, int kept)
    {
        var preferences = new EditorPreferences { ScreenWidth = asked, ScreenHeight = asked };

        Assert.Equal(kept, preferences.ScreenWidth);
        Assert.Equal(kept, preferences.ScreenHeight);
    }

    // ------------------------------------------------------------------ with the window mounted

    /// <summary>The button of the bar has to reach the canvas, which is the one that draws.</summary>
    [AvaloniaFact]
    public void El_boton_de_la_barra_pone_la_rejilla_en_el_lienzo()
    {
        using var editor = new MapWindow(NewMap(64, 48));

        MapCanvas canvas = editor.Canvas;

        Assert.Equal(0, canvas.ScreenColumns);

        editor.ToggleScreenGrid();

        Assert.Equal(32, canvas.ScreenColumns);
        Assert.Equal(24, canvas.ScreenRows);

        editor.ToggleScreenGrid();

        Assert.Equal(0, canvas.ScreenColumns);
    }

    /// <summary>
    /// And the label at the bottom says which screen the mouse is in.
    /// </summary>
    /// <remarks>
    /// Over an empty cell on purpose: that is just where one wonders which screen is the one left
    /// without drawing.
    /// </remarks>
    [AvaloniaFact]
    public void La_etiqueta_dice_en_que_pantalla_esta_el_raton()
    {
        MapEditorViewModel map = NewMap(64, 48);

        map.Preferences.ScreenWidth = 16;
        map.Preferences.ScreenHeight = 12;

        using var editor = new MapWindow(map);

        editor.ToggleScreenGrid();
        editor.Hover(17, 13);

        Assert.Equal("17, 13 \u00b7 2-2", editor.Label);
    }

    /// <summary>Without the grid, the label stays as it was.</summary>
    [AvaloniaFact]
    public void Sin_rejilla_la_etiqueta_no_dice_ninguna_pantalla()
    {
        using var editor = new MapWindow(NewMap(64, 48));

        editor.Hover(17, 13);

        Assert.Equal("17, 13", editor.Label);
    }

    // ------------------------------------------------------------------ the screen marked

    /// <summary>The screen marked has to reach the canvas, which is the one that draws it.</summary>
    [AvaloniaFact]
    public void La_pantalla_senalada_llega_al_lienzo()
    {
        MapEditorViewModel map = NewMap(64, 48);

        using var editor = new MapWindow(map);

        Assert.Null(editor.Canvas.Highlight);

        map.ShowScreen(new MapRegion(32, 0, 32, 24));

        Assert.Equal(new MapRegion(32, 0, 32, 24), editor.Canvas.Highlight);

        map.ShowScreen(null);

        Assert.Null(editor.Canvas.Highlight);
    }

    /// <summary>
    /// Marking a screen takes the view to it.
    /// </summary>
    /// <remarks>
    /// With a map of twenty screens, marking 7-5 while looking at 1-1 is marking something that
    /// cannot be seen.
    /// </remarks>
    [AvaloniaFact]
    public void Senalar_una_pantalla_lleva_la_vista_hasta_ella()
    {
        MapEditorViewModel map = NewMap(200, 200);

        using var editor = new MapWindow(map);

        Assert.DoesNotContain(176, Columns(editor.Canvas.VisibleRange()));

        map.ShowScreen(new MapRegion(160, 120, 32, 24));

        MapRegion seen = editor.Canvas.VisibleRange();

        Assert.Contains(176, Columns(seen));
        Assert.Contains(132, Rows(seen));
    }

    private static IEnumerable<int> Columns(MapRegion region) =>
        Enumerable.Range(region.Left, region.Width);

    private static IEnumerable<int> Rows(MapRegion region) =>
        Enumerable.Range(region.Top, region.Height);

    // ------------------------------------------------------------------ from the preferences

    /// <summary>
    /// Changing the size in the preferences moves the grid of the maps already open.
    /// </summary>
    /// <remarks>
    /// The preferences live as long as the program, so the maps do not subscribe to them: a
    /// closed map would stay hooked with nobody to let it go. The main window tells them, and
    /// this checks that it really does.
    /// </remarks>
    [AvaloniaFact]
    public void Cambiar_el_tamano_en_la_configuracion_llega_a_los_mapas_abiertos()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));
        MapEditorViewModel map = main.OpenMap(new TileMap("Nivel", 64, 48), tiles);

        map.ShowScreenGrid = true;

        List<string?> changed = [];

        map.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        main.Preferences.ScreenWidth = 16;

        Assert.Equal(16, map.ScreenGridColumns);
        Assert.Contains(nameof(MapEditorViewModel.ScreenGridColumns), changed);
    }

    /// <summary>And turning the grid on in one map turns it on in the others, since it is the same one.</summary>
    [AvaloniaFact]
    public void La_rejilla_se_enciende_para_todos_los_mapas_abiertos()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        MapEditorViewModel first = main.OpenMap(new TileMap("Nivel 1", 64, 48), tiles);
        MapEditorViewModel second = main.OpenMap(new TileMap("Nivel 2", 64, 48), tiles);

        List<string?> changed = [];

        second.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        first.ShowScreenGrid = true;

        Assert.True(second.ShowScreenGrid);
        Assert.Equal(32, second.ScreenGridColumns);
        Assert.Contains(nameof(MapEditorViewModel.ScreenGridColumns), changed);
    }

    private static MapEditorViewModel NewMap(int width, int height, int superTile = 0)
    {
        var tileSet = new TileSet("Bosque");

        if (superTile > 0)
            tileSet.UseSuperTiles(superTile, superTile);

        return new MapEditorViewModel(
            new TileMap("Nivel", width, height),
            new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard()));
    }

    /// <summary>The panel of the map really mounted, which is where it shows whether the wiring gets through.</summary>
    private sealed class MapWindow : IDisposable
    {
        private readonly Window _window;
        private readonly MapEditorView _view;

        public MapWindow(MapEditorViewModel map)
        {
            _view = new MapEditorView { DataContext = map };
            _window = new Window { Content = _view, Width = 1100, Height = 800 };

            _window.Show();
            Pump();
        }

        public MapCanvas Canvas => _view.GetVisualDescendants().OfType<MapCanvas>().Single();

        /// <summary>What reads at the bottom right, which is what the user sees.</summary>
        public string Label => Named<TextBlock>("HoverText").Text ?? string.Empty;

        public void Dispose() => _window.Close();

        public void ToggleScreenGrid() => Click(Named<ToggleButton>("ScreenGridButton"));

        /// <summary>Moves the mouse over a cell of the map.</summary>
        public void Hover(int column, int row)
        {
            MapCanvas canvas = Canvas;

            Point cell = canvas.TranslatePoint(
                new Point((column + 0.5) * canvas.CellWidth, (row + 0.5) * canvas.CellHeight),
                _window)!.Value;

            _window.MouseMove(cell);
            Pump();
        }

        private T Named<T>(string name) where T : Control =>
            _view.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

        private void Click(Visual target)
        {
            Point centre = target.TranslatePoint(
                new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), _window)!.Value;

            _window.MouseDown(centre, MouseButton.Left);
            Pump();
            _window.MouseUp(centre, MouseButton.Left);
            Pump();
        }

        private static void Pump() => Dispatcher.UIThread.RunJobs();
    }
}
