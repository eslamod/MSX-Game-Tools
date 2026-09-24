using System.Text;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// A screen already cut out of the map.
/// </summary>
/// <param name="Column">Which column and row of screens it falls in, counting from zero.</param>
/// <param name="Map">
/// The piece, always a whole screen: the one at the edge falls short of the map, and the cells
/// that are missing come empty, which is what the exporter writes as filler.
/// </param>
/// <param name="Padded">Whether it is one of the edge, which the map does not fill whole.</param>
public sealed record MapScreen(int Column, int Row, TileMap Map, bool Padded)
{
    /// <summary>
    /// Which cell of the map it starts at, which is what says where it came from.
    /// </summary>
    /// <remarks>
    /// Worked out from the number of the screen and the size of the piece, which is always a
    /// whole screen: that way there are not two places keeping the same thing.
    /// </remarks>
    public int Left => Column * Map.Width;

    /// <inheritdoc cref="Left"/>
    public int Top => Row * Map.Height;
}

/// <summary>
/// Cuts a map into the screens it is made of.
/// </summary>
/// <remarks>
/// <para>
/// For the games of fixed screens, which are drawn in one go —a whole map, with its screens
/// side by side— and then loaded one at a time. The map is cut where the editor draws the
/// grid, and each piece comes out as a map of its own so that the same exporter writes it.
/// </para>
/// <para>
/// In cells and not in tiles: in a map of super tiles a cell is several tiles, and what the
/// file carries is the numbers of the cells. Whoever calls here has already done that sum.
/// </para>
/// </remarks>
public static class MapScreens
{
    /// <summary>What goes after the stem in the name of the index, the table of where each screen starts.</summary>
    public const string IndexSuffix = "_screens";

    /// <summary>And in the name of the file that brings the screens in, next to the table.</summary>
    public const string DataSuffix = "_screens_data";

    /// <summary>
    /// And in the name of each page of screens, when they go out shared into the pages of a
    /// mapper: the number follows.
    /// </summary>
    public const string PageSuffix = "_screens_page_";

    /// <summary>What the page table says of a screen that was not written.</summary>
    /// <remarks>
    /// In the page and not in the offset: an offset of 0 is the first screen of any page. And no
    /// game has 255 pages of screens, which would be two megabytes of them.
    /// </remarks>
    public const byte NoPage = 0xFF;

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

    /// <summary>How many screens it takes along that side to cover the map.</summary>
    /// <remarks>
    /// Rounding up: half a screen at the edge is still a screen, and what is missing gets
    /// filled. A map of 70 cells with screens of 32 is three of them, not two.
    /// </remarks>
    public static int Count(int cells, int screen) => screen > 0 ? ((cells + screen - 1) / screen) : 0;

    /// <summary>Whether the map falls short and the last screen along some side has to be filled.</summary>
    public static bool Pads(TileMap map, int wide, int high) =>
        wide > 0 && high > 0 && (map.Width % wide != 0 || map.Height % high != 0);

    /// <summary>
    /// The screen that falls in that column and that row, empty or not, or nothing if there is
    /// no screen there.
    /// </summary>
    /// <remarks>
    /// None is skipped here: asking for a screen by its number is asking for that one, and one
    /// left empty on purpose —a cellar the game fills in on entering— is as asked for as the
    /// others.
    /// </remarks>
    /// <param name="column">Column and row of screens, counting from zero.</param>
    public static MapScreen? At(TileMap map, int wide, int high, string stem, int column, int row)
    {
        bool inside = column >= 0 && row >= 0
                      && column < Count(map.Width, wide)
                      && row < Count(map.Height, high);

        return wide > 0 && high > 0 && inside
            ? Cut(map.Flatten(), map, wide, high, stem, column, row)
            : null;
    }

    /// <summary>
    /// The screens that have something drawn, in reading order.
    /// </summary>
    /// <remarks>
    /// The empty ones do not go out: in a map of fixed screens the rectangle is usually not
    /// whole —an L, a cross, a castle with its wings— and a file of 768 zeros for every hole in
    /// the drawing is not a map, it is wasted room. The ones that do go out keep their number,
    /// so skipping one does not shift the others.
    /// </remarks>
    /// <param name="stem">Where the name of each piece comes from, which is the name of the file.</param>
    public static IReadOnlyList<MapScreen> Of(TileMap map, int wide, int high, string stem)
    {
        var screens = new List<MapScreen>();

        if (wide <= 0 || high <= 0)
            return screens;

        // Once and not once per screen: flattening goes over the whole map for every layer, and
        // that times a hundred screens shows.
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

    /// <summary>A screen cut out of the map already flattened.</summary>
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
            // Outside the map the grid gives back empty, so the screen at the edge fills itself
            // without having to look for where the map ends.
            for (int x = 0; x < wide; x++)
                grid[x, y] = flat[left + x, top + y];
        }

