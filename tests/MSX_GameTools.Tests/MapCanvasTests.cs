using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El lienzo del mapa, con la ventana montada.
/// </summary>
/// <remarks>
/// Aquí no hay un control por celda que preguntar, así que lo único que se puede
/// comprobar es lo que sale pintado y en qué celda dice que se ha pulsado. Es justo donde
/// está el riesgo: el recorte de lo visible y el desplazamiento son aritmética, y en
/// aritmética los fallos no se ven leyendo.
/// </remarks>
public class MapCanvasTests : IDisposable
{
    private const int Zoom = 2;
    private const double CellSize = TileRow.Columns * Zoom;   // 16 pixeles por tile

    private readonly Window _window;
    private readonly MapCanvas _canvas;

    public MapCanvasTests()
    {
        var tileSet = new TileSet("Bosque");

        // El tile 1 entero blanco, para reconocerlo en la captura.
        foreach (TileRow line in tileSet.ListOfTiles[1].ArrayTileRows)
            line.BackColor = 15;

        var tiles = new TileSetEditorViewModel(tileSet, new PaletteLibrary());

        Map = new TileMap("Mapa", 40, 30);

        _canvas = new MapCanvas
        {
            Map = Map,
            Tiles = tiles.Thumbnails,
            Zoom = Zoom,
            Background = Brushes.Magenta,
            ShowGrid = false,
        };

        _window = new Window { Content = _canvas, Width = 320, Height = 240 };
        _window.Show();
        Pump();
    }

    private TileMap Map { get; }

    public void Dispose()
    {
        _window.Close();
        Pump();
    }

    // ------------------------------------------------------------------ el raton

    [AvaloniaFact]
    public void Pulsar_avisa_de_la_celda_que_hay_debajo()
    {
        (int Column, int Row)? pressed = null;
        _canvas.CellPressed += (column, row) => pressed = (column, row);

        Click(new Point((CellSize * 3) + 4, (CellSize * 2) + 4));

        Assert.Equal((3, 2), pressed);
    }

    /// <summary>Con más zoom el mismo punto de pantalla cae en una celda más cercana.</summary>
    [AvaloniaFact]
    public void El_zoom_cambia_la_celda_que_toca_un_punto()
    {
        (int Column, int Row)? pressed = null;
        _canvas.CellPressed += (column, row) => pressed = (column, row);

        _canvas.Zoom = 4;
        Pump();

        Click(new Point((CellSize * 3) + 4, 4));

        Assert.Equal((1, 0), pressed);
    }

    /// <summary>
    /// La rueda pulsada desplaza sin cambiar de herramienta, y después el mismo punto de
    /// pantalla cae en otra celda del mapa.
    /// </summary>
    [AvaloniaFact]
    public void Arrastrar_con_la_rueda_desplaza_el_mapa()
    {
        (int Column, int Row)? pressed = null;
        _canvas.CellPressed += (column, row) => pressed = (column, row);

        _window.MouseDown(new Point(200, 200), MouseButton.Middle);
        Pump();
        _window.MouseMove(new Point(200 - (CellSize * 2), 200), RawInputModifiers.MiddleMouseButton);
        Pump();
        _window.MouseUp(new Point(200 - (CellSize * 2), 200), MouseButton.Middle);
        Pump();

        Click(new Point(4, 4));

        Assert.Equal((2, 0), pressed);
    }

    [AvaloniaFact]
    public void Salir_del_lienzo_apaga_la_posicion()
    {
        var hovers = new List<(int Column, int Row)?>();
        _canvas.HoverChanged += hover => hovers.Add(hover);

        _window.MouseMove(new Point(4, 4));
        Pump();
        _window.MouseMove(new Point(600, 600));
        Pump();

        Assert.Equal((0, 0), hovers[0]);
        Assert.Null(hovers[^1]);
    }

    // ------------------------------------------------------------------ lo pintado

    [AvaloniaFact]
    public void Se_pinta_el_tile_que_hay_en_la_celda()
    {
        Map.Stamp(0, 0, 0, TilePatch.Single(1));
        Redraw();

        // Dentro del tile blanco, y lejos de el, donde se ve el fondo.
        Assert.Equal(Colors.White, PixelAt(4, 4));
        Assert.Equal(Colors.Magenta, PixelAt(200, 200));
    }

    // ------------------------------------------------------------------ lo visible

    /// <summary>
    /// El recorte de lo visible es de donde sale que el coste de pintar no dependa de lo
    /// grande que sea el mapa. En un hueco de 320x240 con tiles de 16 caben 20x15.
    /// </summary>
    [AvaloniaFact]
    public void Solo_se_recorren_las_celdas_que_caben()
    {
        MapRegion visible = _canvas.VisibleRange();

        Assert.Equal(new MapRegion(0, 0, 21, 16), visible);
    }

    [AvaloniaFact]
    public void Con_mas_zoom_caben_menos_celdas()
    {
        _canvas.Zoom = 4;
        Pump();

        MapRegion visible = _canvas.VisibleRange();

        Assert.Equal(new MapRegion(0, 0, 11, 8), visible);
    }

    /// <summary>Al desplazarse, lo que se recorre se mueve con el mapa.</summary>
    [AvaloniaFact]
    public void Desplazarse_mueve_las_celdas_que_se_recorren()
    {
        _window.MouseDown(new Point(200, 200), MouseButton.Middle);
        Pump();
        _window.MouseMove(new Point(200 - (CellSize * 3), 200), RawInputModifiers.MiddleMouseButton);
        Pump();
        _window.MouseUp(new Point(200 - (CellSize * 3), 200), MouseButton.Middle);
        Pump();

        MapRegion visible = _canvas.VisibleRange();

        Assert.Equal(3, visible.Left);
        Assert.Equal(0, visible.Top);
    }

    /// <summary>Un mapa más pequeño que el hueco no se recorre más allá de su borde.</summary>
    [AvaloniaFact]
    public void Un_mapa_pequeno_no_se_recorre_de_mas()
    {
        _canvas.Map = new TileMap("Chico", 4, 3);
        Pump();

        Assert.Equal(new MapRegion(0, 0, 4, 3), _canvas.VisibleRange());
    }

    private void Click(Point point)
    {
        _window.MouseDown(point, MouseButton.Left);
        Pump();
        _window.MouseUp(point, MouseButton.Left);
        Pump();
    }

    private void Redraw()
    {
        _canvas.InvalidateVisual();
        Pump();
    }

    /// <summary>El color de un pixel de la ventana, en coordenadas del lienzo.</summary>
    private Color PixelAt(int x, int y)
    {
        Point origin = _canvas.TranslatePoint(new Point(0, 0), _window)
                       ?? throw new InvalidOperationException("El lienzo no está montado.");

        // Dos veces a proposito: con varias ventanas vivas en el proceso, la primera
        // captura devuelve el fotograma de otra. La segunda ya es la de esta.
        _window.CaptureRenderedFrame();

        using WriteableBitmap frame = _window.CaptureRenderedFrame()
                                     ?? throw new InvalidOperationException("No se pudo capturar el fotograma.");

        using ILockedFramebuffer buffer = frame.Lock();

        int[] row = new int[buffer.Size.Width];
        Marshal.Copy(buffer.Address + ((int)(origin.Y + y) * buffer.RowBytes), row, 0, row.Length);

        int pixel = row[(int)origin.X + x];

        return Color.FromRgb((byte)(pixel >> 16), (byte)(pixel >> 8), (byte)pixel);
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
