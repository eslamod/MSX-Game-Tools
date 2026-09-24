using System.Text;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Una pantalla ya recortada del mapa.
/// </summary>
/// <param name="Column">En qué columna y fila de pantallas cae, contando desde cero.</param>
/// <param name="Map">
/// El trozo, siempre de una pantalla entera: la del borde se queda corta en el mapa y las
/// celdas que faltan vienen vacías, que es lo que el exportador escribe como relleno.
/// </param>
/// <param name="Padded">Si es una de las del borde, a la que el mapa no llega entero.</param>
public sealed record MapScreen(int Column, int Row, TileMap Map, bool Padded)
{
    /// <summary>
    /// Por qué celda del mapa empieza, que es lo que dice de dónde salió.
    /// </summary>
    /// <remarks>
    /// Sale del número de pantalla y de lo que mide el recorte, que es siempre una pantalla
    /// entera: así no hay dos sitios donde apuntar lo mismo.
    /// </remarks>
    public int Left => Column * Map.Width;

    /// <inheritdoc cref="Left"/>
    public int Top => Row * Map.Height;
}

/// <summary>
/// Parte un mapa en las pantallas de las que está hecho.
/// </summary>
/// <remarks>
/// <para>
/// Para los juegos de pantallas fijas, que se dibujan de una sola vez —un mapa entero, con sus
/// pantallas pegadas— y luego se cargan de una en una. Aquí se corta por donde el editor pinta
/// la rejilla, y cada trozo sale como un mapa suyo para que lo escriba el mismo exportador.
/// </para>
/// <para>
/// En celdas y no en tiles: en un mapa de supertiles una celda son varios tiles, y lo que el
/// fichero lleva son los números de las celdas. Quien llame aquí ya ha hecho esa cuenta.
/// </para>
/// </remarks>
public static class MapScreens
{
    /// <summary>What goes after the stem in the name of the index, the table of where each screen starts.</summary>
    public const string IndexSuffix = "_screens";

    /// <summary>And in the name of the file that brings the screens in, next to the table.</summary>
    public const string DataSuffix = "_screens_data";

    /// <summary>
    /// The two of them are assembler even when the screens go out in bytes.
    /// </summary>
    /// <remarks>
    /// A table of pointers needs the assembler to work out where each screen landed, the same
    /// way the example ROM is assembler whatever the format of what it brings in.
    /// </remarks>
    public const string IndexExtension = ".asm";

    /// <summary>
    /// Entries per line of the table.
    /// </summary>
    /// <remarks>
    /// A row of screens is as long as the map allows, and a line with thirty labels on it is a
    /// line nobody reads and not every assembler is sure to take whole. A row that does not fit
    /// goes on in the next line: the table is read in order, and the lines do not count.
    /// </remarks>
    private const int EntriesPerLine = 8;
    /// <summary>Cuántas pantallas de ese lado hacen falta para cubrir el mapa.</summary>
    /// <remarks>
    /// Redondeando hacia arriba: media pantalla al borde sigue siendo una pantalla, y lo que
    /// falta se rellena. Un mapa de 70 celdas con pantallas de 32 son tres, no dos.
    /// </remarks>
    public static int Count(int cells, int screen) => screen > 0 ? ((cells + screen - 1) / screen) : 0;

    /// <summary>Si el mapa se queda corto y hay que rellenar la última pantalla de algún lado.</summary>
    public static bool Pads(TileMap map, int wide, int high) =>
        wide > 0 && high > 0 && (map.Width % wide != 0 || map.Height % high != 0);

