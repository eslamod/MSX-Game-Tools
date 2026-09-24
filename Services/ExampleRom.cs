using System.Text;
using System.Text.RegularExpressions;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// The .asm of a ROM that shows on a real machine what has just been exported.
/// </summary>
/// <remarks>
/// <para>
/// What comes out of an export is bytes, and what has to be done with them is not in them: the
/// VDP registers of the mode, the three copies of each table that GRAPHIC 2 needs, the name
/// table. Somebody who is starting has nowhere to read that from. This writes a ROM that does
/// it, next to the very files that were exported and for the assembler that was chosen.
/// </para>
/// <para>
/// Out of a template and not built line by line here because it is a program, and a program is
/// read as a program: it lives in <c>Templates/</c> as assembler, with its comments, and only
/// the handful of things that change from one assembler or one screen mode to the next are
/// tokens. Writing it from C# would turn 350 lines of Z80 into 350 string literals that nobody
/// can assemble to check.
/// </para>
/// </remarks>
public static class ExampleRom
{
    /// <summary>What the name of the ROM source ends in, next to the files it loads.</summary>
    public const string Suffix = "_rom";

    /// <summary>The ROM source is assembler whatever the data files came out as.</summary>
    public const string Extension = ".asm";

    private const string Templates = "MSX_GameTools.Templates.";

    private const int BytesPerLine = 8;

    /// <summary>The directives the templates ask for by name.</summary>
    private static readonly string[] Directives =
        ["org", "db", "dw", "equ", "ds", "incbin", "include", "if", "else", "endif"];

    /// <summary>What is left to fill in: <c>{NAME}</c>, <c>{PALETTE}</c>…</summary>
    private const string TokenPattern = @"\{([A-Z0-9_]+)\}";

    /// <summary>
    /// What the entry point of the ROM is called.
    /// </summary>
    /// <remarks>
    /// Begin and not Start because in asMSX <c>start</c> is a directive, and a label by that
    /// name is read as one: the file stops on the line where the header names it.
    /// </remarks>
    private const string Entry = "Begin";

    /// <summary>Where a 16K cartridge ends, which is where page 2 starts.</summary>
    private const string PageTwo = "0x8000";

    /// <summary>And where a 32K one does, taking pages 1 and 2.</summary>
    private const string PageThree = "0xC000";

    /// <summary>Where the pattern and the colour tables live in VRAM, as always.</summary>
    private const int PatternTable = 0x0000;

    /// <inheritdoc cref="PatternTable"/>
    private const int ColorTable = 0x2000;

    /// <summary>
    /// What the routine that plays the animations is called.
    /// </summary>
    /// <remarks>
    /// A file of its own and not a chunk inside the ROM because it is the piece that gets
    /// copied into a real game: it travels whole, and not spread through an example.
    /// </remarks>
    public const string PlayerSuffix = "_player";

    /// <summary>What the ROM of that export is called.</summary>
    public static string NameOf(string stem) => $"{stem}{Suffix}{Extension}";

    /// <inheritdoc cref="PlayerSuffix"/>
    public static string PlayerNameOf(string stem) => $"{stem}{PlayerSuffix}{Extension}";

    /// <summary>
    /// The ROM that shows a tile set: the two tables in VRAM and the whole set on screen.
    /// </summary>
    /// <param name="stem">The name the exported files share, which is what it has to load.</param>
    /// <param name="binary">
    /// Whether those files are the binary ones, which is what tells an <c>incbin</c> from an
    /// <c>include</c>.
    /// </param>
    public static string ForTileSet(
        TileSet tileSet, ColorPalette palette, AsmDialect dialect, string stem, bool binary)
    {
        string patterns = $"{stem}_patterns";
        string colors = $"{stem}_colors";
        string extension = binary ? ".bin" : Extension;

        // The registers of the mode. In GRAPHIC 2 the ones for R#3 and R#4 are not the address
        // divided by anything: the bits below carry the mask that splits the tables into the
        // three thirds, and that is why they come out as 0xFF and 0x03 with the tables where
        // they always are. In GRAPHIC 1 they are the plain address: 0x2000 / 64 and 0 / 2048.
        bool graphic1 = tileSet.IsGraphic1;

        return Fill("TileSetRom.asm", dialect, new Dictionary<string, string>
        {
            ["NAME"] = tileSet.Name,
            ["STEM"] = stem,
            ["START"] = Entry,
            ["ROM_END"] = PageTwo,
            ["ASSEMBLER"] = dialect.Name,
            ["COMMAND"] = dialect.CommandFor(NameOf(stem), $"{stem}.rom"),
            ["FILES"] = $";     {patterns}{extension}\n;     {colors}{extension}",
            ["MODE"] = graphic1 ? "GRAPHIC 1" : "GRAPHIC 2",
            ["SCREEN"] = graphic1 ? "SCREEN 1" : "SCREEN 2",
            ["R0"] = graphic1 ? "0x00" : "0x02",
            ["R3"] = graphic1 ? "0x80" : "0xFF",
            ["R4"] = graphic1 ? "0x00" : "0x03",

            // In GRAPHIC 1 the colour table is another table, not the same one cut short: one
            // byte per group of eight tiles, and one of it for the whole screen.
            ["COLOR_BYTES"] = graphic1
                ? TileSet.ColorGroupCount.ToString()
                : TileSetExporter.TableBytes.ToString(),
            ["TABLE_COPIES"] = graphic1 ? "1" : TileSetExporter.ScreenThirds.ToString(),

            ["PALETTE"] = PaletteLines(palette, dialect),
            ["PATTERNS"] = Loads(dialect, patterns, binary),
            ["PATTERNS_ALT"] = Loads(dialect, patterns, !binary, commented: true),
            ["COLORS"] = Loads(dialect, colors, binary),
            ["COLORS_ALT"] = Loads(dialect, colors, !binary, commented: true),
        });
    }

