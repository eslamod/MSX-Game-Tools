using Avalonia;
using Avalonia.Media;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Traer una hoja a un banco de MSX1.
/// </summary>
/// <remarks>
/// <para>
/// Las dos máquinas no se parecen en nada más que en leer la hoja. En MSX2 el bit CC mezcla con
/// un OR los colores de los sprites solapados, así que con las máscaras a potencias de dos dos
/// planos enseñan tres colores. En MSX1 ese bit no existe: un sprite es de un color y solaparlos
/// no mezcla nada, gana el de más prioridad.
/// </para>
/// <para>
/// Sale de un banco importado que enseñaba tres colores con dos sprites. En el editor se veía
/// bien porque la vista previa hace el OR; en un MSX1 de verdad no se puede ver.
/// </para>
/// </remarks>
public class SheetImportMsx1Tests
{
    private const int Fondo = unchecked((int)0xFF000000);
    private const int Rojo = unchecked((int)0xFFFF0000);
    private const int Verde = unchecked((int)0xFF00FF00);

    /// <summary>Dos colores en la celda son dos sprites, y ninguno lleva CC.</summary>
    [Fact]
    public void En_msx1_cada_color_es_un_sprite_y_no_se_combina()
    {
        SheetImport import = Import(SpriteBank.SpriteType.MSX);

        Assert.True(import.Ok);
        Assert.Equal(SpriteBank.SpriteType.MSX, import.Bank!.Type);

        SpriteGroup group = Assert.Single(import.Bank.Groups);

        Assert.Equal(2, group.Members.Count);

        // Ni una línea con CC: encenderlo escribiría un banco que la máquina no puede enseñar.
        Assert.All(group.Members, member => Assert.All(member.Rows, line => Assert.False(line.CombineColor)));
    }

    /// <summary>
    /// Y los índices de paleta van seguidos, no a potencias de dos.
    /// </summary>
    /// <remarks>
    /// Las potencias de dos existen para que el OR del VDP reconstruya el índice. Sin OR no
    /// pintan nada y sólo gastan huecos de paleta: con cuatro colores se llegaría al índice 8
    /// teniendo el 4 libre.
    /// </remarks>
    [Fact]
    public void En_msx1_los_colores_van_seguidos_en_la_paleta()
    {
        SheetImport import = Import(SpriteBank.SpriteType.MSX);

        Assert.Equal([1, 2], import.Analysis.Masks);

        // Y cada sprite lleva su color, el mismo en sus dieciséis líneas.
        foreach (SpriteGroupMember member in import.Bank!.Groups[0].Members)
        {
            int[] colors = [.. import.Bank.SpritesList[member.PatternIndex].ArraySpriteRows
                .Select(line => line.Color)
                .Distinct()];

            Assert.Single(colors);
        }
    }

    /// <summary>
    /// La misma celda en MSX2 sale con menos sprites, porque ahí el OR sí trabaja.
    /// </summary>
    /// <remarks>
    /// No es que un modo esté mejor que el otro: es que la cuenta es distinta y por eso hay que
    /// preguntarla antes de importar en vez de suponerla.
    /// </remarks>
    [Fact]
    public void En_msx2_los_planos_se_cuentan_por_el_or()
    {
        // Tres colores: el rojo, el verde y el pixel que lleva los dos bits.
        SheetImport msx2 = Import(SpriteBank.SpriteType.MSX2);

        Assert.True(msx2.Ok);
        Assert.Equal(SpriteBank.SpriteType.MSX2, msx2.Bank!.Type);

        // Y ahí sí hay CC, que es lo que encadena el OR.
        Assert.Contains(
            msx2.Bank.Groups[0].Members.Skip(1),
            member => member.Rows.Any(line => line.CombineColor));
    }

    /// <summary>
    /// Con más colores que sprites se dice, en vez de sacar algo que no se puede pintar.
    /// </summary>
    /// <remarks>
    /// En MSX2 dos planos dan tres colores, así que esta misma celda entra. En MSX1 no: tres
    /// colores son tres sprites, y con el tope en dos no hay manera.
    /// </remarks>
    [Fact]
    public void En_msx1_mas_colores_que_sprites_se_avisa()
    {
        int[] pixels = Sheet(Rojo, Verde, unchecked((int)0xFF0000FF));

        SheetAnalysis msx1 = SpriteSheetAnalysis.Analyse(
            pixels, new PixelSize(8, 8), 8, Color.FromUInt32(0xFF000000), Cell, 2,
            SpriteBank.SpriteType.MSX);

        Assert.False(msx1.Ok);
        Assert.Contains("MSX1", Assert.Single(msx1.Problems));

        // La misma con el tope en tres sí entra.
        SheetAnalysis holgada = SpriteSheetAnalysis.Analyse(
            pixels, new PixelSize(8, 8), 8, Color.FromUInt32(0xFF000000), Cell, 3,
            SpriteBank.SpriteType.MSX);

        Assert.True(holgada.Ok);
        Assert.Equal(3, holgada.Planes);
    }

    // ------------------------------------------------------------------ los andamios

    private static SheetSelection Cell => new(0, 0, 1, 1);

    /// <summary>Una celda de 8x8 con los colores que se le digan en filas distintas.</summary>
    private static int[] Sheet(params int[] colors)
    {
        int[] pixels = new int[8 * 8];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Fondo;

        for (int color = 0; color < colors.Length; color++)
        {
            // Cada color en su fila: así en MSX2 ninguna línea necesita dos planos y el
            // reparto sale con menos sprites que colores, que es justo el caso que interesa.
            pixels[(color * 8) + 1] = colors[color];
            pixels[(color * 8) + 2] = colors[color];
        }

        return pixels;
    }

    private static SheetImport Import(SpriteBank.SpriteType type) =>
        SpriteSheetImporter.Import(
            Sheet(Rojo, Verde),
            new PixelSize(8, 8),
            8,
            Color.FromUInt32(0xFF000000),
            Cell,
            maxPlanes: 3,
            "Bichos",
            type);
}
