using Avalonia;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Traer dibujos a un juego de screen 1, de otro juego o de un png.
/// </summary>
/// <remarks>
/// <para>
/// Las dos entradas se tratan al revés a propósito. Estampar mueve un puñado de tiles a mano,
/// se ve al momento y se puede deshacer: ahí se aproxima y se sigue. Importar un png trae una
/// hoja entera de fuera, no hay deshacer y el resultado no se mira tile a tile: ahí se rechaza
/// y se dice qué franja de la imagen hay que retocar.
/// </para>
/// <para>
/// El png tiene además una ventaja que no tiene GRAPHIC 2: si un grupo trae exactamente dos
/// colores, esos dos <b>son</b> su par, así que la imagen rellena la tabla de colores sola. En
/// GRAPHIC 2 hay que decidir por cada línea cuál de los dos es el frente, y de ahí viene que un
/// mismo tile salga con unas líneas escritas de una forma y otras de la otra.
/// </para>
/// </remarks>
public class Graphic1CrossModeTests
{
    private static TileSet Graphic1(string name = "Cueva") =>
        new(name, TileSet.GraphicMode.Graphic1);

    /// <summary>Pinta el tile entero con un patrón reconocible y unos colores concretos.</summary>
    private static void Draw(TileSet tileSet, int index, byte pattern, int fore, int back)
    {
        foreach (TileRow row in tileSet.ListOfTiles[index].ArrayTileRows)
        {
            row.ForeColor = fore;
            row.BackColor = back;

            for (int column = 0; column < TileRow.Columns; column++)
                row.ArrayPattern[column] = (pattern & (1 << column)) != 0;
        }
    }

    // ------------------------------------------------------------------ estampar

    /// <summary>
    /// Un grupo sin estrenar se queda con el color de lo que le llega.
    /// </summary>
    /// <remarks>
    /// Es lo que hace que traerse un trozo de otro juego a uno nuevo no salga en blanco y
    /// negro. Sin esto había que reelegir los 32 pares a mano después de cada importación.
    /// </remarks>
    [Fact]
    public void Un_grupo_sin_estrenar_se_queda_con_el_color_que_le_llega()
    {
        var origin = new TileSet("Bosque");
        Draw(origin, 0, 0b1010_1010, fore: 2, back: 3);

        TileSet target = Graphic1();

        target.Stamp(8, 0, origin.Copy(0, 0, 1, 1));

        Assert.Equal(2, target.ColorGroups[1].ForeColor);
        Assert.Equal(3, target.ColorGroups[1].BackColor);

        // Y el dibujo llega entero, que es lo que de verdad cuesta hacer.
        Assert.Equal(
            origin.ListOfTiles[0].ArrayTileRows[0].PatternByte,
            target.ListOfTiles[8].ArrayTileRows[0].PatternByte);
    }

    /// <summary>
    /// Un grupo al que ya le has elegido el color no se toca, aunque no tenga nada dibujado.
    /// </summary>
    /// <remarks>
    /// Primero eliges los dos colores de la franja y después traes los dibujos: es el orden
    /// natural de trabajo, y mirar sólo si hay dibujo daba ese grupo por sin estrenar y pisaba
    /// una decisión deliberada.
    /// </remarks>
    [Fact]
    public void Un_grupo_con_el_color_ya_elegido_no_se_toca()
    {
        var origin = new TileSet("Bosque");
        Draw(origin, 0, 0b1010_1010, fore: 2, back: 3);

        TileSet target = Graphic1();
        target.ColorGroups[1].Set(foreColor: 6, backColor: 7);

        target.Stamp(8, 0, origin.Copy(0, 0, 1, 1));

        Assert.Equal(6, target.ColorGroups[1].ForeColor);
        Assert.Equal(7, target.ColorGroups[1].BackColor);
        Assert.Equal(6, target.ListOfTiles[8].ArrayTileRows[0].ForeColor);
    }

