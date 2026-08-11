using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.Services;
using MSX_SpritesEditor.ViewModels;
using Xunit;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// Llevarse un juego de tiles a GIMP y traerlo de vuelta.
/// </summary>
/// <remarks>
/// Exportar no pierde nada; importar sí puede, así que se comprueba antes en vez de
/// aproximar en silencio.
/// </remarks>
public class TileSetPngTests
{
    [AvaloniaFact]
    public void La_imagen_del_juego_entero_mide_256_por_64()
    {
        Assert.Equal(new PixelSize(256, 64), TileSetPngConverter.FullSize);

        int[] pixels = TileSetPngConverter.ToPixels(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

        Assert.Equal(256 * 64, pixels.Length);
    }

    /// <summary>
    /// Un pixel con el codigo 0 sale transparente y no del color del borde: en la maquina
    /// deja ver el fondo, y asi al reimportar vuelve al 0 solo.
    /// </summary>
    [AvaloniaFact]
    public void El_codigo_0_sale_transparente_al_exportar()
    {
        var tileSet = new TileSet("Bosque");
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        tileSet.ListOfTiles[0].ArrayTileRows[0].BackColor = 0;
        tileSet.ListOfTiles[0].ArrayTileRows[1].BackColor = 8;

        int[] pixels = TileSetPngConverter.ToPixels(tileSet, palette);

        Assert.Equal(0u, (uint)pixels[0] >> 24);
        Assert.Equal(255u, (uint)pixels[256] >> 24);
    }

    [AvaloniaFact]
    public void Los_tiles_van_en_el_orden_de_la_rejilla()
    {
        var tileSet = new TileSet("Bosque");
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        // El tile 32 es el primero de la segunda fila de la rejilla.
        tileSet.ListOfTiles[32].ArrayTileRows[0].BackColor = 6;

        int[] pixels = TileSetPngConverter.ToPixels(tileSet, palette);

        Assert.Equal(ToBgra(palette.GetColor(6)), pixels[8 * 256]);
    }

    // ------------------------------------------------------------------ importar

    [AvaloniaFact]
    public void Un_tamano_que_no_es_multiplo_de_8_no_se_importa()
    {
        TileSetImportResult result = Analyse(new int[20 * 8], new PixelSize(20, 8));

        Assert.False(result.Ok);
        Assert.Contains("múltiplos", result.Problems[0].Message);
        Assert.Contains("20x8", result.Problems[0].Message);
    }

    [AvaloniaFact]
    public void Una_imagen_con_mas_de_256_tiles_no_se_importa()
    {
        TileSetImportResult result = Analyse(new int[512 * 64], new PixelSize(512, 64));

        Assert.False(result.Ok);
        Assert.Contains("512 tiles", result.Problems[0].Message);
    }

    /// <summary>
    /// Lo que limita el VDP son los índices, no los colores del png: dos rojos casi
    /// iguales caen en el mismo índice y esa línea es perfectamente legal. Comprobar
    /// antes de mapear la rechazaría sin motivo.
    /// </summary>
    [AvaloniaFact]
    public void Dos_colores_parecidos_que_caen_en_el_mismo_indice_no_estorban()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();
        Color rojo = palette.GetColor(8);

        int[] pixels = new int[8 * 8];
        Array.Fill(pixels, ToBgra(palette.GetColor(1)));

        // Tres colores distintos en la línea 0, pero dos son casi el mismo rojo.
        pixels[0] = ToBgra(rojo);
        pixels[1] = ToBgra(Color.FromRgb((byte)(rojo.R - 1), rojo.G, rojo.B));

        TileSetImportResult result = Analyse(pixels, new PixelSize(8, 8), palette);

        Assert.True(result.Ok, result.Problems.FirstOrDefault()?.Message);

        // Se comprueba que cada pixel acaba en el indice que le toca, y no un patron
        // concreto: cual de los dos colores queda de frente y cual de fondo es una
        // decision arbitraria del importador y da lo mismo, se ve igual.
        TileRow line = result.TileSet!.ListOfTiles[0].ArrayTileRows[0];

        Assert.Equal(8, IndexAt(line, 0));
        Assert.Equal(8, IndexAt(line, 1));
        Assert.Equal(1, IndexAt(line, 2));
    }

    [AvaloniaFact]
    public void Tres_colores_de_verdad_en_una_linea_se_rechazan_diciendo_donde()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        int[] pixels = new int[8 * 8];
        Array.Fill(pixels, ToBgra(palette.GetColor(1)));

        pixels[0] = ToBgra(palette.GetColor(8));
        pixels[1] = ToBgra(palette.GetColor(6));

        TileSetImportResult result = Analyse(pixels, new PixelSize(8, 8), palette);

