using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Traer un rectángulo de una hoja de sprites a un banco con sus grupos.
/// </summary>
/// <remarks>
/// Lo que hay que comprobar no es cómo quedan los patrones sino que el grupo se vea como la
/// celda de la que salió. Por eso las comprobaciones rehacen la composición leyendo el banco
/// —el OR de los colores de los planos que pintan cada pixel, que es lo que hace el VDP— en vez
/// de mirar bit a bit lo que escribió el importador.
/// </remarks>
public class SpriteSheetImporterTests
{
    private const int Cell = 8;

    private static readonly Color White = Color.FromRgb(255, 255, 255);
    private static readonly Color Black = Color.FromRgb(0, 0, 0);
    private static readonly Color Red = Color.FromRgb(255, 0, 0);
    private static readonly Color Clear = Color.FromRgb(0, 255, 0);

    /// <summary>
    /// Colores que sobreviven a la ida y vuelta por la paleta del V9938.
    /// </summary>
    /// <remarks>
    /// Allí cada componente son tres bits, así que sólo 0 y 255 vuelven siendo lo que eran. Con
    /// cualquier otro valor la comprobación fallaría por el redondeo y no por lo que se mira.
    /// </remarks>
    private static readonly Color Blue = Color.FromRgb(0, 0, 255);
    private static readonly Color Yellow = Color.FromRgb(255, 255, 0);
    private static readonly Color Magenta = Color.FromRgb(255, 0, 255);

    private static Color ColorOf(char at) => at switch
    {
        'W' => White,
        'K' => Black,
        'R' => Red,
        'B' => Blue,
        'Y' => Yellow,
        'M' => Magenta,
        _ => Clear,
    };

