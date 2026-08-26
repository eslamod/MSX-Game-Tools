using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Lo que captura el script de openMSX, leído y cosido.
/// </summary>
/// <remarks>
/// Las capturas de estas pruebas se escriben con el mismo formato que emite
/// <c>map_grabber.tcl</c>: cabecera, la palabra <c>screens</c>, y una línea por pantalla con
/// el sello del juego de tiles por delante.
/// </remarks>
public class MapCaptureTests
{
    private const int Columns = 8;
    private const int Rows = 4;

    /// <summary>Una captura de una zona que se recorre sale como un mapa más ancho.</summary>
    [Fact]
    public void Una_zona_recorrida_sale_como_un_mapa()
    {
        int[,] world = World(16, Rows);

        string text = Written(1, Cuts(world, 0, 1, 2, 3));

        TileMap map = Assert.Single(MapCapture.Stitch(MapCapture.Read(text), "Zona"));

        Assert.Equal(Columns + 3, map.Width);
        Assert.Equal(Rows, map.Height);

        for (int column = 0; column < map.Width; column++)
        {
            for (int row = 0; row < Rows; row++)
                Assert.Equal(world[column, row], map.Layers[0].Grid[column, row]);
        }
    }

    /// <summary>
    /// Cambiar el sello del juego de tiles no corta el mapa.
    /// </summary>
    /// <remarks>
    /// Al principio sí cortaba, y estaba mal. El sello cambia con cualquier tile animado y con
    /// los patrones que muchos juegos redefinen para el scroll suave: cortaba cada pocos
    /// fotogramas y una partida salía en miles de trozos de dos pantallas. Lo que de verdad
    /// dice que se ha cambiado de sitio es que la pantalla no encaje, y eso ya se mira.
    /// </remarks>
    [Fact]
    public void Cambiar_de_juego_de_tiles_no_corta_el_mapa()
    {
        int[,] world = World(16, Rows);

        // Las mismas pantallas, encajando todas, pero con el sello cambiando a cada una.
        string text = Written(
            [(1, Cut(world, 0)), (2, Cut(world, 1)), (3, Cut(world, 2)), (4, Cut(world, 3))]);

        TileMap map = Assert.Single(MapCapture.Stitch(MapCapture.Read(text), "Zona"));

        Assert.Equal(Columns + 3, map.Width);
    }

    /// <summary>Y cambiar de sala también, aunque el juego de tiles siga siendo el mismo.</summary>
    [Fact]
    public void Cambiar_de_sala_corta_el_mapa()
    {
        int[,] one = World(16, Rows);
        int[,] other = World(16, Rows, seed: 4242);

        string text = Written(
            [(1, Cut(one, 0)), (1, Cut(one, 1)), (1, Cut(other, 0)), (1, Cut(other, 1))]);

        Assert.Equal(2, MapCapture.Stitch(MapCapture.Read(text), "Zona").Count);
    }

    /// <summary>
    /// Una sala por la que sólo se pasó un momento no deja mapa.
    /// </summary>
    /// <remarks>
    /// Un mapa de una sola pantalla no dice nada y llena la lista de estorbo. Se guarda lo que
    /// se ha recorrido.
    /// </remarks>
    [Fact]
    public void Una_sala_de_paso_no_deja_mapa()
    {
        int[,] one = World(16, Rows);
        int[,] other = World(16, Rows, seed: 555);

        string text = Written(
            [(1, Cut(one, 0)), (1, Cut(other, 0)), (1, Cut(one, 0)), (1, Cut(one, 1))]);

        // La del medio se queda en una pantalla suelta; la primera tambien.
        TileMap map = Assert.Single(MapCapture.Stitch(MapCapture.Read(text), "Zona"));

        Assert.Equal(Columns + 1, map.Width);
    }

    /// <summary>Un fichero que no es una captura se rechaza diciendo por qué.</summary>
    [Fact]
    public void Un_fichero_que_no_es_una_captura_se_rechaza()
    {
        Assert.Throws<FileFormatException>(() => MapCapture.Read("hola\nqué tal\n"));
    }

    /// <summary>Y una pantalla con celdas de menos, también.</summary>
    /// <remarks>
    /// Rellenarla o recortarla sería peor: saldría un mapa con pinta de bueno y desplazado a
    /// partir de ahí, sin nada que lo delatara.
    /// </remarks>
    [Fact]
    public void Una_pantalla_a_medias_se_rechaza()
    {
        string text = "msxmap 1\ncolumns 8\nrows 4\nscreens\n7 1 2 3\n";

        FileFormatException failed =
            Assert.Throws<FileFormatException>(() => MapCapture.Read(text));

        Assert.Contains("32", failed.Message);
    }

    // ------------------------------------------------------------------ los andamios

    private static int[,] World(int columns, int rows, int seed = 99)
    {
        var world = new int[columns, rows];
        var random = new Random(seed);

        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows; row++)
                world[column, row] = random.Next(1, 256);
        }

        return world;
    }

    private static int[,] Cut(int[,] world, int at)
    {
        var screen = new int[Columns, Rows];

        for (int column = 0; column < Columns; column++)
        {
            for (int row = 0; row < Rows; row++)
                screen[column, row] = world[at + column, row];
        }

        return screen;
    }

    private static (long Stamp, int[,] Cells)[] Cuts(int[,] world, params int[] at) =>
        [.. at.Select(one => ((long)1, Cut(world, one)))];

    private static string Written(long stamp, (long Stamp, int[,] Cells)[] screens) =>
        Written(screens);

    /// <summary>El mismo formato que escribe el script.</summary>
    private static string Written((long Stamp, int[,] Cells)[] screens)
    {
        var text = new System.Text.StringBuilder();

        text.AppendLine("msxmap 1");
        text.AppendLine($"columns {Columns}");
        text.AppendLine($"rows {Rows}");
        text.AppendLine("mode 2");
        text.AppendLine("names 6144");
        text.AppendLine("patterns 0");
        text.AppendLine("colors 8192");
        text.AppendLine("screens");

        foreach ((long stamp, int[,] cells) in screens)
        {
            var numbers = new List<string> { stamp.ToString() };

            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                    numbers.Add(cells[column, row].ToString());
            }

            text.AppendLine(string.Join(' ', numbers));
        }

        return text.ToString();
    }
}
