using Avalonia;
using Avalonia.Media;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Encajar los colores de la hoja en una paleta que ya existe.
/// </summary>
/// <remarks>
/// <para>
/// Sólo en MSX1, y no por pereza: en MSX2 el índice de cada color lo fija el reparto de planos,
/// porque es lo que hace que el OR del bit CC reconstruya el color de cada pixel. Ahí los
/// índices no se pueden elegir y encajarlos en otros rompería la cuenta.
/// </para>
/// <para>
/// Lo que se gana es no acabar con una paleta por cada png importado. Lo que se pierde es que
/// los colores ya no son los de la imagen, sino los más parecidos que hubiera.
/// </para>
/// </remarks>
public class SheetReusePaletteTests
{
    private const int Fondo = 0;

    /// <summary>Cada color se lleva el índice del más parecido, y no sale paleta nueva.</summary>
    [Fact]
    public void Los_colores_se_encajan_en_los_mas_parecidos()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        // Dos colores casi iguales a dos de la paleta, pero no exactos.
        Color one = Nudge(palette.GetColor(4));
        Color two = Nudge(palette.GetColor(9));

        SheetImport import = Import(Sheet(one, two), palette);

        Assert.True(import.Ok, string.Join(" / ", import.Problems));

        // Ni una paleta nueva: los colores ya son los de la que hay.
        Assert.Null(import.Palette);
        Assert.Equal([4, 9], import.Analysis.Masks);
    }

    /// <summary>Sin decir nada, sigue saliendo una paleta con los colores del png.</summary>
    [Fact]
    public void Sin_encajar_sale_la_paleta_de_la_hoja()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        SheetImport import = Import(Sheet(Nudge(palette.GetColor(4)), Nudge(palette.GetColor(9))), null);

        Assert.True(import.Ok);
        Assert.NotNull(import.Palette);
        Assert.Equal([1, 2], import.Analysis.Masks);
    }

    /// <summary>
    /// Dos colores que caen en el mismo de la paleta se dicen, no se juntan en silencio.
    /// </summary>
    /// <remarks>
    /// Juntarlos se vería en la máquina: dos partes del dibujo que eran distintas pasarían a ser
    /// del mismo color. O se retoca la hoja, o se le hace sitio a ese color, o se trae con
    /// paleta nueva; para elegir entre las tres hay que saber cuáles son.
    /// </remarks>
    [Fact]
    public void Dos_colores_en_el_mismo_hueco_se_avisan()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        // Los dos pegadísimos al mismo color de la paleta.
        Color target = palette.GetColor(4);

        SheetImport import = Import(Sheet(Nudge(target), Nudge(target, 2)), palette);

        Assert.False(import.Ok);
        Assert.Contains("no caben todos los colores", Assert.Single(import.Problems));
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un color casi igual a otro, para que encaje en él sin ser el mismo.</summary>
    private static Color Nudge(Color color, int by = 1) => Color.FromRgb(
        (byte)Math.Clamp(color.R + by, 0, 255),
        (byte)Math.Clamp(color.G + by, 0, 255),
        (byte)Math.Clamp(color.B + by, 0, 255));

    /// <summary>Una celda de 8x8 con una fila de cada color.</summary>
    private static int[] Sheet(params Color[] colors)
    {
        int[] pixels = new int[8 * 8];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Fondo;

        for (int row = 0; row < colors.Length; row++)
        {
            pixels[(row * 8) + 1] = unchecked((int)0xFF000000)
                | (colors[row].R << 16) | (colors[row].G << 8) | colors[row].B;
        }

        return pixels;
    }

    private static SheetImport Import(int[] pixels, ColorPalette? reuse) =>
        SpriteSheetImporter.Import(
            pixels,
            new PixelSize(8, 8),
            8,
            transparent: null,
            new SheetSelection(0, 0, 1, 1),
            maxPlanes: 3,
            "Bichos",
            SpriteBank.SpriteType.MSX,
            across: 1,
            down: 1,
            reuse);
}