    /// <summary>
    /// The ROM that shows a sprite bank: its patterns in the sprite generator and its groups
    /// laid out in a grid, playing the animations if it has any.
    /// </summary>
    /// <remarks>
    /// Only for an MSX2 bank, which is the one that exports the sixteen colour bytes per sprite
    /// that mode 2 asks for. An MSX1 one is another program -mode 1, four sprites per line, the
    /// colour inside the attribute- and not a couple of tokens.
    /// </remarks>
    /// <param name="stem">The name the exported files share, which is what it has to load.</param>
    /// <param name="binary">
    /// Whether those files are the binary ones, which is what tells an <c>incbin</c> from an
    /// <c>include</c>.
    /// </param>
    public static string ForSpriteBank(
        SpriteBank bank, ColorPalette palette, AsmDialect dialect, string stem, bool binary)
    {
        string patterns = $"{stem}_patterns";
        string groups = $"{stem}_groups";
        string animations = $"{stem}_animations";
        string extension = binary ? ".bin" : Extension;
        bool animated = bank.Animations.Count > 0;

        string files = $";     {patterns}{extension}\n;     {groups}{extension}";

        if (animated)
            files += $"\n;     {animations}{extension}";

        return Fill("SpriteBankRom.asm", dialect, new Dictionary<string, string>
        {
            ["NAME"] = bank.Name,
            ["STEM"] = stem,
            ["START"] = Entry,
            ["ROM_END"] = PageTwo,
            ["ASSEMBLER"] = dialect.Name,
            ["COMMAND"] = dialect.CommandFor(NameOf(stem), $"{stem}.rom"),
            ["FILES"] = files,
            ["PALETTE"] = PaletteLines(palette, dialect),
            ["PATTERNS"] = Loads(dialect, patterns, binary),
            ["PATTERNS_ALT"] = Loads(dialect, patterns, !binary, commented: true),
            ["GROUPS"] = Loads(dialect, groups, binary),
            ["GROUPS_ALT"] = Loads(dialect, groups, !binary, commented: true),

            // Without animations the block stays empty on purpose: the player compares the two
            // labels and never starts, and the ROM behaves like it did before there were any.
            ["ANIMATIONS"] = animated
                ? Loads(dialect, animations, binary)
                : "              ; this bank has no animations",
            ["ANIMATIONS_ALT"] = animated
                ? Loads(dialect, animations, !binary, commented: true)
                : string.Empty,

            ["PLAYER"] = $"                {dialect.Directive("include")} \"{PlayerNameOf(stem)}\"",
        });
    }

    /// <inheritdoc cref="PlayerSuffix"/>
    public static string AnimationPlayer(AsmDialect dialect, SpriteBank bank) =>
        Fill("AnimationPlayer.asm", dialect, new Dictionary<string, string>
        {
            // In mode 2 the colour of a sprite is sixteen bytes in a table of its own; in mode 1
            // it travels in the fourth byte of the attribute, and the player takes both.
            ["ANIM_COLOR_TABLE"] = bank.Type == SpriteBank.SpriteType.MSX2 ? "1" : "0",
        });

