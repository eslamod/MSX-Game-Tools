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
    private static readonly string[] Directives = ["org", "db", "dw", "equ", "ds"];

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

    /// <summary>What the ROM of that export is called.</summary>
    public static string NameOf(string stem) => $"{stem}{Suffix}{Extension}";

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
