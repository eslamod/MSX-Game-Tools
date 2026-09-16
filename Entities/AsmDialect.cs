namespace MSX_GameTools.Entities;

/// <summary>
/// One of the assemblers an example ROM can be written for.
/// </summary>
/// <remarks>
/// <para>
/// Measured and not assumed: the same ROM was assembled with the four of them, and for three
/// the only thing that changes is the dot in front of the directives —the <c>equ</c>, the
/// <c>incbin</c>, the <c>include</c>, the <c>ds</c> with a filler byte and the expressions with
/// a label they all take the same way, and the three give the very same 16384 bytes—.
/// </para>
/// <para>
/// asMSX is the one that does not fit that mould, and it is why a dialect is more than a dot.
/// It builds the cartridge itself out of <c>.rom</c> and <c>.start</c>, so the sixteen bytes of
/// the header are not written by hand; its <c>ds</c> takes a length and no filler byte, so the
/// padding line the others use does not even assemble; and it rounds the ROM up to the smallest
/// cartridge that holds it instead of leaving it at 16 KB. Hence the header and the tail of the
/// file being part of this and not of the template.
/// </para>
/// <para>
/// Two more things it taught, both in the template now: indirections are written in square
/// brackets unless <c>.zilog</c> says otherwise, so a <c>ld a,(hl)</c> stops it dead; and
/// <c>start</c> is one of its directives, so a label called that is read as one. That is why
/// the entry point of the ROM is called Begin.
/// </para>
/// <para>
/// The command line goes with them because it is the one thing that is not in the file and is
/// needed to build it: it is written into the header of the ROM that comes out, so that whoever
/// exported it has it at hand and does not have to go looking for the flag that names the
/// output.
/// </para>
/// </remarks>
/// <param name="Name">What it is called, which is what gets shown and stored.</param>
/// <param name="Dotted">Whether its directives carry a leading dot.</param>
/// <param name="Command">How it is run, with <c>{SOURCE}</c> and <c>{ROM}</c> for the names.</param>
/// <param name="Header">What opens the file, up to the entry point.</param>
/// <param name="Tail">What closes it, which is where the ROM gets its size.</param>
public sealed record AsmDialect(
    string Name, bool Dotted, string Command, string Header, string Tail)
{
    /// <summary>The one the test ROMs of this repository are assembled with.</summary>
    public static AsmDialect SasSx { get; } =
        new("sasSX", true, "sasSX.exe {SOURCE} {ROM}", Cartridge, Padding);

    /// <summary>Takes both spellings; the output file is named by a flag.</summary>
    public static AsmDialect SjasmPlus { get; } =
        new("sjasmplus", true, "sjasmplus --raw={ROM} {SOURCE}", Cartridge, Padding);

    /// <summary>The one that rejects the dot.</summary>
    public static AsmDialect Pasmo { get; } =
        new("pasmo", false, "pasmo {SOURCE} {ROM}", Cartridge, Padding);

    /// <summary>The one that builds the cartridge itself and names the output from inside.</summary>
    public static AsmDialect AsMsx { get; } =
        new("asMSX", true, "asMSX {SOURCE}", AsMsxCartridge, AsMsxPadding);

    /// <summary>All of them, in the order they are offered.</summary>
    public static IReadOnlyList<AsmDialect> All { get; } = [SasSx, SjasmPlus, Pasmo, AsMsx];

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
        Command.Replace("{SOURCE}", source).Replace("{ROM}", rom);

    // ------------------------------------------------------------------ how a ROM opens and closes

    /// <summary>The sixteen bytes the BIOS goes looking for in page 1, written by hand.</summary>
    private const string Cartridge =
        """
                        {ORG} 0x4000

                        {DB} "AB"
                        {DW} {START}     ; INIT
                        {DW} 0           ; STATEMENT
                        {DW} 0           ; DEVICE
                        {DW} 0           ; TEXT
                        {DB} 0,0,0,0,0,0 ; reserved
        """;

    /// <summary>
    /// Padding up to 16K by hand.
    /// </summary>
    /// <remarks>
    /// With a label and not with <c>$</c>, because in sasSX a <c>$</c> inside an expression does
    /// not give the program counter: it stays at zero and a 32K ROM comes out.
    /// </remarks>
    private const string Padding =
        """
        ; Padding up to 16K, the size a cartridge in page 1 is expected to be. With a
        ; label and not with $, because in sasSX a $ inside an expression does not give
        ; the PC: it stays at zero and a 32K ROM comes out.
        RomEnd:
                        {DS} 0x8000 - RomEnd, 0xFF
        """;

    /// <inheritdoc cref="AsmDialect"/>
    private const string AsMsxCartridge =
        """
        ; asMSX writes those sixteen bytes itself: .rom says what is being built and
        ; .start says where it begins. .filename is what the ROM comes out called, because
        ; this one takes no output name on the command line. And .zilog is what puts the
        ; indirections back in parentheses: left alone, asMSX writes them in square
        ; brackets and a ld a,(hl) does not even get through.
                        .zilog
                        .filename "{STEM}"
                        .page 1
                        .rom
                        .start {START}
        """;

    /// <inheritdoc cref="AsmDialect"/>
    private const string AsMsxPadding =
        """
        ; No padding by hand here: asMSX rounds the ROM up to the smallest cartridge that
        ; holds it, which for this one is 8 KB. And its ds takes a length and no filler
        ; byte, so the line the other assemblers use would not even assemble.
        """;
}