    /// <summary>Y uno que ya tiene dibujos, tampoco: recolorearlo cambiaría hasta ocho tiles.</summary>
    [Fact]
    public void Un_grupo_que_ya_se_usa_no_se_recolorea()
    {
        var origin = new TileSet("Bosque");
        Draw(origin, 0, 0b1010_1010, fore: 2, back: 3);

        TileSet target = Graphic1();

        // El grupo 1 ya tiene un dibujo suyo, con el par de partida.
        target.ListOfTiles[9].ArrayTileRows[0].ArrayPattern[0] = true;

        target.Stamp(8, 0, origin.Copy(0, 0, 1, 1));

        Assert.Equal(15, target.ColorGroups[1].ForeColor);
        Assert.Equal(0, target.ColorGroups[1].BackColor);
    }

    /// <summary>
    /// Del par que llega se coge el que más se repite, no el de la primera línea.
    /// </summary>
    /// <remarks>
    /// Un dibujo suele tener un par dominante y alguna línea suelta con otro; quedarse con el
    /// de la primera línea daría el color de una esquina.
    /// </remarks>
    [Fact]
    public void Del_trozo_que_llega_manda_el_par_que_mas_se_repite()
    {
        var origin = new TileSet("Bosque");
        Draw(origin, 0, 0b1010_1010, fore: 2, back: 3);

        // Sólo la primera línea va de otro color.
        origin.ListOfTiles[0].ArrayTileRows[0].ForeColor = 9;
        origin.ListOfTiles[0].ArrayTileRows[0].BackColor = 10;

        TileSet target = Graphic1();

        target.Stamp(8, 0, origin.Copy(0, 0, 1, 1));

        Assert.Equal(2, target.ColorGroups[1].ForeColor);
        Assert.Equal(3, target.ColorGroups[1].BackColor);
    }

    /// <summary>De screen 1 a screen 2 no se pierde nada: allí cabe todo.</summary>
    [Fact]
    public void De_screen_1_a_screen_2_llega_dibujo_y_color()
    {
        TileSet origin = Graphic1("Mazmorra");
        origin.ColorGroups[0].Set(foreColor: 4, backColor: 12);
        origin.ListOfTiles[0].ArrayTileRows[0].ArrayPattern[0] = true;

        var target = new TileSet("Bosque");

        target.Stamp(5, 0, origin.Copy(0, 0, 1, 1));

        Assert.Equal(4, target.ListOfTiles[5].ArrayTileRows[0].ForeColor);
        Assert.Equal(12, target.ListOfTiles[5].ArrayTileRows[0].BackColor);
        Assert.True(target.ListOfTiles[5].ArrayTileRows[0].ArrayPattern[0]);
    }

    // ------------------------------------------------------------------ el png

    /// <summary>Una imagen de dos colores por franja entra, y además rellena la tabla.</summary>
    [Fact]
    public void El_png_rellena_la_tabla_de_colores()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        // Los ocho primeros tiles con dos colores y el resto de uno. Uno de cada cuatro
        // pixeles y no uno de cada dos: con la mitad justa de cada color hay empate y no se
        // estaría comprobando la regla de cuál es el fondo, sino cuál se vio primero.
        int[] pixels = Sheet(palette, (tile, x, _) =>
            tile < TileSet.ColorGroupSize && x % 4 == 0 ? 4 : 12);

        TileSetImportResult result = Analyse(pixels, palette);

        Assert.True(result.Ok, Describe(result));

        TileSet imported = result.TileSet!;

        // El 12 ocupa más pixeles, así que es el fondo y el 4 el trazo.
        Assert.Equal(4, imported.ColorGroups[0].ForeColor);
        Assert.Equal(12, imported.ColorGroups[0].BackColor);

