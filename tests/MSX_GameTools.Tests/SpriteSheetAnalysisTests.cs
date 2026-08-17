using Avalonia;
using Avalonia.Media;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Lo que se sabe de una hoja de sprites antes de traer nada.
/// </summary>
/// <remarks>
/// No es una previa opcional: sin repartir los índices no se puede descomponer, y sin contar
/// los patrones no se sabe si cabe en los 64 huecos del banco. Es la primera etapa, y lo que se
/// enseña antes de escribir es su resultado.
/// </remarks>
public class SpriteSheetAnalysisTests
{
    private const int Cell = 16;

    /// <summary>Blanco, negro, rojo y azul, que es el reparto típico de una hoja.</summary>
    private static readonly Color White = Color.FromRgb(255, 255, 255);
    private static readonly Color Black = Color.FromRgb(0, 0, 0);
    private static readonly Color Red = Color.FromRgb(255, 0, 0);
    private static readonly Color Blue = Color.FromRgb(0, 0, 255);
    private static readonly Color Clear = Color.FromRgb(0, 255, 0);

    /// <summary>
    /// Una hoja de celdas de 16x16, pintada a mano celda por celda.
    /// </summary>
    /// <param name="cells">
    /// Los colores de cada celda por filas. Cada celda se pinta con sus colores repartidos por
    /// líneas: la línea i lleva el color i, y así se controla qué coincide con qué.
    /// </param>
    private static (int[] Pixels, PixelSize Size) Sheet(int columns, int rows, Color[][] cells)
    {
        var size = new PixelSize(columns * Cell, rows * Cell);
        int[] pixels = new int[size.Width * size.Height];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Bgra(Clear);

        for (int cell = 0; cell < cells.Length; cell++)
        {
            int left = (cell % columns) * Cell;
            int top = (cell / columns) * Cell;
            Color[] colors = cells[cell];

            for (int line = 0; line < Cell && colors.Length > 0; line++)
            {
                // Todos los colores de la celda en la misma línea: es lo que los ata entre sí.
                for (int x = 0; x < colors.Length; x++)
                    pixels[((top + line) * size.Width) + left + x] = Bgra(colors[x]);
            }
        }

        return (pixels, size);
    }

    private static int Bgra(Color color) =>
        unchecked((int)0xFF000000) | (color.R << 16) | (color.G << 8) | color.B;

    private static SheetAnalysis Analyse(
        (int[] Pixels, PixelSize Size) sheet, SheetSelection selection, int maxPlanes) =>
        SpriteSheetAnalysis.Analyse(sheet.Pixels, sheet.Size, Cell, Clear, selection, maxPlanes);

    // ------------------------------------------------------------------ los colores

    /// <summary>Saca los colores del rectángulo elegido, y sólo de ése.</summary>
    /// <remarks>
    /// Mirar la hoja entera contaría colores de celdas que no se van a traer, y como el tope
    /// son quince, eso tumbaría importaciones que sí caben.
    /// </remarks>
    [Fact]
    public void Solo_cuenta_los_colores_del_rectangulo_elegido()
    {
        (int[], PixelSize) sheet = Sheet(2, 1, [[White, Black], [Red, Blue]]);

        SheetAnalysis left = Analyse(sheet, new SheetSelection(0, 0, 1, 1), maxPlanes: 2);

        Assert.True(left.Ok, string.Join(" / ", left.Problems));
        Assert.Equal(2, left.Colors.Count);
        Assert.Contains(White, left.Colors);
        Assert.DoesNotContain(Red, left.Colors);
    }

    /// <summary>El color elegido como transparente no cuenta ni gasta plano.</summary>
    /// <remarks>
    /// Muchas hojas traen el fondo de un color liso en vez de con alfa. Sin esto, ese fondo se
    /// llevaría un índice de paleta y un plano entero para no pintar nada.
    /// </remarks>
    [Fact]
    public void El_color_transparente_no_cuenta()
    {
        (int[], PixelSize) sheet = Sheet(1, 1, [[White, Black]]);

        SheetAnalysis analysis = Analyse(sheet, new SheetSelection(0, 0, 1, 1), maxPlanes: 2);

        Assert.DoesNotContain(Clear, analysis.Colors);
        Assert.Equal(2, analysis.Colors.Count);
    }

