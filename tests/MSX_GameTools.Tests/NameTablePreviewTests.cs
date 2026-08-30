using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// La tabla de nombres de un volcado, dibujada para ver cuál es.
/// </summary>
/// <remarks>
/// Un volcado no dice qué tabla estaba enseñando el juego, y hay juegos que la cambian a media
/// pantalla con la interrupción de línea. Probando direcciones y mirando se acierta enseguida;
/// analizando bytes, no. Esto es lo que faltaba para dar con la de Space Manbow.
/// </remarks>
public class NameTablePreviewTests
{
    /// <summary>Cada celda se pinta con el tile que dice, y con sus dos colores.</summary>
    [AvaloniaFact]
    public void Cada_celda_se_pinta_con_el_tile_que_dice()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        // El tile 7 empieza con un pixel encendido y el resto de la fila apagado.
        TileSet tiles = Painted(7, fore: 8, back: 4);

        byte[] vram = Named(7);

        ImageMini screen = NameTablePreview.Draw(vram, 0, [tiles], palette)!;

        Assert.Equal(256, screen.Width);
        Assert.Equal(192, screen.Height);

        Assert.Equal(palette.GetColor(8), screen.ColorAt(0, 0));
        Assert.Equal(palette.GetColor(4), screen.ColorAt(1, 0));

        // Y la celda de al lado lo mismo, que es el mismo tile.
        Assert.Equal(palette.GetColor(8), screen.ColorAt(8, 0));
    }

    /// <summary>
    /// Cada tercio de la pantalla usa su juego de tiles.
    /// </summary>
    /// <remarks>
    /// Es lo que distingue GRAPHIC 2 y 3 de SCREEN 1: el mismo número de tile dibuja tres cosas
    /// distintas según en qué tercio de la pantalla caiga. Dibujándolo todo con el primero, un
    /// volcado de tres tercios saldría con los dos de abajo cambiados y no se reconocería.
    /// </remarks>
    [AvaloniaFact]
    public void Cada_tercio_usa_su_juego_de_tiles()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        TileSet[] thirds =
        [
            Painted(7, fore: 2, back: 0),
            Painted(7, fore: 9, back: 0),
            Painted(7, fore: 14, back: 0),
        ];

        ImageMini screen = NameTablePreview.Draw(Named(7), 0, thirds, palette)!;

        Assert.Equal(palette.GetColor(2), screen.ColorAt(0, 0));
        Assert.Equal(palette.GetColor(9), screen.ColorAt(0, 8 * Tile.Rows));
        Assert.Equal(palette.GetColor(14), screen.ColorAt(0, 16 * Tile.Rows));
    }

    /// <summary>
    /// Con un solo juego, los tres tercios son ese.
    /// </summary>
    /// <remarks>
    /// Pasa en SCREEN 1, que no tiene tercios, y en las pantallas de SCREEN 2 que repiten los
    /// tres: el importador saca uno solo y hay que dibujar con él las veinticuatro filas.
    /// </remarks>
    [AvaloniaFact]
    public void Con_un_solo_juego_los_tres_tercios_son_ese()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        ImageMini screen = NameTablePreview.Draw(
            Named(7), 0, [Painted(7, fore: 6, back: 0)], palette)!;

        Assert.Equal(palette.GetColor(6), screen.ColorAt(0, 0));
        Assert.Equal(palette.GetColor(6), screen.ColorAt(0, 8 * Tile.Rows));
        Assert.Equal(palette.GetColor(6), screen.ColorAt(0, 16 * Tile.Rows));
    }

    /// <summary>
    /// Una dirección donde no cabe la tabla no dibuja nada.
    /// </summary>
    /// <remarks>
    /// Se prueban direcciones a mano, así que se llega a las de arriba del todo. Dibujar medio
    /// cuadro con lo que hubiera sería enseñar basura con pinta de dato.
    /// </remarks>
    [AvaloniaFact]
    public void Una_direccion_donde_no_cabe_no_dibuja_nada()
    {
        TileSet[] tiles = [Painted(7, fore: 6, back: 0)];
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        var vram = new byte[1024];

        Assert.NotNull(NameTablePreview.Draw(vram, 1024 - 768, tiles, palette));
        Assert.Null(NameTablePreview.Draw(vram, 1024 - 767, tiles, palette));
        Assert.Null(NameTablePreview.Draw(vram, -1, tiles, palette));
    }

    /// <summary>Y sin juegos de tiles tampoco, que no habría con qué.</summary>
    [AvaloniaFact]
    public void Sin_juegos_de_tiles_no_dibuja_nada()
    {
        Assert.Null(NameTablePreview.Draw(
            Named(7), 0, [], ColorPalette.CreateMsxStandard()));
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un juego donde ese tile lleva el primer píxel de cada fila encendido.</summary>
    private static TileSet Painted(int index, int fore, int back)
    {
        var tiles = new TileSet("Volcado");

        foreach (TileRow row in tiles.ListOfTiles[index].ArrayTileRows)
        {
            row.ArrayPattern[0] = true;
            row.ForeColor = fore;
            row.BackColor = back;
        }

        return tiles;
    }

    /// <summary>Una VRAM con la tabla de nombres al principio, toda del mismo tile.</summary>
    private static byte[] Named(byte tile)
    {
        var vram = new byte[16384];

        for (int at = 0; at < NameTablePreview.Columns * NameTablePreview.Rows; at++)
            vram[at] = tile;

        return vram;
    }
}
