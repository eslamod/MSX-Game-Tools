using Avalonia;
using Avalonia.Media;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Partir una celda de la hoja en los sprites que hay que superponer.
/// </summary>
/// <remarks>
/// Lo único que de verdad importa aquí es que lo descompuesto se vea como el original. Por eso
/// casi todas las comprobaciones vuelven a montar la celda haciendo el OR de los planos, que es
/// lo que hace el VDP, y la comparan con lo que había: mirar los planos uno a uno diría que el
/// código hace lo que dice, no que el resultado sea el bueno.
/// </remarks>
public class SpriteSheetDecomposerTests
{
    private const int Cell = 8;

    private static readonly Color White = Color.FromRgb(255, 255, 255);
    private static readonly Color Black = Color.FromRgb(0, 0, 0);
    private static readonly Color Red = Color.FromRgb(255, 0, 0);
    private static readonly Color Clear = Color.FromRgb(0, 255, 0);

    /// <summary>Una celda pintada a mano: una letra por pixel, '.' es transparente.</summary>
    private static (int[] Pixels, PixelSize Size) Cellof(params string[] rows)
    {
        var size = new PixelSize(Cell, Cell);
        int[] pixels = new int[Cell * Cell];

        for (int y = 0; y < Cell; y++)
        {
            for (int x = 0; x < Cell; x++)
                pixels[(y * Cell) + x] = Bgra(ColorOf(At(rows, x, y)));
        }

        return (pixels, size);
    }

    private static char At(IReadOnlyList<string> rows, int x, int y) =>
        y < rows.Count && x < rows[y].Length ? rows[y][x] : '.';

    private static Color ColorOf(char at) => at switch
    {
        'W' => White,
        'K' => Black,
        'R' => Red,
        _ => Clear,
    };

    private static int Bgra(Color color) =>
        unchecked((int)0xFF000000) | (color.R << 16) | (color.G << 8) | color.B;

    /// <summary>
    /// Los índices que debería tener cada pixel, sacados del dibujo y no del código.
    /// </summary>
    /// <remarks>
    /// A mano desde las letras a propósito: calcular lo esperado con las mismas funciones que
    /// se están probando daría por bueno cualquier error que tuvieran las dos veces.
    /// </remarks>
    private static int[,] Expected(
        IReadOnlyList<string> rows, IReadOnlyList<Color> colors, IReadOnlyList<int> masks)
    {
        int[,] indices = new int[Cell, Cell];

        for (int y = 0; y < Cell; y++)
        {
            for (int x = 0; x < Cell; x++)
            {
                Color color = ColorOf(At(rows, x, y));

                for (int number = 0; number < colors.Count; number++)
                {
                    if (colors[number] == color)
                        indices[y, x] = masks[number];
                }
            }
        }

        return indices;
    }

    private static IReadOnlyList<SheetPlane> Decompose(
        (int[] Pixels, PixelSize Size) cell, IReadOnlyList<Color> colors, IReadOnlyList<int> masks) =>
        SpriteSheetDecomposer.Decompose(
            cell.Pixels, cell.Size, Cell, Clear, colors, masks, column: 0, row: 0);

    /// <summary>
    /// Tres colores con el reparto 1, 2, 3 salen a dos planos y se ven igual.
    /// </summary>
    /// <remarks>
    /// El caso que hace útil el CC: el tercer color es el OR de los otros dos, así que sus
    /// pixeles los pintan los dos sprites a la vez.
    /// </remarks>
    [Fact]
    public void Tres_colores_a_dos_planos_se_ven_igual()
    {
        string[] drawing = ["WWKK....", "WRRK....", ".RRR....", "..KK...."];

        IReadOnlyList<Color> colors = [White, Black, Red];
        int[] masks = [1, 2, 3];

        IReadOnlyList<SheetPlane> planes = Decompose(Cellof(drawing), colors, masks);

        Assert.Equal(2, planes.Count);
        Assert.Equal(Expected(drawing, colors, masks), SpriteSheetDecomposer.Compose(planes, Cell));
    }

