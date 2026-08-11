using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MSX_SpritesEditor.Entities;
using Xunit;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// Los tiles de los modos gráficos 2 y 3: 8x8, un byte de máscara y otro de color por
/// línea, con el color de frente en el nibble alto.
/// </summary>
public class TileSetTests
{
    [AvaloniaFact]
    public void Un_juego_nace_con_sus_256_tiles_vacios()
    {
        var tileset = new TileSet("Bosque");

        // La tabla del VDP es de tamaño fijo y un mapa referencia por número: que los
        // tiles aparezcan y desaparezcan sólo haría bailar los índices.
        Assert.Equal(256, tileset.ListOfTiles.Count);
        Assert.All(tileset.ListOfTiles, tile => Assert.All(tile.ArrayTileRows, row => Assert.Equal(0, row.PatternByte)));
        Assert.All(tileset.ListOfTiles, tile => Assert.NotNull(tile.ImageMini));
    }

    [AvaloniaFact]
    public void La_columna_0_es_el_bit_mas_significativo()
    {
        var row = new TileRow();

        row.ArrayPattern[0] = true;
        Assert.Equal(0x80, row.PatternByte);

        row.ArrayPattern[0] = false;
        row.ArrayPattern[7] = true;
        Assert.Equal(0x01, row.PatternByte);
    }

    [AvaloniaFact]
    public void Una_linea_entera_encendida_son_todos_los_bits()
    {
        var row = new TileRow();
        Array.Fill(row.ArrayPattern, true);

        Assert.Equal(0xFF, row.PatternByte);
    }

    /// <summary>El nibble alto es el color de frente y el bajo el de fondo.</summary>
    [AvaloniaFact]
    public void El_byte_de_color_lleva_frente_arriba_y_fondo_abajo()
    {
        var row = new TileRow { ForeColor = 15, BackColor = 4 };

        Assert.Equal(0xF4, row.ColorByte);
    }

    [AvaloniaFact]
    public void Un_tile_tiene_ocho_lineas_de_ocho_pixeles()
    {
        var tile = new Tile();

        Assert.Equal(8, tile.ArrayTileRows.Length);
        Assert.All(tile.ArrayTileRows, row => Assert.Equal(8, row.ArrayPattern.Length));
    }

    /// <summary>
    /// En un tile no hay color transparente que resolver: cada pixel es uno de los dos
    /// colores de su línea, y el 0 es un color de la paleta como cualquier otro.
    /// </summary>
    [AvaloniaFact]
    public void Cada_pixel_se_pinta_con_el_color_que_le_toca_de_su_linea()
    {
        var tile = new Tile { ImageMini = new ImageMini(TileRow.Columns, Tile.Rows) };
        var palette = ColorPalette.CreateMsxStandard();

        tile.ArrayTileRows[3].ForeColor = 8;
        tile.ArrayTileRows[3].BackColor = 1;
        tile.ArrayTileRows[3].ArrayPattern[2] = true;

        TileRenderer.Render(tile, palette, Colors.Black, tile.ImageMini);

        Assert.Equal(ToBgra(palette.GetColor(8)), PixelReader.At(tile.ImageMini, 2, 3));
        Assert.Equal(ToBgra(palette.GetColor(1)), PixelReader.At(tile.ImageMini, 3, 3));
    }

    [AvaloniaFact]
    public void Cada_linea_lleva_sus_propios_dos_colores()
    {
        var tile = new Tile { ImageMini = new ImageMini(TileRow.Columns, Tile.Rows) };
        var palette = ColorPalette.CreateMsxStandard();

        tile.ArrayTileRows[0].BackColor = 4;
        tile.ArrayTileRows[1].BackColor = 6;

        TileRenderer.Render(tile, palette, Colors.Black, tile.ImageMini);

        Assert.Equal(ToBgra(palette.GetColor(4)), PixelReader.At(tile.ImageMini, 0, 0));
        Assert.Equal(ToBgra(palette.GetColor(6)), PixelReader.At(tile.ImageMini, 0, 1));
    }

    /// <summary>
    /// El codigo de color 0 es transparente y deja ver el color del borde, igual que en
    /// los sprites: la tabla de colores de GRAPHIC 2 usa los mismos codigos. El editor lo
    /// pintaba como un color mas de la paleta y enseñaba algo que la maquina no iba a
    /// mostrar.
    /// </summary>
    [AvaloniaFact]
    public void El_color_0_es_transparente_y_deja_ver_el_borde()
    {
        var tile = new Tile { ImageMini = new ImageMini(TileRow.Columns, Tile.Rows) };
        var palette = ColorPalette.CreateMsxStandard();

        tile.ArrayTileRows[0].ForeColor = 0;    // transparente
        tile.ArrayTileRows[0].BackColor = 8;
        tile.ArrayTileRows[0].ArrayPattern[0] = true;

        Color border = palette.GetColor(6);
        TileRenderer.Render(tile, palette, border, tile.ImageMini);

        Assert.Equal(ToBgra(border), PixelReader.At(tile.ImageMini, 0, 0));
        Assert.Equal(ToBgra(palette.GetColor(8)), PixelReader.At(tile.ImageMini, 1, 0));

        // Y no es que se pinte del color 0 de la paleta, que es otro.
        Assert.NotEqual(ToBgra(palette.GetColor(0)), PixelReader.At(tile.ImageMini, 0, 0));
    }

    [AvaloniaFact]
    public void Cambiar_el_borde_cambia_lo_que_se_ve_donde_hay_un_0()
    {
        var tile = new Tile { ImageMini = new ImageMini(TileRow.Columns, Tile.Rows) };
        var palette = ColorPalette.CreateMsxStandard();

        tile.ArrayTileRows[0].BackColor = 0;

        TileRenderer.Render(tile, palette, palette.GetColor(4), tile.ImageMini);
        Assert.Equal(ToBgra(palette.GetColor(4)), PixelReader.At(tile.ImageMini, 0, 0));

        TileRenderer.Render(tile, palette, palette.GetColor(9), tile.ImageMini);
        Assert.Equal(ToBgra(palette.GetColor(9)), PixelReader.At(tile.ImageMini, 0, 0));
    }

    private static int ToBgra(Color color) => (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
}
