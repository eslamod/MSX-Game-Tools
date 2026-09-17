using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// One of the files an export is going to write.
/// </summary>
/// <param name="Exists">
/// Whether there is already one with that name where it is going, which means it is about to be
/// overwritten.
/// </param>
public sealed record ExportFileRow(string Name, bool Exists);

/// <summary>
/// Exporting a document: as what, where to, and which files are going to come out.
/// </summary>
/// <remarks>
/// <para>
/// A panel and not two menu entries because exporting is more than one question, and above all
/// because what comes out is more than one file: a tile set writes its patterns, its colours
/// and, when it has them, its super tiles and its attributes; a sprite bank writes its patterns,
/// its groups and its animations. The file picker of the system only warns about the one that
/// gets named, so the others could be overwritten without a word. Here they are all listed
/// before a single one is written.
/// </para>
/// <para>
/// What each kind of document writes is not here but behind <see cref="IExportDocument"/>: this
/// asks the questions, lists what is coming and writes it, and the same panel serves them all.
/// </para>
/// <para>
/// It is also where the two questions that only make sense one after the other are asked:
/// whether an asm of an example ROM is wanted as well, and which assembler it is for.
/// </para>
/// </remarks>
public partial class ExportViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;
    private readonly IExportDocument _document;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsExampleRom))]
    private ExportChoice _format;

    /// <summary>Where the first of the files goes; the others take their name from it.</summary>
    [ObservableProperty]
    private string _destination = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>
    /// Whether an asm of a ROM that shows this goes out as well.
    /// </summary>
    /// <remarks>
    /// Off by default: whoever already knows what to do with the tables does not need it, and
    /// it would be one more file landing in their folder without being asked for.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsAssembler))]
    private bool _wantsExampleRom;

    /// <summary>The one that example ROM is written for.</summary>
    [ObservableProperty]
    private AsmDialect _assembler = AsmDialect.Default;

    public ExportViewModel(MainWindowViewModel mainWindowVm, IExportDocument document)
    {
        _mainWindowVm = mainWindowVm;
        _document = document;

        _format = document.Formats[0];

        Header = $"{Text["ExportTitle"]}: {document.DocumentName}";
        TagId = "export";

        Refresh();
    }

    private static Localizer Text => Localizer.Instance;

    /// <summary>The formats this document can come out in.</summary>
    public IReadOnlyList<ExportChoice> Formats => _document.Formats;

    /// <summary>The assemblers an example ROM can be written for.</summary>
    public IReadOnlyList<AsmDialect> Assemblers { get; } = AsmDialect.All;

    /// <summary>
    /// The assembler is only asked for with the box ticked.
    /// </summary>
    /// <remarks>
    /// With it unticked there is no asm of ours going out, and the exported files follow what
    /// the preferences say: asking here as well would be the same question twice, with two
    /// answers that could disagree.
    /// </remarks>
    public bool ShowsAssembler => WantsExampleRom;

    /// <summary>
    /// The box only comes out where there is a ROM to write.
    /// </summary>
    /// <remarks>
    /// The ROM brings the exported files in, so it only makes sense for the two formats it can
    /// bring in: a png is a picture and a csv is for a spreadsheet. And a document whose
    /// template is not written yet would be offering a tick that does nothing.
    /// </remarks>
    public bool ShowsExampleRom => _document.HasExampleRom
        && Format.Format is ExportFormat.Assembler or ExportFormat.Binary;

    /// <summary>The files that are going to be written, with their names already worked out.</summary>
    public ObservableCollection<ExportFileRow> Files { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// How many of the files going out land on one that is already there.
    /// </summary>
    /// <remarks>
    /// This is what the file picker of the system cannot say: it only asks about the file that
    /// gets named, and a tile set writes up to four.
    /// </remarks>
    public string? Overwrites
    {
        get
        {
            int existing = Files.Count(file => file.Exists);

            return existing == 0 ? null : Text.Format("ExportOverwrites", existing, Files.Count);
        }
    }

    public bool HasOverwrites => Overwrites is not null;

    /// <summary>The chosen folder, or nothing while there is none.</summary>
    private string Folder => string.IsNullOrWhiteSpace(Destination)
        ? string.Empty
        : Path.GetDirectoryName(Destination) ?? string.Empty;

    /// <summary>
    /// The base name the others come from.
    /// </summary>
    /// <remarks>
    /// The document's own while no destination has been chosen, so that the list says something
    /// from the moment the panel opens; and the chosen one as soon as there is one, which is
    /// what the export already did.
    /// </remarks>
    private string Stem => string.IsNullOrWhiteSpace(Destination)
        ? _document.Stem
        : Path.GetFileNameWithoutExtension(Destination);

    /// <summary>
    /// Whether a ROM is going out: the box ticked, and a format it makes sense for.
    /// </summary>
    /// <remarks>
    /// Ticked and then the format changed to png or csv, the box stays as it was but the ROM
    /// does not go out: it would be naming a file it cannot read.
    /// </remarks>
    private bool WithRom => WantsExampleRom && ShowsExampleRom;

    /// <summary>
    /// Everything that has been answered, which is what the document writes from.
    /// </summary>
    /// <remarks>
    /// With the box ticked the chosen assembler rules over the whole batch, the data files
    /// included: the ROM brings them in with an include, and a file written with a directive
    /// that assembler does not take would stop it on a line nobody wrote.
    /// </remarks>
    private ExportRequest Request => new(
        Format.Format,
        Stem,
        WithRom ? Assembler.Style : _mainWindowVm.Preferences.AsmStyle,
        WithRom ? Assembler : null);

    [RelayCommand]
    private async Task BrowseAsync()
    {
        string? picked = await _mainWindowVm.Dialogs.PickFileToSaveAsync(
            Text["PickExportTo"], $"{Stem}{Format.Extension}", KindOf(Format.Format));

        if (picked is not null)
            Destination = picked;
    }

    [RelayCommand]
    private async Task AcceptExportAsync()
    {
        if (string.IsNullOrWhiteSpace(Destination))
        {
            ErrorMessage = Text["ExportNoDestination"];

            return;
        }

        ErrorMessage = null;

        // Before a single file is written: saying no here leaves the folder as it was.
        if (!await _document.ReadyAsync())
            return;

        try
        {
            foreach (ExportPiece piece in _document.Pieces(Request))
                await piece.Write(Path.Combine(Folder, NameOf(piece)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // In the panel and not in a dialog: the panel stays open with everything as it was,
            // which is what it takes to fix the destination and try again.
            ErrorMessage = exception.Message;

            return;
        }

        if (_document.Note(Request) is { } note)
            await _mainWindowVm.Dialogs.ShowMessageAsync(note.Title, note.Body);

        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelExport() => _mainWindowVm.RightPanViewModel = null;

    /// <summary>The destination follows the format, so that a path does not keep the old one.</summary>
    partial void OnFormatChanged(ExportChoice value)
    {
        if (!string.IsNullOrWhiteSpace(Destination))
            Destination = Path.ChangeExtension(Destination, value.Extension);

        Refresh();
    }

    partial void OnDestinationChanged(string value) => Refresh();

    /// <summary>The ROM is one more file in the list, so the list has to hear about it.</summary>
    partial void OnWantsExampleRomChanged(bool value) => Refresh();

    /// <summary>Rebuilds the list of what is going to come out.</summary>
    private void Refresh()
    {
        Files.Clear();

        foreach (ExportPiece piece in _document.Pieces(Request))
        {
            string name = NameOf(piece);

            Files.Add(new ExportFileRow(
                name, Folder.Length > 0 && File.Exists(Path.Combine(Folder, name))));
        }

        OnPropertyChanged(nameof(Overwrites));
        OnPropertyChanged(nameof(HasOverwrites));
    }

    /// <summary>
    /// What one of them is called.
    /// </summary>
    /// <remarks>
    /// With the format's extension, except for the one that brings its own: the example ROM is
    /// assembler even when what is being exported is binary.
    /// </remarks>
    private string NameOf(ExportPiece piece) =>
        $"{Stem}{piece.Suffix}{piece.Extension ?? Format.Extension}";

    private static PickerFileKind KindOf(ExportFormat format) => format switch
    {
        ExportFormat.Binary => PickerFileKind.Binary,
        ExportFormat.Png => PickerFileKind.Image,
        ExportFormat.Csv => PickerFileKind.Csv,
        _ => PickerFileKind.Assembler,
    };
}