    /// <summary>El rojo, que es el OR de los otros dos, lo pintan los dos planos.</summary>
    [Fact]
    public void El_color_combinado_lo_pintan_los_dos_planos()
    {
        (int[], PixelSize) cell = Cellof("WKR.....");

        IReadOnlyList<Color> colors = [White, Black, Red];
        int[] masks = [1, 2, 3];

        IReadOnlyList<SheetPlane> planes = Decompose(cell, colors, masks);

        // El plano del bit 0 pinta el blanco y el rojo; el del bit 1, el negro y el rojo.
        Assert.True(planes[0].Rows[0][0]);
        Assert.False(planes[0].Rows[0][1]);
        Assert.True(planes[0].Rows[0][2]);

        Assert.False(planes[1].Rows[0][0]);
        Assert.True(planes[1].Rows[0][1]);
        Assert.True(planes[1].Rows[0][2]);
    }

    /// <summary>Los planos van coloreados a potencias de dos, de menor a mayor bit.</summary>
    /// <remarks>
    /// El orden fijo no es estético: es lo que hace que dos celdas con los mismos colores den
    /// sus planos en el mismo orden y sus patrones se puedan comparar para no repetirlos.
    /// </remarks>
    [Fact]
    public void Los_planos_salen_a_potencias_de_dos_y_en_orden()
    {
        (int[], PixelSize) cell = Cellof("WKR.....");

        IReadOnlyList<SheetPlane> planes = Decompose(cell, [White, Black, Red], [1, 2, 3]);

        Assert.Equal([1, 2], planes.Select(plane => plane.Color));
    }

    /// <summary>
    /// Un bit que la celda no usa no se lleva un plano.
    /// </summary>
    /// <remarks>
    /// Un plano en blanco gastaría un hueco del banco y uno de los sprites que caben en la
    /// línea de barrido, para no pintar nada.
    /// </remarks>
    [Fact]
    public void Un_bit_que_no_se_usa_no_gasta_plano()
    {
        (int[], PixelSize) cell = Cellof("WWWW....");

        // El reparto tiene cuatro colores, pero esta celda sólo lleva blanco.
        IReadOnlyList<SheetPlane> planes = Decompose(cell, [White, Black, Red], [1, 2, 4]);

        Assert.Single(planes);
        Assert.Equal(1, planes[0].Color);
    }

    /// <summary>Una celda entera en transparente no gasta ningún patrón.</summary>
    [Fact]
    public void Una_celda_vacia_no_gasta_nada()
    {
        Assert.Empty(Decompose(Cellof(), [White], [1]));
    }

    /// <summary>
    /// Un reparto malo se ve al recomponer, aunque los planos parezcan correctos.
    /// </summary>
    /// <remarks>
    /// Con el blanco en 1, el negro en 2 y el rojo en 4 hacen falta tres planos para lo mismo.
    /// Sigue viéndose igual —eso es lo que hay que comprobar— pero cuesta un sprite más, que
    /// es justo lo que el reparto de índices existe para evitar.
    /// </remarks>
    [Fact]
    public void Un_reparto_peor_se_ve_igual_pero_cuesta_un_plano_mas()
    {
        string[] drawing = ["WKR....."];

        IReadOnlyList<Color> colors = [White, Black, Red];
        int[] worse = [1, 2, 4];

        IReadOnlyList<SheetPlane> planes = Decompose(Cellof(drawing), colors, worse);

        Assert.Equal(3, planes.Count);
        Assert.Equal(Expected(drawing, colors, worse), SpriteSheetDecomposer.Compose(planes, Cell));
    }

    /// <summary>
    /// Lo que dijo el análisis que iba a costar es lo que cuesta.
    /// </summary>
    /// <remarks>
    /// Las dos etapas leen la misma hoja y tienen que estar de acuerdo: si el informe dice tres
    /// planos y la descomposición saca cuatro, el aviso de si entra en los 64 huecos no vale
    /// para nada.
    /// </remarks>
    [Fact]
    public void Los_planos_son_los_que_anuncio_el_analisis()
    {
        (int[] pixels, PixelSize size) = Cellof(
            "WWKK....",
            "WRRK....",
            ".RRR....");

        SheetAnalysis analysis = SpriteSheetAnalysis.Analyse(
            pixels, size, Cell, Clear, new SheetSelection(0, 0, 1, 1), maxPlanes: 2);

        Assert.True(analysis.Ok, string.Join(" / ", analysis.Problems));

        IReadOnlyList<SheetPlane> planes = SpriteSheetDecomposer.Decompose(
            pixels, size, Cell, Clear, analysis.Colors, analysis.Masks, column: 0, row: 0);

        Assert.Equal(analysis.Planes, planes.Count);
        Assert.Equal(analysis.Patterns, planes.Count);
    }
}