        Assert.False(result.Ok);
        Assert.Single(result.Problems);
        Assert.Equal(0, result.Problems[0].TileIndex);
        Assert.Equal(0, result.Problems[0].Row);
        Assert.Contains("3 colores", result.Problems[0].Message);
    }

    /// <summary>Una imagen mala da cientos de problemas y el diálogo se vuelve ilegible.</summary>
    [AvaloniaFact]
    public void El_listado_de_problemas_se_corta()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        // Cuatro colores en cada línea de una imagen de 32 tiles: 256 problemas.
        int[] pixels = new int[256 * 8];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = ToBgra(palette.GetColor(1 + (i % 4)));

        TileSetImportResult result = Analyse(pixels, new PixelSize(256, 8), palette);

        Assert.False(result.Ok);
        Assert.InRange(result.Problems.Count, 1, TileSetPngConverter.MaxReportedProblems + 1);
    }

    [AvaloniaFact]
    public void Un_pixel_transparente_entra_como_codigo_0()
    {
        int[] pixels = new int[8 * 8];   // todo a cero: alfa 0

        TileSetImportResult result = Analyse(pixels, new PixelSize(8, 8));

        Assert.True(result.Ok);
        Assert.Equal(0, result.TileSet!.ListOfTiles[0].ArrayTileRows[0].BackColor);
    }

    // ------------------------------------------------------------------ ida y vuelta

    /// <summary>
    /// Exportar e importar tiene que devolver lo mismo <i>en pantalla</i>. Los bytes
    /// pueden cambiar: en una línea de dos colores no hay forma de saber cuál era el
    /// frente, así que el patrón puede volver invertido con los colores intercambiados.
    /// </summary>
    [AvaloniaFact]
    public void Exportar_e_importar_devuelve_la_misma_imagen()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();
        var tileSet = new TileSet("Bosque");

        var random = new Random(1234);

        for (int index = 0; index < 40; index++)
        {
            foreach (TileRow line in tileSet.ListOfTiles[index].ArrayTileRows)
            {
                line.ForeColor = random.Next(1, ColorPalette.Size);
                line.BackColor = random.Next(1, ColorPalette.Size);

                for (int column = 0; column < TileRow.Columns; column++)
                    line.ArrayPattern[column] = random.Next(2) == 1;
            }
        }

        int[] exported = TileSetPngConverter.ToPixels(tileSet, palette);

        TileSetImportResult result = TileSetPngConverter.Analyse(
            exported, TileSetPngConverter.FullSize, palette, "Vuelta");

        Assert.True(result.Ok, result.Problems.FirstOrDefault()?.Message);

        int[] again = TileSetPngConverter.ToPixels(result.TileSet!, palette);

        Assert.Equal(exported, again);
    }

    // ------------------------------------------------------------------ paleta generada

    [AvaloniaFact]
    public void Una_paleta_generada_deja_libre_el_indice_0()
    {
        Assert.Equal(15, TileSetPngConverter.MaxGeneratedColors);

        ColorPalette palette = TileSetPngConverter.BuildPalette(
            "De la imagen", [Color.FromRgb(255, 0, 0), Color.FromRgb(0, 255, 0)]);

        // El primer color de la imagen va al 1, no al 0: el 0 es el transparente del VDP
        // y usarlo como color haria que esos pixeles enseñaran el borde en la maquina.
        Assert.Equal("700", palette[1].HexRgb);
        Assert.Equal("070", palette[2].HexRgb);
    }

    /// <summary>
    /// Traer un png con paleta generada dejaba el juego entero de color: lo vacío es el
    /// código 0, el editor lo pinta del color del borde, y el borde arrancaba fijo en el
    /// índice 1, que la paleta generada acababa de darle al primer color de la imagen.
    /// </summary>
    [AvaloniaFact]
    public void El_borde_de_una_paleta_generada_no_se_lleva_el_primer_color_de_la_imagen()
    {
        ColorPalette generated = TileSetPngConverter.BuildPalette(
            "De la imagen", [Color.FromRgb(255, 0, 0), Color.FromRgb(0, 0, 255)]);

        var palettes = new PaletteLibrary();
        palettes.Palettes.Add(generated);
        palettes.ActivePalette = generated;

        var editor = new TileSetEditorViewModel(new TileSet("Importado"), palettes);

        Assert.NotEqual(1, editor.BorderColorIndex);
        Assert.Equal("000", editor.BorderColor.HexRgb);

        // Y un tile recién importado, que es todo código 0, se ve de ese negro.
        Assert.Equal(
            Color.FromRgb(0, 0, 0),
            TileRenderer.ColorAt(editor.TileSet.ListOfTiles[0], generated, editor.BorderColor.Color, 0, 0));
    }

    [AvaloniaFact]
    public void Los_colores_se_recortan_a_los_tres_bits_del_MSX2()
    {
        ColorPalette palette = TileSetPngConverter.BuildPalette("X", [Color.FromRgb(128, 128, 128)]);

        // 128 de 255 son 3,5 septimos: se queda en 3.
        Assert.Equal("333", palette[1].HexRgb);
    }

    [AvaloniaFact]
    public void Los_colores_distintos_se_cuentan_sin_los_transparentes()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        int[] pixels =
        [
            0,
            ToBgra(palette.GetColor(4)),
            ToBgra(palette.GetColor(4)),
            ToBgra(palette.GetColor(9)),
        ];

        Assert.Equal(2, TileSetPngConverter.DistinctColors(pixels).Count);
    }

    /// <summary>El indice de paleta con el que se ve un pixel de la linea.</summary>
    private static int IndexAt(TileRow line, int column) =>
        line.ArrayPattern[column] ? line.ForeColor : line.BackColor;

    private static TileSetImportResult Analyse(int[] pixels, PixelSize size, ColorPalette? palette = null) =>
        TileSetPngConverter.Analyse(pixels, size, palette ?? ColorPalette.CreateMsxStandard(), "Importado");

    private static int ToBgra(Color color) => (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
}
