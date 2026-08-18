using Avalonia;
using Avalonia.Media;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Traer figuras que no caben en un sprite.
/// </summary>
/// <remarks>
/// Un personaje de 16x32 son dos sprites apilados. Lo que se quiere es que entren en un mismo
/// grupo, cabeza y cuerpo, cada uno con su desplazamiento, en vez de quedar como dos grupos
/// sueltos que hay que volver a juntar a mano.
/// </remarks>
public class SheetImportFigureTests
{
    private const int Fondo = unchecked((int)0xFF000000);
    private const int Rojo = unchecked((int)0xFFFF0000);
    private const int Verde = unchecked((int)0xFF00FF00);

    /// <summary>Los dos trozos van al mismo grupo, y el de abajo desplazado un sprite.</summary>
    [Fact]
    public void Una_figura_de_dos_sprites_entra_en_un_grupo()
    {
        SheetImport import = Import(SpriteBank.SpriteType.MSX, down: 2);

        Assert.True(import.Ok);

        SpriteGroup group = Assert.Single(import.Bank!.Groups);

        Assert.Equal(2, group.Members.Count);
        Assert.Equal(0, group.Members[0].OffsetY);
        Assert.Equal(8, group.Members[1].OffsetY);
    }

    /// <summary>
    /// Los sprites de una figura apilada no comparten línea de barrido, así que no se suman.
    /// </summary>
    /// <remarks>
    /// Es el número que decide si la figura se puede enseñar: el VDP deja de pintar a partir de
    /// cuatro sprites por línea en modo 1 y ocho en el 2. Contar la figura entera diría que una
    /// de 16x32 con un color gasta dos, y gasta uno.
    /// </remarks>
    [Fact]
    public void Los_sprites_apilados_no_cuentan_en_la_misma_linea()
    {
        SheetAnalysis stacked = Analyse(down: 2);

        // Un color por trozo: un sprite en cada línea de barrido, no dos.
        Assert.Equal(1, stacked.Planes);

        // Pero gasta los dos patrones del banco, que es otra cuenta.
        Assert.Equal(2, stacked.Patterns);
    }

    /// <summary>Y los que van lado a lado sí, que comparten las mismas líneas.</summary>
    [Fact]
    public void Los_sprites_de_al_lado_si_cuentan_en_la_misma_linea()
    {
        SheetAnalysis side = SpriteSheetAnalysis.Analyse(
            Sheet(8, 16, (0, 0, Rojo), (8, 0, Verde)),
            new PixelSize(16, 8),
            8,
            Color.FromUInt32(0xFF000000),
            new SheetSelection(0, 0, 2, 1),
            maxPlanes: 3,
            SpriteBank.SpriteType.MSX,
            across: 2);

        Assert.Equal(2, side.Planes);
        Assert.Equal(2, side.Patterns);
    }

    /// <summary>
    /// En MSX2, el primer plano de cada trozo se queda sin CC.
    /// </summary>
    /// <remarks>
    /// Una línea con CC sólo se dibuja si en esa misma línea de pantalla hay antes un sprite con
    /// CC a 0. Los trozos de bandas distintas no comparten ninguna línea, así que si al primero
    /// del trozo de abajo se le enciende el CC no lo habilita nadie y no se dibuja.
    /// </remarks>
    [Fact]
    public void Cada_trozo_tiene_su_plano_sin_cc()
    {
        SheetImport import = SpriteSheetImporter.Import(
            Sheet(8, 16, (0, 0, Rojo), (1, 0, Verde), (0, 8, Rojo), (1, 8, Verde)),
            new PixelSize(8, 16),
            8,
            Color.FromUInt32(0xFF000000),
            new SheetSelection(0, 0, 1, 2),
            maxPlanes: 3,
            "Bichos",
            SpriteBank.SpriteType.MSX2,
            across: 1,
            down: 2);

        Assert.True(import.Ok);

        SpriteGroup group = Assert.Single(import.Bank!.Groups);

        // Dos colores por trozo y dos trozos: cuatro planos.
        Assert.Equal(4, group.Members.Count);

        // Uno sin CC por cada banda, y es el primero de la suya.
        List<int> enablers =
        [
            .. group.Members
                .Select((member, index) => (member, index))
                .Where(pair => pair.member.Rows.All(line => !line.CombineColor))
                .Select(pair => pair.index),
        ];

        Assert.Equal([0, 2], enablers);
        Assert.Equal(0, group.Members[0].OffsetY);
        Assert.Equal(8, group.Members[2].OffsetY);
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Una hoja del tamaño que se diga con los pixeles que se le pongan.</summary>
    private static int[] Sheet(int width, int height, params (int X, int Y, int Color)[] dots)
    {
        int[] pixels = new int[width * height];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Fondo;

        foreach ((int x, int y, int color) in dots)
            pixels[(y * width) + x] = color;

        return pixels;
    }

    /// <summary>Una figura apilada: un color arriba y otro abajo, uno en cada trozo.</summary>
    private static int[] Stacked() => Sheet(8, 16, (0, 0, Rojo), (0, 8, Verde));

    private static SheetAnalysis Analyse(int down) => SpriteSheetAnalysis.Analyse(
        Stacked(),
        new PixelSize(8, 16),
        8,
        Color.FromUInt32(0xFF000000),
        new SheetSelection(0, 0, 1, 2),
        maxPlanes: 3,
        SpriteBank.SpriteType.MSX,
        across: 1,
        down: down);

    private static SheetImport Import(SpriteBank.SpriteType type, int down) =>
        SpriteSheetImporter.Import(
            Stacked(),
            new PixelSize(8, 16),
            8,
            Color.FromUInt32(0xFF000000),
            new SheetSelection(0, 0, 1, 2),
            maxPlanes: 3,
            "Bichos",
            type,
            across: 1,
            down: down);
}
