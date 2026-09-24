using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

/// <summary>What a document comes out as when it is exported.</summary>
public enum ExportFormat
{
    Assembler,
    Binary,
    Png,
    Csv,
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
/// <param name="Screens">
/// How the map is cut when it goes out screen by screen, or nothing when the whole thing goes
/// out in one file, which is what every other document does.
/// </param>
/// <param name="Folder">
/// Where the files are going. For what has to be said afterwards about what was already there:
/// a screen written on its own can be missing from an index that was written before it.
/// </param>
public sealed record ExportRequest(
    ExportFormat Format,
    string Stem,
    AsmStyle Style,
    AsmDialect? Rom,
    ScreenSplit? Screens = null,
    string Folder = "");

/// <summary>
/// Cutting what is exported into the screens of the game.
/// </summary>
/// <param name="Header">
/// Whether each file carries the four bytes of the size in front. They are the same four in
/// every one of them —every screen measures the same— but whoever loads them with the code
/// that reads a whole map needs them, so it is asked rather than decided here.
/// </param>
/// <param name="Only">
/// The one screen that goes out, or nothing for every screen with something on it. Asking for
/// one by its number writes it even if it is empty: that one was asked for.
/// </param>
/// <param name="Index">
/// Whether the table of where each screen starts goes out as well, with the file that brings
/// the screens in. Only with all of them: the index speaks of the whole batch, and a single
/// screen leaves the one there was as it was.
/// </param>
public sealed record ScreenSplit(bool Header, ScreenNumber? Only = null, bool Index = false);

/// <summary>
/// One screen of the map, said the way the editor reads it: column and row, counting from one.
/// </summary>
/// <remarks>
/// From one and column first, like the label at the bottom of the editor. The screen read there
/// as «3-1» is the one asked for here, and the file that comes out ends in <c>_3_1</c>.
/// </remarks>
public sealed record ScreenNumber(int Column, int Row)
{
    public override string ToString() => $"{Column}-{Row}";
}

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
    /// The screen the panel starts on, or nothing when this document has no screens.
    /// </summary>
    /// <remarks>
    /// Nothing doubles as «this one cannot be cut», which is what keeps that box from coming
    /// out: only a map has screens, and a table has no rectangle to cut. A map always gives
    /// one, even when the size does not work out; what is wrong with the size is said by
    /// <see cref="Problem"/>, where there is room to explain it.
    /// </remarks>
    ScreenNumber? FirstScreen { get; }

    /// <summary>
    /// What stops this export, or nothing when there is nothing in the way.
    /// </summary>
    /// <remarks>
    /// In the panel and before a single file is written, next to the list of what is coming: an
    /// empty list says that nothing is going out but not why, and the answer —the screen does
    /// not go a whole number of times into the super tile— is not in this panel.
    /// </remarks>
    string? Problem(ExportRequest request);

    /// <summary>
    /// What is about to be written, for the document to show where it can be seen. Nothing when
    /// the panel is gone.
    /// </summary>
    /// <remarks>
    /// The panel has nowhere to show it: a map marks the screen on the map, which is where it
    /// can be told which piece is being talked about. It is called whenever the answers change
    /// what comes out, and once more when the panel closes; whoever has nothing to show does
    /// nothing.
    /// </remarks>
    void Preview(ExportRequest? request);

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