    /// <summary>
    /// The ROM that shows a map: the tile set in VRAM, the map drawn over it and the cursor
    /// keys to move around one that is bigger than the screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A 32K cartridge taking pages 1 and 2, because the map travels inside the ROM and 16K
    /// ran short straight away.
    /// </para>
    /// <para>
    /// The files of the tile set are not the map's: they come out of exporting the tile set,
    /// and this names them the way that export names them when nobody changes the name. What
    /// it cannot do is write them, so the header of the ROM says which ones have to be next
    /// to it.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// <para>
    /// A map of super tiles is another program and another template: its cells are places in
    /// the super tile table and not tile numbers, so it resolves each one through the table
    /// as it draws. Which one it is gets decided here and not in the panel, because from the
    /// outside it is the same question: the ROM of this map.
    /// </para>
    /// </remarks>
    /// <param name="bands">
    /// The tile set of each band, one to three of them. More than one means a different set
    /// in each screen third, and that is what the list of blocks is for.
    /// </param>
    public static string ForMap(
        TileMap map,
        IReadOnlyList<TileSet> bands,
        ColorPalette palette,
        AsmDialect dialect,
        string stem,
        bool binary)
    {
        bool graphic1 = bands[0].IsGraphic1;
        bool superTiles = bands[0].HasSuperTiles;
        string extension = binary ? ".bin" : Extension;

        // In GRAPHIC 1 there is one table of each for the whole screen; in GRAPHIC 2, one per
        // third, and they are only the same table when the map has a single band.
        int thirds = graphic1 ? 1 : TileSetExporter.ScreenThirds;
        int colorBytes = graphic1 ? TileSet.ColorGroupCount : TileSetExporter.TableBytes;

        var tables = new List<string>();

        for (int third = 0; third < thirds; third++)
        {
            tables.Add(Block(
                dialect,
                $"Patterns{BandOf(bands, third)}",
                PatternTable + (third * TileSetExporter.TableBytes),
                TileSetExporter.TableBytes,
                $"patterns, third {third + 1}"));
        }

        for (int third = 0; third < thirds; third++)
        {
            tables.Add(Block(
                dialect,
                $"Colors{BandOf(bands, third)}",
                ColorTable + (third * colorBytes),
                colorBytes,
                $"colours, third {third + 1}"));
        }

        var values = new Dictionary<string, string>
        {
            ["NAME"] = map.Name,
            ["STEM"] = stem,
            ["START"] = Entry,
            ["ROM_END"] = PageThree,
            ["ASSEMBLER"] = dialect.Name,
            ["COMMAND"] = dialect.CommandFor(NameOf(stem), $"{stem}.rom"),
            ["FILES"] = FilesOf(bands, stem, extension, superTiles),
            ["MODE"] = graphic1 ? "GRAPHIC 1" : "GRAPHIC 2",
            ["SCREEN"] = graphic1 ? "SCREEN 1" : "SCREEN 2",
            ["R0"] = graphic1 ? "0x00" : "0x02",
            ["R3"] = graphic1 ? "0x80" : "0xFF",
            ["R4"] = graphic1 ? "0x00" : "0x03",
            ["TABLE_LOADS"] = tables.Count.ToString(),
            ["TABLES"] = string.Join("\n", tables),
            ["PALETTE"] = PaletteLines(palette, dialect),
            ["TILESETS"] = TileSetsOf(bands, dialect, binary),
            ["MAP"] = Loads(dialect, stem, binary),
            ["MAP_ALT"] = Loads(dialect, stem, !binary, commented: true),
        };

        if (superTiles)
        {
            // The table is the tile set's and not the map's: every map drawn with that set
            // shares the same one, so it comes out of exporting the set.
            string table = $"{AsmLabel.Of(bands[0].Name)}_supertiles";

            values["SUPERTILES"] = Loads(dialect, table, binary);
            values["SUPERTILES_ALT"] = Loads(dialect, table, !binary, commented: true);
        }

        return Fill(superTiles ? "SuperTileMapRom.asm" : "MapRom.asm", dialect, values);
    }

    /// <summary>Which band a screen third takes its tile set from.</summary>
    /// <remarks>
    /// The same rule the map itself follows: a third with no band of its own falls back to the
    /// first one, which is what a map with a single tile set is.
    /// </remarks>
    private static int BandOf(IReadOnlyList<TileSet> bands, int third) =>
        third < bands.Count ? third : 0;

    /// <summary>One entry of the list: where it comes from, where it goes and how much.</summary>
    private static string Block(
        AsmDialect dialect, string source, int destination, int length, string what) =>
        $"                {{DW}} {source}, 0x{destination:X4}, {length}".PadRight(58) + $"; {what}";

