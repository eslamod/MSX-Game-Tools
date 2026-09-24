using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// What comes out of exporting a palette: the 32 bytes the palette register of the V9938 wants.
/// </summary>
/// <remarks>
/// <para>
/// One file, unlike the banks and the tile sets: 32 bytes with nothing to split.
/// </para>
/// <para>
/// The palette is the one on the bar and not the one of the document in front, so this is the
/// only export that does not need anything open. Which one it is gets decided when the panel
/// opens: what was on the bar then is what comes out, even if the bar changes meanwhile.
/// </para>
/// </remarks>
public sealed class PaletteExport(ColorPalette palette) : IExportDocument
{
    private static Localizer Text => Localizer.Instance;

    public string DocumentName => palette.Name;

    /// <summary>
    /// With <c>_palette</c> already in it, and not as the end of every file.
    /// </summary>
    /// <remarks>
    /// Only one file comes out of here, so whatever name is chosen is the name it takes: adding
    /// an ending to a name somebody typed in full would be a surprise.
    /// </remarks>
    public string Stem => $"{AsmLabel.Of(palette.Name)}_palette";

    public IReadOnlyList<ExportChoice> Formats { get; } =
    [
        new(ExportFormat.Assembler, Text["ExportFormatAsm"], ".asm"),
        new(ExportFormat.Binary, Text["ExportFormatBin"], ".bin"),
    ];

    /// <summary>
    /// No ROM.
    /// </summary>
    /// <remarks>
    /// A palette on its own shows nothing that the other three do not show already: all of them
    /// carry it inside and load it on a machine that has one.
    /// </remarks>
    public bool HasExampleRom => false;

    public IEnumerable<ExportPiece> Pieces(ExportRequest request)
    {
        yield return new ExportPiece(string.Empty, path =>
            request.Format == ExportFormat.Binary
                ? File.WriteAllBytesAsync(path, PaletteExporter.ToBinary(palette))
                : File.WriteAllTextAsync(path, PaletteExporter.ToAssembler(palette, request.Style)));
    }

    /// <summary>Nothing to ask: a palette always exports.</summary>
    public Task<bool> ReadyAsync() => Task.FromResult(true);

    /// <summary>And nothing to say afterwards: what came out is the file that was asked for.</summary>
    public (string Title, string Body)? Note(ExportRequest request) => null;
}