    /// <summary>
    /// Las pantallas que llevan algo dibujado, en orden de lectura.
    /// </summary>
    /// <remarks>
    /// Las vacías no salen: en un mapa de pantallas fijas lo normal es que el rectángulo no esté
    /// entero —una L, una cruz, un castillo con sus alas— y un fichero de 768 ceros por cada
    /// hueco del dibujo no es un mapa, es sitio gastado. Las que sí salen conservan su número,
    /// así que saltarse una no corre a las demás.
    /// </remarks>
    /// <summary>
    /// La pantalla que cae en esa columna y esa fila, esté vacía o no, o nada si ahí no hay
    /// pantalla.
    /// </summary>
    /// <remarks>
    /// Aquí no se salta ninguna: pedir una pantalla por su número es pedir ésa, y una vacía a
    /// propósito —un sótano que el juego rellena al entrar— está tan pedida como las demás.
    /// </remarks>
    /// <param name="column">Columna y fila de pantallas, contando desde cero.</param>
    public static MapScreen? At(TileMap map, int wide, int high, string stem, int column, int row)
    {
        bool inside = column >= 0 && row >= 0
                      && column < Count(map.Width, wide)
                      && row < Count(map.Height, high);

        return wide > 0 && high > 0 && inside
            ? Cut(map.Flatten(), map, wide, high, stem, column, row)
            : null;
    }