        // Y los bits salen donde estaba el color de frente.
        Assert.True(imported.ListOfTiles[0].ArrayTileRows[0].ArrayPattern[0]);
        Assert.False(imported.ListOfTiles[0].ArrayTileRows[0].ArrayPattern[1]);
    }

    /// <summary>
    /// Tres colores en una franja de ocho tiles no entran, y se dice cuál es la franja.
    /// </summary>
    /// <remarks>
    /// Se rechaza en vez de reducir: una hoja entera viene de fuera y una reducción silenciosa
    /// estropearía el trabajo de horas sin decir dónde. El mensaje dice qué tiles retocar.
    /// </remarks>
    [Fact]
    public void Tres_colores_en_una_franja_no_entran_y_dice_cual()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        // La franja de los tiles 8-15 trae tres colores; la primera sólo dos.
        int[] pixels = Sheet(palette, (tile, x, _) =>
        {
            if (tile < TileSet.ColorGroupSize)
                return x % 2 == 0 ? 4 : 12;

            return x % 3 == 0 ? 4 : (x % 3 == 1 ? 12 : 6);
        });

        TileSetImportResult result = Analyse(pixels, palette);

        Assert.False(result.Ok);
        Assert.Contains(result.Problems, problem => problem.Message.Contains("8-15"));
        Assert.DoesNotContain(result.Problems, problem => problem.Message.Contains("0-7"));
    }

    /// <summary>
    /// La misma imagen entra sin problema como screen 2.
    /// </summary>
    /// <remarks>
    /// Es lo que mide cuánto más dura es la regla: tres colores repartidos en 512 pixeles no
    /// caben en GRAPHIC 1 y en GRAPHIC 2 sí, porque allí el límite es por línea de ocho.
    /// </remarks>
    [Fact]
    public void Esa_misma_imagen_entra_como_screen_2()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        // Cada tile de un color liso, y tres colores distintos repartidos entre los tiles de
        // la segunda franja. Por línea nunca hay más de uno, así que en GRAPHIC 2 entra sin
        // problema; en GRAPHIC 1 esa franja suma tres y no cabe.
        int[] pixels = Sheet(palette, (tile, _, _) =>
        {
            if (tile < TileSet.ColorGroupSize)
                return 4;

            return (tile % 3) switch { 0 => 4, 1 => 12, _ => 6 };
        });

        Assert.False(Analyse(pixels, palette).Ok);
        Assert.True(Analyse(pixels, palette, TileSet.GraphicMode.Graphic2).Ok);
    }

    /// <summary>Un juego de screen 1 exportado a png vuelve a entrar igual.</summary>
    [Fact]
    public void Ida_y_vuelta_por_png_devuelve_los_mismos_colores()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        TileSet origin = Graphic1();
        origin.ColorGroups[0].Set(foreColor: 4, backColor: 12);
        origin.ListOfTiles[0].ArrayTileRows[0].ArrayPattern[0] = true;
        origin.ListOfTiles[0].ArrayTileRows[0].ArrayPattern[1] = true;

        int[] pixels = TileSetPngConverter.ToPixels(origin, palette);

        TileSetImportResult result = Analyse(pixels, palette);

        Assert.True(result.Ok, Describe(result));

        TileSet read = result.TileSet!;

        Assert.Equal(4, read.ColorGroups[0].ForeColor);
        Assert.Equal(12, read.ColorGroups[0].BackColor);
        Assert.Equal(
            origin.ListOfTiles[0].ArrayTileRows[0].PatternByte,
            read.ListOfTiles[0].ArrayTileRows[0].PatternByte);
    }

    private static TileSetImportResult Analyse(
        int[] pixels,
        ColorPalette palette,
        TileSet.GraphicMode mode = TileSet.GraphicMode.Graphic1) =>
        TileSetPngConverter.Analyse(pixels, TileSetPngConverter.FullSize, palette, "Traido", mode);

    private static string Describe(TileSetImportResult result) =>
        string.Join(" / ", result.Problems.Select(problem => problem.Message));

    /// <summary>Una hoja entera de 256 tiles, con el color de cada pixel puesto a mano.</summary>
    private static int[] Sheet(ColorPalette palette, Func<int, int, int, int> colorOf)
    {
        PixelSize size = TileSetPngConverter.FullSize;
        int[] pixels = new int[size.Width * size.Height];

        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                int tile = ((y / Tile.Rows) * TileSet.Columns) + (x / TileRow.Columns);

                Avalonia.Media.Color color = palette.GetColor(
                    colorOf(tile, x % TileRow.Columns, y % Tile.Rows));

                pixels[(y * size.Width) + x] =
                    unchecked((int)0xFF000000) | (color.R << 16) | (color.G << 8) | color.B;
            }
        }

        return pixels;
    }
}