        return new MapScreen(
            column, row, cut, left + wide > map.Width || top + high > map.Height);
    }

    /// <summary>The bytes a screen takes in its file: its cells, and the size in front if it goes.</summary>
    public static int Bytes(int wide, int high, bool header) =>
        (wide * high) + (header ? MapExporter.HeaderBytes : 0);

    /// <summary>
    /// The screens shared out into pages of that size, in order, none of them cut in two.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In a megaROM a screen has to be whole inside the segment that gets mapped: half of it in
    /// the next one would need both mapped at once. So a page is closed when the next screen
    /// does not fit, even with room left over.
    /// </para>
    /// <para>
    /// In order, so that the pages keep the order of the files and of the table. And adding up
    /// sizes rather than dividing, although today every screen measures the same: compressed,
    /// each one will measure something else, and this is the one place that would have to know.
    /// </para>
    /// </remarks>
    /// <param name="bytes">What each screen takes, which has to fit in a page: see <see cref="Bytes"/>.</param>
    public static IReadOnlyList<IReadOnlyList<MapScreen>> Pages(
        IReadOnlyList<MapScreen> screens, int bytes, int pageSize)
    {
        var pages = new List<IReadOnlyList<MapScreen>>();
        var page = new List<MapScreen>();
        int used = 0;

        foreach (MapScreen screen in screens)
        {
            if (used + bytes > pageSize && page.Count > 0)
            {
                pages.Add(page);
                page = [];
                used = 0;
            }

            page.Add(screen);
            used += bytes;
        }

        if (page.Count > 0)
            pages.Add(page);

        return pages;
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

        AppendRows(
            text,
            style.Directive("dw"),
            columns,
            rows,
            (column, row) => labels.GetValueOrDefault((column, row), "0"));

        return text.ToString();
    }

    /// <summary>
    /// The table when the screens go out shared into the pages of a mapper: a page and an offset
    /// for each one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In a megaROM two screens of different pages have the same address, because the mapper
    /// shows each page in the same window. So the address alone does not say where a screen is:
    /// it takes the page, and then how far into it.
    /// </para>
    /// <para>
    /// Both counted from the start, and not as addresses: the page from the first one of the
    /// screens and the offset from the start of its page. That way they are good in whatever
    /// segment the pages end up and whatever window the mapper shows them in; the game maps the
    /// page and adds the offset to where it sees it.
    /// </para>
    /// <para>
    /// Numbers the tool works out and not labels for the assembler, because in a megaROM each
    /// segment is often assembled on its own, and then a table cannot name what is in another
    /// one. The tool can: it is the one that shared the screens out, and each page file comes
    /// out exactly as it counted it.
    /// </para>
    /// </remarks>
    public static string PagedIndex(
        TileMap map,
        IReadOnlyList<IReadOnlyList<MapScreen>> pages,
        int wide,
        int high,
        string stem,
        AsmStyle style,
        bool header,
        int pageSize)
    {
        int columns = Count(map.Width, wide);
        int rows = Count(map.Height, high);
        int bytes = Bytes(wide, high, header);

        string name = AsmLabel.Of(stem);
        string constant = name.ToUpperInvariant();

        var places = new Dictionary<(int Column, int Row), (int Page, int Offset)>();

        for (int page = 0; page < pages.Count; page++)
        {
            int offset = 0;

            foreach (MapScreen screen in pages[page])
            {
                places[(screen.Column, screen.Row)] = (page, offset);
                offset += bytes;
            }
        }

        var text = new StringBuilder();

        text.AppendLine($"; Screen index - {map.Name}, in pages of {pageSize / 1024}K");
        text.AppendLine(
            $"; {columns}x{rows} screens of {wide}x{high} cells, {bytes} bytes each"
            + (header ? ", the size in front included" : string.Empty)
            + $", in {pages.Count} page{(pages.Count == 1 ? string.Empty : "s")}.");
        text.AppendLine(
            $"; Row by row: screen C-R is entry (R-1)*{columns} + (C-1), counting from 1 like the files.");
        text.AppendLine(
            $"; Each screen is a page and an offset into it. Pages count from 0 in the order of the");
        text.AppendLine(
            $"; files {name}{PageSuffix}N{IndexExtension}: map the one it says, and add the offset");
        text.AppendLine("; to wherever that page shows up in memory.");
        text.AppendLine($"; Empty screens were not written: their page is {AsmHex.Of(NoPage)}.");
        text.AppendLine();
        text.AppendLine($"{constant}_SCREENS_WIDE {style.Directive("equ")} {columns}");
        text.AppendLine($"{constant}_SCREENS_HIGH {style.Directive("equ")} {rows}");
        // PAGE_COUNT and not PAGES: sasSX reads names without telling capitals apart, and
        // NIVEL_1_SCREENS_PAGES is the table nivel_1_screens_pages to it, defined twice.
        text.AppendLine($"{constant}_SCREENS_PAGE_COUNT {style.Directive("equ")} {pages.Count}");
        text.AppendLine();
        text.AppendLine($"{name}{IndexSuffix}_pages:");

        AppendRows(
            text,
            style.Directive("db"),
            columns,
            rows,
            (column, row) => places.TryGetValue((column, row), out var place)
                ? AsmHex.Of((byte)place.Page)
                : AsmHex.Of(NoPage));

        text.AppendLine();
        text.AppendLine($"{name}{IndexSuffix}_offsets:");

        AppendRows(
            text,
            style.Directive("dw"),
            columns,
            rows,
            (column, row) => places.TryGetValue((column, row), out var place)
                ? $"{AsmHex.Prefix}{place.Offset:X4}"
                : $"{AsmHex.Prefix}{0:X4}");

        return text.ToString();
    }

    /// <summary>
    /// One page of screens, starting under its own label.
    /// </summary>
    /// <remarks>
    /// The offsets of the table count from the first byte of this, so nothing can go between the
    /// label and the screens; and whatever goes before it in the segment has to be added.
    /// </remarks>
    public static string Page(
        TileMap map,
        IReadOnlyList<MapScreen> screens,
        int page,
        string stem,
        AsmStyle style,
        bool binary,
        int bytes,
        int pageSize)
    {
        string name = AsmLabel.Of(stem);
        var text = new StringBuilder();

        text.AppendLine(
            $"; Page {page} of the screens of {map.Name}: {screens.Count * bytes} bytes"
            + $" of the {pageSize} a page holds.");
        text.AppendLine(
            $"; The offsets in {name}{IndexSuffix}{IndexExtension} count from the start of this: put it at");
        text.AppendLine("; the start of its segment, or add where it starts.");
        text.AppendLine();
        text.AppendLine($"{name}{PageSuffix}{page}:");

        AppendScreens(text, screens, stem, style, binary);

        return text.ToString();
    }

    /// <summary>
    /// A table with an entry per screen, row by row, a line per row at most as long as it can be.
    /// </summary>
    private static void AppendRows(
        StringBuilder text, string directive, int columns, int rows, Func<int, int, string> entry)
    {
        for (int row = 0; row < rows; row++)
        {
            string[] entries = [.. Enumerable.Range(0, columns).Select(column => entry(column, row))];

            for (int start = 0; start < entries.Length; start += EntriesPerLine)
            {
                string line = $"    {directive} "
                              + string.Join(", ", entries.Skip(start).Take(EntriesPerLine));

                text.AppendLine(start == 0 ? $"{line}    ; row {row + 1}" : line);
            }
        }
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

        AppendScreens(text, screens, stem, style, binary);

        return text.ToString();
    }

    /// <summary>
    /// The lines that bring the screens in: the ones in bytes with their label, the ones in
    /// assembler with an include, since they carry the label inside.
    /// </summary>
    private static void AppendScreens(
        StringBuilder text, IReadOnlyList<MapScreen> screens, string stem, AsmStyle style, bool binary)
    {
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
    }

    /// <summary>
    /// The comment lines that say where this screen came from.
    /// </summary>
    /// <remarks>
    /// The file of a loose screen does not say which map it belongs to nor where it was, and with
    /// twenty in the same folder the name is all that is left. This leaves it written inside.
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