    /// <param name="stem">De dónde sale el nombre de cada trozo, que es el del fichero.</param>
    public static IReadOnlyList<MapScreen> Of(TileMap map, int wide, int high, string stem)
    {
        var screens = new List<MapScreen>();

        if (wide <= 0 || high <= 0)
            return screens;

        // Una vez y no una por pantalla: aplastar es recorrer el mapa entero por cada capa, y
        // eso multiplicado por cien pantallas se nota.
        TileGrid flat = map.Flatten();

        int columns = Count(map.Width, wide);
        int rows = Count(map.Height, high);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                MapScreen screen = Cut(flat, map, wide, high, stem, column, row);

                if (!screen.Map.Layers[0].Grid.IsEmpty)
                    screens.Add(screen);
            }
        }

        return screens;
    }

    /// <summary>Una pantalla recortada del mapa ya aplastado.</summary>
    private static MapScreen Cut(
        TileGrid flat, TileMap map, int wide, int high, string stem, int column, int row)
    {
        int left = column * wide;
        int top = row * high;

        var cut = new TileMap($"{stem}_{column + 1}_{row + 1}", wide, high)
        {
            EmptyTile = map.EmptyTile,
        };

        TileGrid grid = cut.Layers[0].Grid;

        for (int y = 0; y < high; y++)
        {
            // Fuera del mapa la rejilla devuelve vacío, así que la pantalla del borde se
            // rellena sola sin tener que mirar dónde acaba.
            for (int x = 0; x < wide; x++)
                grid[x, y] = flat[left + x, top + y];
        }

        return new MapScreen(
            column, row, cut, left + wide > map.Width || top + high > map.Height);
    }

    /// <summary>
    /// The label a screen goes by, which is the one its own file defines.
    /// </summary>
    /// <remarks>
    /// The assembler file of a screen writes it itself, from the same name; the index and the
    /// file that brings the binary ones in have to spell it exactly the same, or the table points
    /// at a label nobody defines.
    /// </remarks>
    public static string Label(MapScreen screen) => $"{AsmLabel.Of(screen.Map.Name)}_map";

    /// <summary>
    /// The table of where each screen starts, row by row, with 0 for the ones not written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pointers by label and not offsets: the assembler works out where each screen landed, and
    /// the day the screens go out compressed the table stays the same, pointing at the
    /// compressed data. With offsets it would have to be redone, since then every screen would
    /// measure something different.
    /// </para>
    /// <para>
    /// Zero for the empty ones because no screen can start at 0x0000, so the game can tell there
    /// is no room there by testing the pointer.
    /// </para>
    /// <para>
    /// Row by row because that is the order of reading and of the list of files, and the entry
    /// comes straight out of the column and the row, which is how a game of fixed screens
    /// usually keeps the room it is in.
    /// </para>
    /// </remarks>
    /// <param name="screens">The ones that were written, which are the ones that get a label.</param>
    /// <param name="header">Whether they went out with the size in front, which is said at the top.</param>
    public static string Index(
        TileMap map,
        IReadOnlyList<MapScreen> screens,
        int wide,
        int high,
        string stem,
        AsmStyle style,
        bool header)
    {
        int columns = Count(map.Width, wide);
        int rows = Count(map.Height, high);
        int bytes = (wide * high) + (header ? MapExporter.HeaderBytes : 0);

        string name = AsmLabel.Of(stem);
        string constant = name.ToUpperInvariant();

        Dictionary<(int Column, int Row), string> labels =
            screens.ToDictionary(screen => (screen.Column, screen.Row), Label);

        var text = new StringBuilder();

        text.AppendLine($"; Screen index - {map.Name}");
        text.AppendLine(
            $"; {columns}x{rows} screens of {wide}x{high} cells, {bytes} bytes each"
            + (header ? ", the size in front included." : "."));
        text.AppendLine(
            $"; Row by row: screen C-R is entry (R-1)*{columns} + (C-1), counting from 1 like the files.");
        text.AppendLine("; Empty screens were not written and are 0 here.");
        text.AppendLine($"; The labels come from {name}{DataSuffix}{IndexExtension}, which brings the screens in.");
        text.AppendLine();
        text.AppendLine($"{constant}_SCREENS_WIDE {style.Directive("equ")} {columns}");
        text.AppendLine($"{constant}_SCREENS_HIGH {style.Directive("equ")} {rows}");
        text.AppendLine();
        text.AppendLine($"{name}{IndexSuffix}:");

        for (int row = 0; row < rows; row++)
        {
            string[] entries =
            [
                .. Enumerable.Range(0, columns)
                    .Select(column => labels.GetValueOrDefault((column, row), "0")),
            ];

            for (int start = 0; start < entries.Length; start += EntriesPerLine)
            {
                string line = $"    {style.Directive("dw")} "
                              + string.Join(", ", entries.Skip(start).Take(EntriesPerLine));

                text.AppendLine(start == 0 ? $"{line}    ; row {row + 1}" : line);
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// The screens themselves, one after another, each under the label the table points at.
    /// </summary>
    /// <remarks>
    /// Apart from the table on purpose. In a cartridge of 32K both go in and that is all; in a
    /// megaROM the screens are spread over pages and cannot all go in one place, and then the
    /// table stays with the code and each screen here goes to its page.
    /// </remarks>
    /// <param name="binary">
    /// Whether the screens went out in bytes. Those need their label written here; the assembler
    /// ones carry it inside.
    /// </param>
    public static string Data(
        TileMap map, IReadOnlyList<MapScreen> screens, string stem, AsmStyle style, bool binary)
    {
        var text = new StringBuilder();

        text.AppendLine($"; Screens of {map.Name}, the ones {AsmLabel.Of(stem)}{IndexSuffix}{IndexExtension} points at.");
        text.AppendLine("; In a megaROM, move each one to the page where it has to go.");
        text.AppendLine();

        foreach (MapScreen screen in screens)
        {
            string file = $"{stem}_{screen.Column + 1}_{screen.Row + 1}";

            if (binary)
            {
                text.AppendLine($"{Label(screen)}:");
                text.AppendLine($"    {style.Directive("incbin")} \"{file}.bin\"");
            }
            else
            {
                text.AppendLine($"    {style.Directive("include")} \"{file}.asm\"");
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Las líneas de comentario que dicen de dónde salió esta pantalla.
    /// </summary>
    /// <remarks>
    /// El fichero de una pantalla suelta no dice de qué mapa es ni por dónde iba, y con veinte
    /// en la misma carpeta el nombre es lo único que queda. Esto lo deja escrito dentro.
    /// </remarks>
    public static IReadOnlyList<string> Notes(MapScreen screen, TileMap map)
    {
        int right = screen.Left + screen.Map.Width - 1;
        int bottom = screen.Top + screen.Map.Height - 1;

        var notes = new List<string>
        {
            $"; Screen {screen.Column + 1}-{screen.Row + 1} of {map.Name}"
            + $" - map columns {screen.Left}-{right}, rows {screen.Top}-{bottom}",
        };

        if (screen.Padded)
        {
            notes.Add(
                $"; The map ends at column {map.Width - 1}, row {map.Height - 1}:"
                + $" the rest of this screen is tile {map.EmptyTile}.");
        }

        return notes;
    }
}