    /// <summary>Más de quince colores no entran: el índice 0 es el transparente.</summary>
    [Fact]
    public void Mas_de_quince_colores_no_entran()
    {
        Color[] many = [.. Enumerable.Range(1, 16).Select(i => Color.FromRgb((byte)(i * 15), 0, 0))];

        (int[], PixelSize) sheet = Sheet(1, 1, [many]);

        SheetAnalysis analysis = Analyse(sheet, new SheetSelection(0, 0, 1, 1), maxPlanes: 4);

        Assert.False(analysis.Ok);
        Assert.Contains(analysis.Problems, problem => problem.Contains("16 colores"));
    }

    // ------------------------------------------------------------------ los planos

    /// <summary>Tres colores que van juntos en una celda salen a dos planos.</summary>
    [Fact]
    public void Tres_colores_en_una_celda_salen_a_dos_planos()
    {
        (int[], PixelSize) sheet = Sheet(1, 1, [[White, Black, Red]]);

        SheetAnalysis analysis = Analyse(sheet, new SheetSelection(0, 0, 1, 1), maxPlanes: 2);

        Assert.True(analysis.Ok, string.Join(" / ", analysis.Problems));
        Assert.Equal(2, analysis.Planes);
        Assert.Equal(2, analysis.Patterns);
    }

    /// <summary>
    /// Dos personajes que comparten colores no salen los dos a dos planos.
    /// </summary>
    /// <remarks>
    /// Es el hallazgo que cambia lo que se puede esperar de esto: la paleta es una sola, así
    /// que el reparto es de toda la selección. Blanco y negro compartidos con rojo en uno y
    /// azul en el otro obligan a un tercer plano, aunque ninguna celda pase de tres colores.
    /// </remarks>
    [Fact]
    public void Dos_personajes_que_comparten_colores_piden_un_plano_mas()
    {
        (int[], PixelSize) sheet = Sheet(2, 1, [[White, Black, Red], [White, Black, Blue]]);
        var whole = new SheetSelection(0, 0, 2, 1);

        SheetAnalysis tight = Analyse(sheet, whole, maxPlanes: 2);

        Assert.False(tight.Ok);
        Assert.Contains(tight.Problems, problem => problem.Contains("paleta es una sola"));

        SheetAnalysis roomy = Analyse(sheet, whole, maxPlanes: 3);

        Assert.True(roomy.Ok, string.Join(" / ", roomy.Problems));
        Assert.Equal(3, roomy.Planes);

        // Y por separado cada uno sí sale a dos: lo que ata es traerlos juntos.
        Assert.True(Analyse(sheet, new SheetSelection(0, 0, 1, 1), maxPlanes: 2).Ok);
        Assert.True(Analyse(sheet, new SheetSelection(1, 0, 1, 1), maxPlanes: 2).Ok);
    }

    // ------------------------------------------------------------------ por que no sale

    /// <summary>
    /// Cuando no sale, dice qué colores se atan y por cuál.
    /// </summary>
    /// <remarks>
    /// «Con 2 planos no salen estos 4 colores» deja adivinando cuál sobra. Lo que hace falta
    /// saber es que hay un color que coincide con todo —una sombra, un contorno— y que es el
    /// que está gastando el plano de más.
    /// </remarks>
    [Fact]
    public void Dice_que_colores_se_atan_y_por_cual()
    {
        // El negro sale con el rojo en un bicho y con el azul en otro: es el que ata.
        (int[], PixelSize) sheet = Sheet(2, 1, [[White, Black, Red], [White, Black, Blue]]);

        SheetAnalysis analysis = Analyse(sheet, new SheetSelection(0, 0, 2, 1), maxPlanes: 2);

        Assert.False(analysis.Ok);

        string why = Assert.Single(analysis.Problems);

        // Los cuatro que no caben juntos, cada uno por su color.
        Assert.Contains("#FF0000", why);
        Assert.Contains("#0000FF", why);
        Assert.Contains("#000000", why);
        Assert.Contains("#FFFFFF", why);

        // Y cuántos caben con ese tope, que es lo que dice si vale la pena subirlo.
        Assert.Contains("3 colores", why);
    }

