using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Pasear el ratón por el mapa no repinta el mapa.
/// </summary>
/// <remarks>
/// <para>
/// Medido antes de arreglarlo: un repintado del mapa lleno son unos 35 ms —2982 celdas
/// visibles y casi todo el coste en el <c>DrawImage</c> de cada una—, y se disparaba con
/// cada cruce de celda del ratón, se estuviera pintando o no. Sólo para mover el fantasma
/// de sitio.
/// </para>
/// <para>
/// Se cuentan los repintados y no los milisegundos: el tiempo depende de la máquina y de si
/// hay GPU, y una prueba que mide tiempo falla sola el día que la máquina esté ocupada. Lo
/// que no depende de nada es cuántas veces se pide dibujar.
/// </para>
/// </remarks>
public class MapHoverRepaintTests : IDisposable
{
    private const int Zoom = 2;
    private const double CellSize = TileRow.Columns * Zoom;

    private readonly Window _window;
    private readonly CountingCanvas _canvas;

    public MapHoverRepaintTests()
    {
        var tileSet = new TileSet("Bosque");

        foreach (TileRow line in tileSet.ListOfTiles[1].ArrayTileRows)
            line.BackColor = 15;

        var tiles = new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard());

        _canvas = new CountingCanvas
        {
            Map = new TileMap("Mapa", 40, 30),
            TilesByThird = [tiles.Thumbnails],
            Zoom = Zoom,
            Background = Brushes.Magenta,
            Brush = TilePatch.Single(1),
        };

        _window = new Window { Content = _canvas, Width = 320, Height = 240 };
        _window.Show();
        Pump();
    }

    public void Dispose()
    {
        _window.Close();
        Pump();
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void Mover_el_raton_no_repinta_el_mapa()
    {
        Frame();

        int before = _canvas.Renders;

        // Cinco celdas seguidas, que es un paseo corto por el mapa.
        for (int column = 1; column <= 5; column++)
        {
            _window.MouseMove(new Point((CellSize * column) + 4, CellSize + 4));
            Pump();
            Frame();
        }

        Assert.Equal(before, _canvas.Renders);
    }

    /// <summary>Y lo que sí cambia el mapa lo sigue repintando.</summary>
    /// <remarks>
    /// Sin esto, la comprobación de arriba se cumpliría igual de bien con un lienzo que no
    /// se repintara nunca.
    /// </remarks>
    [AvaloniaFact]
    public void Cambiar_el_zoom_si_repinta_el_mapa()
    {
        Frame();

        int before = _canvas.Renders;

        _canvas.Zoom = 3;
        Pump();
        Frame();

        Assert.True(
            _canvas.Renders > before,
            "El mapa no se ha repintado al cambiar el zoom, así que la otra comprobación "
            + "no está mirando nada.");
    }

    /// <summary>Fuerza un fotograma, que es cuando el renderizador pide dibujar.</summary>
    private void Frame() => _window.CaptureRenderedFrame()?.Dispose();

    private sealed class CountingCanvas : MapCanvas
    {
        public int Renders { get; private set; }

        public override void Render(DrawingContext context)
        {
            Renders++;

            base.Render(context);
        }
    }
}
