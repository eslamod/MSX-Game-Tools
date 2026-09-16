namespace MSX_GameTools.Entities;

/// <summary>
/// One of the assemblers an example ROM can be written for.
/// </summary>
/// <remarks>
/// <para>
/// Measured and not assumed: the same ROM was assembled with the three of them and the three
/// gave the very same 16384 bytes, so the only thing that changes from one to the next is the
/// dot in front of the directives. Everything else the ROM uses —<c>equ</c>, <c>incbin</c>,
/// <c>include</c>, <c>ds</c> with a filler byte, expressions with a label— all three take the
/// same way.
/// </para>
/// <para>
/// The command line goes with them because it is the one thing that is not in the file and is
/// needed to build it: it is written into the header of the ROM that comes out, so that whoever
/// exported it has it at hand and does not have to go looking for the flag that names the
/// output.
/// </para>
/// <para>
/// asMSX is missing on purpose: it was not here to be tried, and it is the one that does not
/// fit the mould —it writes the cartridge header itself with <c>.rom</c> and <c>.page</c>,
/// which is precisely what this ROM writes by hand—. Adding it blind would hand a beginner a
/// file that does not assemble, which is the opposite of what this is for.
/// </para>
/// </remarks>
/// <param name="Name">What it is called, which is what gets shown and stored.</param>
/// <param name="Dotted">Whether its directives carry a leading dot.</param>
/// <param name="Command">
/// How it is run, with <c>{source}</c> and <c>{rom}</c> where the file names go.
/// </param>
public sealed record AsmDialect(string Name, bool Dotted, string Command)
{
    /// <summary>The one the test ROMs of this repository are assembled with.</summary>
    public static AsmDialect SasSx { get; } = new("sasSX", true, "sasSX.exe {source} {rom}");

    /// <summary>Takes both spellings; the output file is named by a flag.</summary>
    public static AsmDialect SjasmPlus { get; } =
        new("sjasmplus", true, "sjasmplus --raw={rom} {source}");

    /// <summary>The one that rejects the dot.</summary>
    public static AsmDialect Pasmo { get; } = new("pasmo", false, "pasmo {source} {rom}");

    /// <summary>All of them, in the order they are offered.</summary>
    public static IReadOnlyList<AsmDialect> All { get; } = [SasSx, SjasmPlus, Pasmo];

    /// <summary>The one that comes up when nobody has chosen.</summary>
    public static AsmDialect Default => SasSx;

    /// <summary>How the data directives of the exported files have to be written for this one.</summary>
    public AsmStyle Style => new(Directive("db"));

    /// <summary>The one that answers to that name, or the one of always.</summary>
    public static AsmDialect Of(string? name) =>
        All.FirstOrDefault(dialect => dialect.Name == name) ?? Default;

    /// <summary>That directive as this assembler wants it written.</summary>
    public string Directive(string name) => Dotted ? "." + name : name;

    /// <summary>The line to type to build that ROM out of that source.</summary>
    public string CommandFor(string source, string rom) =>
        Command.Replace("{source}", source).Replace("{rom}", rom);
}