    /// <summary>Una hoja de celdas de 8x8, una celda por dibujo.</summary>
    private static (int[] Pixels, PixelSize Size) Sheet(params string[][] cells)
    {
        var size = new PixelSize(cells.Length * Cell, Cell);
        int[] pixels = new int[size.Width * size.Height];

        for (int cell = 0; cell < cells.Length; cell++)
        {
            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    char at = y < cells[cell].Length && x < cells[cell][y].Length
                        ? cells[cell][y][x]
                        : '.';

                    Color color = ColorOf(at);

                    pixels[(y * size.Width) + (cell * Cell) + x] =
                        unchecked((int)0xFF000000) | (color.R << 16) | (color.G << 8) | color.B;
                }
            }
        }

        return (pixels, size);
    }

    private static SheetImport Import(
        (int[] Pixels, PixelSize Size) sheet, SheetSelection selection, int maxPlanes = 3) =>
        SpriteSheetImporter.Import(
            sheet.Pixels, sheet.Size, Cell, Clear, selection, maxPlanes, "Bichos");

    /// <summary>
    /// El color con el que se ve cada pixel de un grupo, como lo compone el VDP.
    /// </summary>
    /// <remarks>
    /// El OR de los colores de los planos que pintan ese pixel, y luego el color de la paleta
    /// que haya en ese índice. Es la prueba de fuego: si esto no coincide con el dibujo de la
    /// hoja, da igual lo bien colocados que estén los patrones.
    /// </remarks>
    private static Color Seen(SheetImport import, SpriteGroup group, int x, int y)
    {
        int index = 0;

        foreach (SpriteGroupMember member in group.Members)
        {
            Sprite pattern = import.Bank!.SpritesList[member.PatternIndex];

            if (pattern.ArraySpriteRows[y].ArrayColumns[x])
                index |= member.Rows[y].Color;
        }

        return index == 0 ? Clear : Rgb(import.Palette![index]);
    }

    /// <summary>El color de la paleta llevado a 0-255, que es como venía de la hoja.</summary>
    private static Color Rgb(PaletteColor color) => Color.FromRgb(
        (byte)(color.Red * 255 / 7), (byte)(color.Green * 255 / 7), (byte)(color.Blue * 255 / 7));

    // ------------------------------------------------------------------ lo que se ve

    /// <summary>Lo traído se ve como la celda de la que salió.</summary>
    [AvaloniaFact]
    public void El_grupo_se_ve_como_la_celda_de_la_hoja()
    {
        string[] drawing = ["WWKK....", "WRRK....", ".RRR....", "..KK...."];

        SheetImport import = Import(Sheet(drawing), new SheetSelection(0, 0, 1, 1));

        Assert.True(import.Ok, string.Join(" / ", import.Problems));
        Assert.Single(import.Bank!.Groups);

        SpriteGroup group = import.Bank.Groups[0];

        for (int y = 0; y < Cell; y++)
        {
            for (int x = 0; x < Cell; x++)
            {
                char at = y < drawing.Length && x < drawing[y].Length ? drawing[y][x] : '.';

                Assert.Equal(ColorOf(at), Seen(import, group, x, y));
            }
        }
    }

    /// <summary>
    /// El primer plano va sin CC y los demás con CC.
    /// </summary>
    /// <remarks>
    /// Es lo que encadena el OR en el hardware. Sin el CC de los de detrás, cada plano taparía
    /// al anterior y se vería sólo el último; con CC en el primero, el grupo combinaría con lo
    /// que hubiera debajo en pantalla, que no es suyo.
    /// </remarks>
    [AvaloniaFact]
    public void El_primer_plano_va_sin_cc_y_los_demas_con_cc()
    {
        SheetImport import = Import(Sheet(["WKR....."]), new SheetSelection(0, 0, 1, 1));

        Assert.True(import.Ok, string.Join(" / ", import.Problems));

        SpriteGroup group = import.Bank!.Groups[0];

        Assert.Equal(2, group.Members.Count);
        Assert.All(group.Members[0].Rows, line => Assert.False(line.CombineColor));
        Assert.All(group.Members[1].Rows, line => Assert.True(line.CombineColor));
    }

    /// <summary>Los planos van superpuestos, sin desplazamiento entre ellos.</summary>
    [AvaloniaFact]
    public void Los_planos_van_superpuestos()
    {
        SheetImport import = Import(Sheet(["WKR....."]), new SheetSelection(0, 0, 1, 1));

        Assert.All(import.Bank!.Groups[0].Members, member =>
        {
            Assert.Equal(0, member.OffsetX);
            Assert.Equal(0, member.OffsetY);
        });
    }

    // ------------------------------------------------------------------ los huecos

    /// <summary>
    /// Un patrón que se repite se coloca una vez.
    /// </summary>
    /// <remarks>
    /// Dos celdas de una hoja repiten planos a menudo, y con 64 huecos eso decide si una
    /// selección entra o no.
    /// </remarks>
    [AvaloniaFact]
    public void Un_patron_repetido_se_coloca_una_vez()
    {
        string[] same = ["WKR....."];

        SheetImport import = Import(Sheet(same, same, same), new SheetSelection(0, 0, 3, 1));

        Assert.True(import.Ok, string.Join(" / ", import.Problems));
        Assert.Equal(3, import.Bank!.Groups.Count);

        // Tres grupos, pero los tres apuntan a los mismos dos patrones.
        int[] used = [.. import.Bank.Groups
            .SelectMany(group => group.Members)
            .Select(member => member.PatternIndex)
            .Distinct()];

        Assert.Equal(2, used.Length);
    }

    /// <summary>Una celda vacía no gasta patrón ni deja un grupo hueco.</summary>
    [AvaloniaFact]
    public void Una_celda_vacia_no_deja_grupo()
    {
        SheetImport import = Import(Sheet(["WKR....."], []), new SheetSelection(0, 0, 2, 1));

        Assert.True(import.Ok, string.Join(" / ", import.Problems));
        Assert.Single(import.Bank!.Groups);
    }

    /// <summary>Si no entra en los 64 huecos, se dice antes de escribir nada.</summary>
    [AvaloniaFact]
    public void Lo_que_no_cabe_no_se_trae_a_medias()
    {
        // Cada celda con un dibujo distinto para que no se reaprovechen patrones.
        string[][] cells = [.. Enumerable.Range(0, 40).Select(i =>
            new[] { new string('W', (i % 7) + 1) + new string('K', (i % 5) + 1) + "R" })];

        SheetImport import = Import(Sheet(cells), new SheetSelection(0, 0, 40, 1), maxPlanes: 2);

        Assert.False(import.Ok);
        Assert.Null(import.Bank);
        Assert.Contains(import.Problems, problem => problem.Contains("64"));

        // Y el informe sigue estando, que es lo que dice cuánto sobra.
        Assert.True(import.Analysis.Ok);
        Assert.True(import.Analysis.Patterns > SpriteBank.MaxSprites);
    }

    // ------------------------------------------------------------------ la paleta

    /// <summary>
    /// Cada color acaba en el índice que le tocó en el reparto.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es lo que hace que el OR del hardware dé el color que toca: si el índice 3 no lleva el
    /// color que el reparto le asignó, los pixeles que pintan los dos planos salen de otro
    /// color aunque los patrones estén perfectos.
    /// </para>
    /// <para>
    /// Con dos grupos de tres colores que no se cruzan, y no con tres colores sueltos: allí el
    /// reparto sale 1, 2, 3 —o sea el número del color más uno— y la comprobación pasaba
    /// igual repartiendo por orden de llegada y sin mirar el reparto. Aquí el segundo trío se
    /// va a otros bits y los dos repartos dejan de parecerse.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Cada_color_acaba_en_el_indice_que_le_toco()
    {
        SheetImport import = Import(
            Sheet(["WKR....."], ["BYM....."]), new SheetSelection(0, 0, 2, 1), maxPlanes: 2);

        Assert.True(import.Ok, string.Join(" / ", import.Problems));

        // Que el caso sea de los que distinguen: si alguna vez el reparto volviera a salir
        // correlativo, esta comprobación dejaría de comprobar nada y hay que enterarse.
        Assert.Contains(
            Enumerable.Range(0, import.Analysis.Colors.Count),
            color => import.Analysis.Masks[color] != color + 1);

        for (int color = 0; color < import.Analysis.Colors.Count; color++)
        {
            Assert.Equal(
                import.Analysis.Colors[color],
                Rgb(import.Palette![import.Analysis.Masks[color]]));
        }
    }

    /// <summary>El banco sale de tipo MSX2, que es donde existe el CC.</summary>
    [AvaloniaFact]
    public void El_banco_sale_de_tipo_msx2()
    {
        SheetImport import = Import(Sheet(["WKR....."]), new SheetSelection(0, 0, 1, 1));

        Assert.Equal(SpriteBank.SpriteType.MSX2, import.Bank!.Type);
    }
}
