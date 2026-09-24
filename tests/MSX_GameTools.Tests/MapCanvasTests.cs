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

        var tiles = new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard());

        Map = new TileMap("Mapa", 40, 30);

        _canvas = new MapCanvas
        {
            Map = Map,
            TilesByThird = [tiles.Thumbnails],
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

    /// <summary>
    /// Lo que se va a estampar se ve bajo el ratón antes de pulsar. Sin esto hay que
    /// acordarse de lo que se cogió abajo, y con un bloque además de por dónde cae.
    /// </summary>
    [AvaloniaFact]
    public void Lo_que_se_va_a_estampar_se_ve_bajo_el_raton()
    {
        _canvas.Brush = TilePatch.Single(1);

        _window.MouseMove(new Point((CellSize * 2) + 4, (CellSize * 2) + 4));
        Pump();

        // Translucido: ni el blanco del tile ni el fondo pelado.
        Color ghost = PixelAt((int)(CellSize * 2) + 4, (int)(CellSize * 2) + 4);

        Assert.NotEqual(Colors.Magenta, ghost);
        Assert.NotEqual(Colors.White, ghost);

        // Y donde no está el ratón sigue el fondo.
        Assert.Equal(Colors.Magenta, PixelAt(4, 4));
    }

    /// <summary>Marcando o desplazando no se estampa nada, así que no hay nada que enseñar.</summary>
    [AvaloniaFact]
    public void Marcando_no_se_ensena_lo_que_se_estamparia()
    {
        _canvas.Brush = TilePatch.Single(1);
        _canvas.Tool = MapTool.Select;

        _window.MouseMove(new Point((CellSize * 2) + 4, (CellSize * 2) + 4));
        Pump();

        Assert.Equal(Colors.Magenta, PixelAt((int)(CellSize * 2) + 4, (int)(CellSize * 2) + 4));
    }

    /// <summary>
    /// Cada tercio se pinta con el juego de tiles que le toca.
    /// </summary>
    /// <remarks>
    /// Es el corazón de lo de los tercios: el mismo número de tile en la fila 0, en la 8 y en la
    /// 16 son tres dibujos distintos, como en la máquina, donde cada tercio de la pantalla lee
    /// su propio banco de patrones.
    /// </remarks>
    [AvaloniaFact]
    public void Cada_tercio_se_pinta_con_el_juego_que_le_toca()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        // A x1 la celda mide ocho, y así los tres tercios entran en la ventana.
        _canvas.Zoom = 1;
        _canvas.Map = OneTilePerThird();
        _canvas.TilesByThird = [Solid(4), Solid(8), Solid(12)];

        Redraw();

        Assert.Equal(palette[4].Color, PixelAt(4, 4));
        Assert.Equal(palette[8].Color, PixelAt(4, 68));
        Assert.Equal(palette[12].Color, PixelAt(4, 132));
    }

    /// <summary>
    /// Y el fantasma enseña el dibujo del tercio donde va a caer.
    /// </summary>
    /// <remarks>
    /// Es lo que hace honesto estampar a caballo de una frontera: el mismo número cambia de
    /// dibujo al cruzarla, y con el fantasma pintado del juego de origen se estaría prometiendo
    /// una cosa y soltando otra.
    /// </remarks>
    [AvaloniaFact]
    public void El_fantasma_se_pinta_con_el_juego_del_tercio_donde_cae()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        _canvas.Zoom = 1;
        _canvas.Map = new TileMap("Nivel", 8, 24);
        _canvas.TilesByThird = [Solid(4), Solid(8), Solid(12)];
        _canvas.Brush = TilePatch.Single(1);

        // El ratón en la fila 9, que es del tercio de en medio.
        _window.MouseMove(new Point(4, (9 * 8) + 4));
        Pump();

        Color ghost = PixelAt(4, (9 * 8) + 4);

        // Translucido sobre el fondo, así que no es el color pelado; lo que se comprueba es
        // que tira del de en medio y no del de arriba.
        Assert.True(
            Near(ghost, palette[8].Color) < Near(ghost, palette[4].Color),
            $"el fantasma de la fila 9 se parece más al juego de arriba que al del tercio en el que cae");
    }

    /// <summary>
    /// La rejilla de pantallas se pinta por donde parte cada una.
    /// </summary>
    /// <remarks>
    /// Cada cuántas celdas va la línea se comprueba aparte; lo que sólo se ve en la captura es
    /// que de verdad se dibuje, que es la funcionalidad entera.
    /// </remarks>
    [AvaloniaFact]
    public void La_rejilla_de_pantallas_se_pinta_por_donde_parte_cada_una()
    {
        Assert.Equal(0, PaintedDown((int)CellSize * 4));
        Assert.Equal(0, PaintedAcross((int)CellSize * 3));

        _canvas.ScreenColumns = 4;
        _canvas.ScreenRows = 3;
        Redraw();

        Assert.True(PaintedDown((int)CellSize * 4) > 0, "no hay línea por donde parte la pantalla");
        Assert.True(PaintedAcross((int)CellSize * 3) > 0, "no hay línea por donde parte la fila");

        // Y sólo por ahí: por dentro de la pantalla no hay ninguna.
        Assert.Equal(0, PaintedDown((int)CellSize * 2));
        Assert.Equal(0, PaintedAcross((int)CellSize * 2));
    }

    /// <summary>
    /// Cuántos de ocho pixeles seguidos están pintados encima del fondo.
    /// </summary>
    /// <remarks>
    /// Un tramo y no un punto porque la línea va a trazos: un pixel suelto puede caer en el
    /// hueco entre dos y decir que no hay línea donde sí la hay. Ocho es un trazo y su hueco.
    /// </remarks>
    /// <remarks>
    /// El tramo va de 32 a 39 a propósito: es el único que no roza ninguna de las líneas de la
    /// otra dirección, que son de dos pixeles y pisan el de al lado.
    /// </remarks>
    private int PaintedDown(int x) =>
        Enumerable.Range(32, 8).Count(y => PixelAt(x, y) != Colors.Magenta);

    /// <inheritdoc cref="PaintedDown"/>
    private int PaintedAcross(int y) =>
        Enumerable.Range(32, 8).Count(x => PixelAt(x, y) != Colors.Magenta);

    /// <summary>Lo lejos que está un color de otro, para comparar el fantasma translucido.</summary>
    private static int Near(Color painted, Color wanted) =>
        Math.Abs(painted.R - wanted.R) + Math.Abs(painted.G - wanted.G) + Math.Abs(painted.B - wanted.B);

    /// <summary>Un mapa de una pantalla con el tile 1 puesto en cada tercio.</summary>
    private static TileMap OneTilePerThird()
    {
        var map = new TileMap("Nivel", 8, 24);

        foreach (int row in (int[])[0, 8, 16])
            map.Stamp(0, 0, row, TilePatch.Single(1));

        return map;
    }

    /// <summary>Las miniaturas de un juego cuyo tile 1 es un cuadrado macizo de ese color.</summary>
    private static IReadOnlyList<ImageMini> Solid(int color)
    {
        var tileSet = new TileSet("Juego");

        foreach (TileRow line in tileSet.ListOfTiles[1].ArrayTileRows)
            line.BackColor = color;

        return new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard()).Thumbnails;
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

    /// <summary>
    /// Al cambiar el zoom se sigue mirando el mismo sitio del mapa.
    /// </summary>
    /// <remarks>
    /// El desplazamiento va en pixeles de pantalla, así que hay que reescalarlo: sin eso,
    /// alejarse desde un mapa grande dejaba la vista más allá del final del mapa y no se
    /// pintaba ni una celda, sólo el fondo. Parecía que se hubiera borrado todo.
    /// </remarks>
    [AvaloniaFact]
    public void Cambiar_el_zoom_deja_mirando_al_mismo_sitio()
    {
        _canvas.Map = new TileMap("Grande", 96, 96);
        _canvas.Zoom = 4;
        Pump();

        _window.MouseDown(new Point(300, 200), MouseButton.Middle);
        Pump();
        _window.MouseMove(new Point(10, 10), RawInputModifiers.MiddleMouseButton);
        Pump();
        _window.MouseUp(new Point(10, 10), MouseButton.Middle);
        Pump();

        MapRegion cerca = _canvas.VisibleRange();

        Assert.True(cerca.Left > 0, "Hacía falta estar desplazado para probar esto.");

        _canvas.Zoom = 1;
        Pump();

        MapRegion lejos = _canvas.VisibleRange();

        // La misma columna arriba a la izquierda, y ahora se ven mas celdas.
        Assert.Equal(cerca.Left, lejos.Left);
        Assert.Equal(cerca.Top, lejos.Top);
        Assert.True(lejos.Width > cerca.Width);

        // Y sobre todo: se pinta algo. Sin reescalar no entraba en el bucle ni una celda.
        Assert.True(lejos.Width > 0 && lejos.Height > 0);
    }

    /// <summary>
    /// Alejarse desde el otro extremo de un mapa grande tiene que seguir enseñando mapa.
    /// </summary>
    /// <remarks>
    /// Este es el fallo tal como se vio: en un mapa de 96x96 mirado de cerca y desplazado
    /// al fondo, pulsar «Ajustar» dejaba el desplazamiento apuntando más allá del final
    /// del mapa. No entraba ni una celda en el bucle de pintado y la pantalla se quedaba
    /// del color del fondo.
    /// </remarks>
    [AvaloniaFact]
    public void Alejarse_desde_el_fondo_de_un_mapa_grande_sigue_enseñando_mapa()
    {
        _canvas.Map = new TileMap("Grande", 96, 96);
        _canvas.Zoom = 8;
        Pump();

        // Hasta el tope: arrastrar mucho mas alla del borde.
        _window.MouseDown(new Point(300, 200), MouseButton.Middle);
        Pump();
        _window.MouseMove(new Point(-6000, -6000), RawInputModifiers.MiddleMouseButton);
        Pump();
        _window.MouseUp(new Point(-6000, -6000), MouseButton.Middle);
        Pump();

        Assert.True(_canvas.VisibleRange().Left > 50, "Hacía falta estar en el fondo del mapa.");

        _canvas.Zoom = 1;
        Pump();

        MapRegion visible = _canvas.VisibleRange();

        Assert.True(visible.Width > 0, "No se pintaba ni una celda: la pantalla se quedaba negra.");
        Assert.True(visible.Left < _canvas.Map.Width);
    }

    /// <summary>Abrir otro mapa empieza por su esquina, que es donde se empieza a mirar.</summary>
    [AvaloniaFact]
    public void Cambiar_de_mapa_vuelve_a_la_esquina()
    {
        _window.MouseDown(new Point(200, 200), MouseButton.Middle);
        Pump();
        _window.MouseMove(new Point(100, 100), RawInputModifiers.MiddleMouseButton);
        Pump();
        _window.MouseUp(new Point(100, 100), MouseButton.Middle);
        Pump();

        Assert.True(_canvas.VisibleRange().Left > 0);

        _canvas.Map = new TileMap("Otro", 40, 30);
        Pump();

        Assert.Equal(0, _canvas.VisibleRange().Left);
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

        // El orden de los canales lo dice el formato del framebuffer. Leyendo siempre como
        // BGRA salia el color con el rojo y el azul cambiados, y no se veia: las pruebas de
        // aqui comparaban con blanco y con magenta, que son iguales del derecho y del reves.
        return buffer.Format == PixelFormat.Rgba8888
            ? Color.FromRgb((byte)pixel, (byte)(pixel >> 8), (byte)(pixel >> 16))
            : Color.FromRgb((byte)(pixel >> 16), (byte)(pixel >> 8), (byte)pixel);
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