    /// <summary>The blocks of every band, each one behind the label the list names.</summary>
    private static string TileSetsOf(
        IReadOnlyList<TileSet> bands, AsmDialect dialect, bool binary)
    {
        var lines = new List<string>();

        for (int band = 0; band < bands.Count; band++)
        {
            string label = AsmLabel.Of(bands[band].Name);

            lines.Add(bands.Count == 1
                ? $"; The tile set of the map: {bands[band].Name}"
                : $"; Rows {band * TileMap.RowsPerThird}-{(band * TileMap.RowsPerThird) + TileMap.RowsPerThird - 1}: {bands[band].Name}");

            lines.Add($"Patterns{band}:");
            lines.Add(Loads(dialect, $"{label}_patterns", binary));
            lines.Add(Loads(dialect, $"{label}_patterns", !binary, commented: true));
            lines.Add(string.Empty);
            lines.Add($"Colors{band}:");
            lines.Add(Loads(dialect, $"{label}_colors", binary));
            lines.Add(Loads(dialect, $"{label}_colors", !binary, commented: true));

            if (band < bands.Count - 1)
                lines.Add(string.Empty);
        }

        return string.Join("\n", lines);
    }

    /// <summary>What has to be next to it, the tile set files included.</summary>
    private static string FilesOf(
        IReadOnlyList<TileSet> bands, string stem, string extension, bool superTiles)
    {
        var names = new List<string>();

        foreach (TileSet band in bands)
        {
            string label = AsmLabel.Of(band.Name);

            foreach (string one in (string[])[$"{label}_patterns", $"{label}_colors"])
            {
                if (!names.Contains(one))
                    names.Add(one);
            }

            if (superTiles)
                names.Add($"{label}_supertiles");
        }

        names.Add(stem);

        return string.Join("\n", names.Select(one => $";     {one}{extension}"));
    }

    // ------------------------------------------------------------------ the filling in

    /// <summary>
    /// The template with everything in its place.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The template asks for something nobody fills in. It stops here and not at the
    /// assembler: a token left behind goes out inside the ROM as text, and what the person
    /// exporting would see is their assembler complaining about a line they did not write.
    /// </exception>
    private static string Fill(
        string template, AsmDialect dialect, IReadOnlyDictionary<string, string> values)
    {
        // The two pieces of the assembler first: how a ROM opens and how it closes is the
        // one thing that is not the same program for all of them, and what they bring in
        // carries tokens of its own.
        string source = Read(template)
            .Replace("\r\n", "\n")
            .Replace("{HEADER}", dialect.Header)
            .Replace("{TAIL}", dialect.Tail);

        foreach (Match token in Regex.Matches(source, TokenPattern))
        {
            string name = token.Groups[1].Value;

            if (!values.ContainsKey(name) && !Directives.Contains(name.ToLowerInvariant()))
                throw new InvalidOperationException($"{template} asks for {token.Value}.");
        }

        var text = new StringBuilder(source);

        foreach ((string token, string value) in values)
            text.Replace($"{{{token}}}", value);

        // The directives last: by then everything is in —what the assembler brought in and
        // the lines built above— and all of it comes out spelled the way it wants.
        foreach (string directive in Directives)
            text.Replace($"{{{directive.ToUpperInvariant()}}}", dialect.Directive(directive));

        // The template is written with line feeds and what is put into it too, so the endings
        // are settled once here and the file comes out like the rest of what is exported.
        return text.ToString().Replace("\n", Environment.NewLine);
    }

    /// <summary>The line that brings in one of the exported files, and its commented other half.</summary>
    /// <remarks>
    /// Both, and not only the one that was exported, because the two outputs load the same way
    /// and swapping the comment is how the other one gets tried.
    /// </remarks>
    private static string Loads(AsmDialect dialect, string file, bool binary, bool commented = false)
    {
        string line = $"{dialect.Directive(binary ? "incbin" : "include")} "
                      + $"\"{file}{(binary ? ".bin" : Extension)}\"";

        return commented ? $"              ; {line}" : $"                {line}";
    }

    /// <summary>The palette inside the ROM, so that the bundle does not need one more file.</summary>
    private static string PaletteLines(ColorPalette palette, AsmDialect dialect)
    {
        byte[] bytes = PaletteExporter.ToBinary(palette);
        var lines = new List<string>();

        for (int start = 0; start < bytes.Length; start += BytesPerLine)
        {
            IEnumerable<string> line = bytes
                .Skip(start)
                .Take(BytesPerLine)
                .Select(SpriteBankExporter.HexOf);

            lines.Add($"                {dialect.Directive("db")}  {string.Join(",", line)}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>The template as it travels inside the build.</summary>
    private static string Read(string name)
    {
        using Stream? stream = typeof(ExampleRom).Assembly.GetManifestResourceStream(Templates + name);

        if (stream is null)
            throw new InvalidOperationException($"The template {name} did not travel in the build.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