    /// <summary>Y si es una línea la que se pasa ella sola, lo dice tal cual.</summary>
    /// <remarks>
    /// Se distingue del caso anterior porque se arregla de otra manera: aquí sobran colores en
    /// un sitio, y allí lo que sobra es que dos sitios compartan uno.
    /// </remarks>
    [Fact]
    public void Una_linea_que_se_pasa_ella_sola_se_dice_aparte()
    {
        (int[], PixelSize) sheet = Sheet(1, 1, [[White, Black, Red, Blue]]);

        SheetAnalysis analysis = Analyse(sheet, new SheetSelection(0, 0, 1, 1), maxPlanes: 2);

        Assert.False(analysis.Ok);

        string why = Assert.Single(analysis.Problems);

        Assert.Contains("En una misma línea coinciden 4", why);
        Assert.DoesNotContain("y en otra con otros", why);
    }

    // ------------------------------------------------------------------ lo que cuesta

    /// <summary>
    /// Los patrones son la suma de lo que pide cada celda, no las celdas por el máximo.
    /// </summary>
    /// <remarks>
    /// Una celda de dos colores no gasta tres huecos porque otra los necesite. Contarlo por el
    /// máximo daría por llena una selección que entra de sobra.
    /// </remarks>
    [Fact]
    public void Los_patrones_se_suman_celda_a_celda()
    {
        (int[], PixelSize) sheet = Sheet(2, 1, [[White, Black, Red], [White]]);

        SheetAnalysis analysis = Analyse(sheet, new SheetSelection(0, 0, 2, 1), maxPlanes: 3);

        Assert.True(analysis.Ok, string.Join(" / ", analysis.Problems));

        SheetCellPlan cheap = analysis.Cells.Single(cell => cell.Column == 1);

        Assert.Equal(1, cheap.Planes);
        Assert.Equal(analysis.Cells.Sum(cell => cell.Planes), analysis.Patterns);
        Assert.True(analysis.Patterns < analysis.Cells.Count * analysis.Planes);
    }

    /// <summary>
    /// Una selección que se pasa de los 64 huecos se sabe antes de tocar nada.
    /// </summary>
    /// <remarks>
    /// Es el tope que de verdad manda: una hoja cualquiera trae cientos de celdas y el banco
    /// tiene 64 huecos. Con tres planos, veintiuna celdas lo llenan.
    /// </remarks>
    [Fact]
    public void Se_sabe_si_entra_en_los_sesenta_y_cuatro_huecos()
    {
        Color[][] cells = [.. Enumerable.Range(0, 40).Select(_ => new[] { White, Black, Red })];

        (int[], PixelSize) sheet = Sheet(40, 1, cells);

        SheetAnalysis analysis = Analyse(sheet, new SheetSelection(0, 0, 40, 1), maxPlanes: 2);

        Assert.True(analysis.Ok, string.Join(" / ", analysis.Problems));
        Assert.Equal(80, analysis.Patterns);
        Assert.False(analysis.Fits);

        // Y la mitad de esas celdas sí entra.
        SheetAnalysis half = Analyse(sheet, new SheetSelection(0, 0, 20, 1), maxPlanes: 2);

        Assert.Equal(40, half.Patterns);
        Assert.True(half.Fits);
        Assert.True(half.Patterns <= SpriteBank.MaxSprites);
    }

    // ------------------------------------------------------------------ lo que no vale

    [Fact]
    public void Un_lado_de_celda_que_no_es_de_sprite_no_vale()
    {
        (int[] pixels, PixelSize size) = Sheet(1, 1, [[White]]);

        SheetAnalysis analysis = SpriteSheetAnalysis.Analyse(
            pixels, size, cellSize: 12, Clear, new SheetSelection(0, 0, 1, 1), maxPlanes: 2);

        Assert.False(analysis.Ok);
        Assert.Contains(analysis.Problems, problem => problem.Contains("12"));
    }

    [Fact]
    public void Un_rectangulo_que_se_sale_de_la_hoja_no_vale()
    {
        (int[], PixelSize) sheet = Sheet(2, 1, [[White], [Black]]);

        SheetAnalysis analysis = Analyse(sheet, new SheetSelection(1, 0, 2, 1), maxPlanes: 2);

        Assert.False(analysis.Ok);
        Assert.Contains(analysis.Problems, problem => problem.Contains("se sale"));
    }

    [Fact]
    public void Un_rectangulo_entero_transparente_no_vale()
    {
        (int[], PixelSize) sheet = Sheet(2, 1, [[White], []]);

        SheetAnalysis analysis = Analyse(sheet, new SheetSelection(1, 0, 1, 1), maxPlanes: 2);

        Assert.False(analysis.Ok);
        Assert.Contains(analysis.Problems, problem => problem.Contains("transparente"));
    }
}
