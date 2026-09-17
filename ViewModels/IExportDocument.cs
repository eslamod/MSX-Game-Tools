using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

/// <summary>What a document comes out as when it is exported.</summary>
public enum ExportFormat
{
    Assembler,
    Binary,
    Png,
}

/// <summary>One of the formats on offer, with the label it is read by and what it writes.</summary>
public sealed record ExportChoice(ExportFormat Format, string Label, string Extension);

/// <summary>
/// One of the files an export is going to write: how its name ends and who writes it.
/// </summary>
/// <param name="Extension">
/// Its own, for the one that does not follow the format: the example ROM is assembler even when
/// what is being exported is binary. Nothing means the one of the chosen format.
/// </param>
public sealed record ExportPiece(string Suffix, Func<string, Task> Write, string? Extension = null);

/// <summary>
/// Everything that was answered in the panel, which is what decides what comes out.
/// </summary>
/// <param name="Stem">The name they all share, out of the chosen destination.</param>
/// <param name="Rom">
/// The assembler the example ROM is for, or nothing when it was not asked for. It is the
/// assembler and not a flag because with the box ticked it rules over the whole batch, the data
/// files included.
/// </param>
public sealed record ExportRequest(ExportFormat Format, string Stem, AsmStyle Style, AsmDialect? Rom);

/// <summary>
/// A document that can be exported: what it writes, in what formats, and what has to be said
/// afterwards.
/// </summary>
/// <remarks>
/// <para>
/// The panel knows nothing about tile sets or sprite banks: it asks for the format and the
/// destination, lists what is going to be written, and writes it. What comes out of each kind of
/// document lives here, one file each, so that a new document is a new implementation and not
/// one more branch inside the panel.
/// </para>
/// <para>
/// It is not the editor view models that implement this. What a document exports needs the
/// exporters and, for some, a dialog to ask something first; hanging that off the editor would
/// grow two classes that are already big with something that is not editing.
/// </para>
/// </remarks>
public interface IExportDocument
{
    /// <summary>What it is called, which is what the panel puts in its title.</summary>
    string DocumentName { get; }

    /// <summary>The name the files take while nobody has chosen one.</summary>
    string Stem { get; }

    /// <summary>The formats it can come out in.</summary>
    IReadOnlyList<ExportChoice> Formats { get; }

    /// <summary>
    /// Whether an example ROM can be written for it, which is what puts the box on show.
    /// </summary>
    /// <remarks>
    /// Asked and not assumed because the templates arrive one at a time: offering a ROM that
    /// nobody writes would be a tick box that does nothing.
    /// </remarks>
    bool HasExampleRom { get; }

    /// <summary>
    /// What is going to be written.
    /// </summary>
    /// <remarks>
    /// One list for showing and for writing. With two, the day one of them changed the panel
    /// would be promising one thing and writing another.
    /// </remarks>
    IEnumerable<ExportPiece> Pieces(ExportRequest request);

    /// <summary>
    /// Whether it can go ahead, asking first if there is something to warn about.
    /// </summary>
    /// <remarks>
    /// Before a single file is written, so that saying no leaves the folder as it was.
    /// </remarks>
    Task<bool> ReadyAsync();

    /// <summary>What has to be known afterwards and is not in the files, or nothing.</summary>
    (string Title, string Body)? Note(ExportRequest request);
}
