using System.Globalization;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Lee lo que captura <c>map_grabber.tcl</c> y lo cose en mapas.
/// </summary>
/// <remarks>
/// <para>
/// El fichero es una cabecera y una línea por pantalla: el sello del juego de tiles que había
/// puesto y los números de la tabla de nombres. El script sólo apunta lo que vio; el cosido,
/// que es donde están las decisiones, se hace aquí con <see cref="MapStitcher"/>.
/// </para>
/// <para>
/// <b>Se corta en varios mapas.</b> Cuando una pantalla no encaja con la anterior es que se ha
/// cambiado de sala, y cuando cambia el sello es que se ha cargado otro juego de tiles y los
/// mismos números ya no significan lo mismo. En los dos casos se cierra el mapa y se empieza
/// otro: pegarlos sería mezclar zonas que no se tocan.
/// </para>
/// </remarks>
public static class MapCapture
{
    /// <summary>Lo que el script escribe en la primera línea, por si el formato cambia.</summary>
    public const string Magic = "msxmap";

    /// <summary>Pantallas que hay que juntar para dar por bueno un mapa.</summary>
    /// <remarks>
    /// Pasar por una sala un instante deja un mapa de una pantalla que no dice nada y llena la
    /// lista de estorbo. Se guardan los que se han recorrido.
    /// </remarks>
    public const int LeastScreens = 2;

    /// <summary>Una pantalla capturada, con el juego de tiles que había puesto.</summary>
    public sealed record Screen(long Stamp, int[,] Cells);

    /// <summary>Lo que trae el fichero.</summary>
    public sealed record Capture(
        int Columns,
        int Rows,
        int Mode,
        int Names,
        int Patterns,
        int Colors,
        IReadOnlyList<Screen> Screens);

    public static Capture Read(string text)
    {
        var header = new Dictionary<string, int>();
        var screens = new List<Screen>();

        // Con el retorno de carro quitado: Tcl en Windows escribe CRLF, y partiendo solo
        // por el salto de linea el retorno se queda pegado al ultimo trozo de cada linea.
        // Entonces ni la marca de screens se reconoce ni los numeros de la cabecera se
        // dejan leer, y la captura sale vacia sin decir nada.
        string[] lines =
        [
            .. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim()),
        ];

        if (lines.Length == 0 || !lines[0].StartsWith(Magic, StringComparison.Ordinal))
            throw new FileFormatException("Esto no es una captura de pantallas: falta la cabecera.");

        int at = 1;

        for (; at < lines.Length; at++)
        {
            string[] parts = lines[at].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1 && parts[0] == "screens")
            {
                at++;
                break;
            }

            if (parts.Length == 2 && int.TryParse(parts[1], out int value))
                header[parts[0]] = value;
        }

        int columns = Header(header, "columns");
        int rows = Header(header, "rows");

        for (; at < lines.Length; at++)
        {
            string[] parts = lines[at].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                continue;

            // El sello por delante y las celdas detras, tal como lo escribe el script.
            if (parts.Length != (columns * rows) + 1)
            {
                throw new FileFormatException(
                    $"La pantalla de la línea {at + 1} trae {parts.Length - 1} celdas "
                    + $"y tendría que traer {columns * rows}.");
            }

            var cells = new int[columns, rows];

            for (int cell = 0; cell < columns * rows; cell++)
            {
                cells[cell % columns, cell / columns] =
                    int.Parse(parts[cell + 1], CultureInfo.InvariantCulture);
            }

            screens.Add(new Screen(long.Parse(parts[0], CultureInfo.InvariantCulture), cells));
        }

        return new Capture(
            columns,
            rows,
            header.GetValueOrDefault("mode", 2),
            header.GetValueOrDefault("names"),
            header.GetValueOrDefault("patterns"),
            header.GetValueOrDefault("colors"),
            screens);
    }

    /// <summary>
    /// Cose las pantallas en los mapas que salgan.
    /// </summary>
    /// <remarks>
    /// El nombre lleva el número porque de una captura salen varios y hay que distinguirlos;
    /// cuál es cuál se ve abriéndolos, que es más rápido que cualquier nombre que me invente.
    /// </remarks>
    public static IReadOnlyList<TileMap> Stitch(Capture capture, string name)
    {
        var maps = new List<TileMap>();

        MapStitcher stitcher = new();
        long stamp = capture.Screens.Count == 0 ? 0 : capture.Screens[0].Stamp;

        foreach (Screen screen in capture.Screens)
        {
            // Otro juego de tiles: los mismos numeros ya no dibujan lo mismo.
            bool cut = screen.Stamp != stamp || !stitcher.Feed(screen.Cells);

            if (!cut)
                continue;

            Keep(maps, stitcher, name);

            stitcher = new MapStitcher();
            stitcher.Feed(screen.Cells);
            stamp = screen.Stamp;
        }

        Keep(maps, stitcher, name);

        return maps;
    }

    private static void Keep(List<TileMap> maps, MapStitcher stitcher, string name)
    {
        if (stitcher.Screens < LeastScreens)
            return;

        int?[,] grid = stitcher.ToGrid();

        var map = new TileMap($"{name} {maps.Count + 1}", grid.GetLength(0), grid.GetLength(1));

        // Directo a la rejilla, como hace la importación de csv: aquí no hay nada que deshacer,
        // que el mapa se acaba de crear.
        TileGrid cells = map.Layers[0].Grid;

        for (int column = 0; column < grid.GetLength(0); column++)
        {
            for (int row = 0; row < grid.GetLength(1); row++)
                cells[column, row] = grid[column, row];
        }

        maps.Add(map);
    }

    private static int Header(Dictionary<string, int> header, string key) =>
        header.TryGetValue(key, out int value) && value > 0
            ? value
            : throw new FileFormatException($"A la captura le falta «{key}» en la cabecera.");
}
