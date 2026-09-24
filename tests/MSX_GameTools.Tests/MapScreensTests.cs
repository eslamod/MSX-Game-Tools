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
/// Las pantallas del juego encima del mapa.
/// </summary>
/// <remarks>
/// Un juego de pantallas fijas se dibuja como un mapa entero y luego se carga de pantalla en
/// pantalla. El mapa no sabe nada de eso: lo que mide una pantalla está en la configuración, y
/// con ello el editor enseña por dónde parte cada una y dice en cuál se está.
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
    /// Sin la rejilla no se pintan líneas ni se dice la pantalla.
    /// </summary>
    /// <remarks>
    /// El número por su cuenta no significa nada: sin las líneas no se ve dónde parte cada
    /// pantalla, así que un «3-1» al lado de las coordenadas sería un número sin referencia.
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

    /// <summary>Columna primero y contando desde uno, como se nombran al pedirlas.</summary>
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
    /// En un mapa de supertiles la rejilla va en celdas, que es lo que el mapa cuenta.
    /// </summary>
    /// <remarks>
    /// La pantalla se mide en tiles, que es lo que ve quien juega, pero una celda de un mapa de
    /// supertiles son varios tiles. Pedirla en tiles pintaría una línea cada dos celdas.
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
    /// Una pantalla que corta un supertile por la mitad no se enseña.
    /// </summary>
    /// <remarks>
    /// Con supertiles de 2x2 y una pantalla de 21 filas —tres de marcador— la línea caería a
    /// media celda, y no hay dónde ponerla: una rejilla que miente por medio supertile es peor
    /// que ninguna. Una de 22 sí cuadra, y ésa se pinta.
    /// </remarks>
    [AvaloniaFact]
    public void Una_pantalla_que_parte_un_supertile_no_se_ensena()
    {
        MapEditorViewModel map = NewMap(32, 24, superTile: 2);

        map.Preferences.ScreenHeight = 21;
        map.ShowScreenGrid = true;

        Assert.Equal(16, map.ScreenGridColumns);
        Assert.Equal(0, map.ScreenGridRows);

        // Y sin las dos, tampoco se dice la pantalla: la fila no tendría de dónde salir.
        Assert.Equal(string.Empty, map.ScreenAt(0, 0));
    }

    /// <summary>Un tamaño imposible se recorta en vez de dejar el editor dividiendo por cero.</summary>
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

    // ------------------------------------------------------------------ con la ventana montada

    /// <summary>El botón de la barra tiene que llegar al lienzo, que es quien pinta.</summary>
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
    /// Y la etiqueta de abajo dice en qué pantalla está el ratón.
    /// </summary>
    /// <remarks>
    /// Sobre una celda vacía a propósito: es justo donde uno se pregunta qué pantalla es la
    /// que se ha dejado sin dibujar.
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

    /// <summary>Sin rejilla, la etiqueta se queda como estaba.</summary>
    [AvaloniaFact]
    public void Sin_rejilla_la_etiqueta_no_dice_ninguna_pantalla()
    {
        using var editor = new MapWindow(NewMap(64, 48));

        editor.Hover(17, 13);

        Assert.Equal("17, 13", editor.Label);
    }

    // ------------------------------------------------------------------ desde la configuración

    /// <summary>
    /// Cambiar el tamaño en la configuración mueve la rejilla de los mapas ya abiertos.
    /// </summary>
    /// <remarks>
    /// La configuración vive todo el programa, así que los mapas no se suscriben a ella: un mapa
    /// cerrado se quedaría enganchado sin que nadie lo suelte. Avisa la ventana principal, y esto
    /// comprueba que de verdad avisa.
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

    /// <summary>Y encender la rejilla en un mapa la enciende en los demás, que es la misma.</summary>
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

    /// <summary>El panel del mapa montado de verdad, que es donde se ve si el cableado llega.</summary>
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

        /// <summary>Lo que se lee abajo a la derecha, que es lo que el usuario ve.</summary>
        public string Label => Named<TextBlock>("HoverText").Text ?? string.Empty;

        public void Dispose() => _window.Close();

        public void ToggleScreenGrid() => Click(Named<ToggleButton>("ScreenGridButton"));

        /// <summary>Pasa el ratón por una celda del mapa.</summary>
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
