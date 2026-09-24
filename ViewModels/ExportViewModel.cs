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

/// <summary>One of the page sizes a mapper can have, with what it is read as.</summary>
/// <param name="Bytes">How much a page holds, or 0 for a cartridge without a mapper.</param>
public sealed record PageChoice(string Label, int Bytes);

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
    /// <summary>How many files are shown by name before the rest is summed up.</summary>
    private const int MaxListed = 12;

    private readonly MainWindowViewModel _mainWindowVm;
    private readonly IExportDocument _document;

    private int _total;

    private int _existing;

    /// <summary>The files an earlier export left in the folder that this one does not write.</summary>
    private IReadOnlyList<string> _leftovers = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsExampleRom))]
    [NotifyPropertyChangedFor(nameof(ShowsScreens))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenOptions))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenPick))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenIndex))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenPages))]
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

    /// <summary>
    /// Whether the map goes out cut into the screens of the game, one file each.
    /// </summary>
    /// <remarks>
    /// Off by default: a map is one file, and whoever is not making a game of fixed screens
    /// would find a folder full of pieces of the thing they asked for.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsExampleRom))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenOptions))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenPick))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenIndex))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenPages))]
    private bool _byScreens;

    /// <summary>
    /// Whether every screen carries the four bytes of its size in front.
    /// </summary>
    /// <remarks>
    /// Off by default: the screens of a game all measure the same, so it is the same four bytes
    /// in every file. Ticked for whoever loads them with the code that reads a whole map.
    /// </remarks>
    [ObservableProperty]
    private bool _screenHeader;

    /// <summary>
    /// Whether only one screen goes out instead of every screen with something on it.
    /// </summary>
    /// <remarks>
    /// What it is for is touching one screen and writing that one again: the other twenty are
    /// already out there, and rewriting them all turns a change of one room into twenty files
    /// with a new date on them.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllScreens))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenPick))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenIndex))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenPages))]
    private bool _oneScreen;

    /// <summary>
    /// Whether the table of where each screen starts goes out with them.
    /// </summary>
    /// <remarks>
    /// On by default: whoever cuts a map into screens is going to need to find them from the
    /// game, and writing that table by hand is a line per screen to keep in step with the map.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsScreenPages))]
    private bool _screenIndex = true;

    /// <summary>
    /// The pages of the mapper the screens are shared into, or none.
    /// </summary>
    /// <remarks>
    /// None by default: it is only for a megaROM, and whoever has one knows the size of their
    /// pages. With 8K or 16K the index says of each screen its page and its offset, which is
    /// what is needed when every page is seen through the same window.
    /// </remarks>
    [ObservableProperty]
    private PageChoice _screenPages;

    /// <summary>Which one, column and row, counting from one as the editor reads it.</summary>
    [ObservableProperty]
    private int _screenColumn = 1;

    /// <inheritdoc cref="ScreenColumn"/>
    [ObservableProperty]
    private int _screenRow = 1;

    public ExportViewModel(MainWindowViewModel mainWindowVm, IExportDocument document)
    {
        _mainWindowVm = mainWindowVm;
        _document = document;

        _format = document.Formats[0];
        _screenPages = PageSizes[0];

        // On the screen of whatever was selected: with a map of twenty screens, always starting
        // on 1-1 means counting.
        if (document.FirstScreen is { } screen)
        {
            _screenColumn = screen.Column;
            _screenRow = screen.Row;
        }

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
        && !ByScreens
        && Format.Format is ExportFormat.Assembler or ExportFormat.Binary;

    /// <summary>
    /// Cutting into screens is only offered for what has screens and in the two formats a
    /// machine reads.
    /// </summary>
    /// <remarks>
    /// Not for the csv: it is for opening in a spreadsheet, and twenty spreadsheets of a map
    /// are not easier to read than one.
    /// </remarks>
    public bool ShowsScreens => _document.FirstScreen is not null
        && Format.Format is ExportFormat.Assembler or ExportFormat.Binary;

    /// <summary>What is only asked with the screens on: which ones, and with what header.</summary>
    public bool ShowsScreenOptions => ByScreens && ShowsScreens;

    /// <summary>And the number, only once it has been said that just one goes.</summary>
    public bool ShowsScreenPick => ShowsScreenOptions && OneScreen;

    /// <summary>
    /// The index is only offered with all of them.
    /// </summary>
    /// <remarks>
    /// It speaks of the whole batch. Written with a single screen it would say that the others
    /// are not there, and they are: they went out before.
    /// </remarks>
    public bool ShowsScreenIndex => ShowsScreenOptions && !OneScreen;

    /// <summary>The pages only matter to the index, so they are asked with it.</summary>
    public bool ShowsScreenPages => ShowsScreenIndex && ScreenIndex;

    /// <summary>The sizes a page of the mapper can have, and none for a cartridge without one.</summary>
    public IReadOnlyList<PageChoice> PageSizes { get; } =
    [
        new(Text["ExportPagesNone"], 0),
        new("8K", 8 * 1024),
        new("16K", 16 * 1024),
    ];

    /// <summary>
    /// The opposite of <see cref="OneScreen"/>, for the other button of the pair.
    /// </summary>
    /// <remarks>
    /// It only listens when it gets ticked: ticking the other one, the group unticks this one and
    /// writes a <c>false</c> back, which means nothing here.
    /// </remarks>
    public bool AllScreens
    {
        get => !OneScreen;

        set
        {
            if (value)
                OneScreen = false;
        }
    }

    /// <summary>The files that are going to be written, with their names already worked out.</summary>
    public ObservableCollection<ExportFileRow> Files { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// The ones that do not fit in the list, when there are many.
    /// </summary>
    /// <remarks>
    /// A map of ten by ten screens is a hundred names, and a hundred names are not read: what
    /// needs knowing —what they are called and how many land on something— shows in the first
    /// ones and in the line of the ones already there, which counts them all.
    /// </remarks>
    public string? More =>
        _total > Files.Count ? Text.Format("ExportMoreFiles", _total - Files.Count) : null;

    public bool HasMore => More is not null;

    /// <summary>
    /// How many of the files going out land on one that is already there.
    /// </summary>
    /// <remarks>
    /// This is what the file picker of the system cannot say: it only asks about the file that
    /// gets named, and a tile set writes up to four.
    /// </remarks>
    public string? Overwrites =>
        _existing == 0 ? null : Text.Format("ExportOverwrites", _existing, _total);

    public bool HasOverwrites => Overwrites is not null;

    /// <summary>
    /// The files of the same family that an earlier export left and this one neither writes nor
    /// deletes.
    /// </summary>
    /// <remarks>
    /// Said and not deleted: the folder is the user's, and a file this export does not know it
    /// wrote is not one to take away. But left unsaid, a data file of a flat export next to the
    /// table of a paged one makes it anyone's guess which one goes with which.
    /// </remarks>
    public string? Leftovers =>
        _leftovers.Count == 0 ? null : Text.Format("ExportLeftovers", string.Join(", ", _leftovers));

    public bool HasLeftovers => Leftovers is not null;

    /// <summary>
    /// The chosen folder, or nothing while there is none.
    /// </summary>
    /// <remarks>
    /// By screens the destination is the folder itself: the names are the screens' own and not
    /// one of them is the one that would have been written there.
    /// </remarks>
    private string Folder
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Destination))
                return string.Empty;

            return ByScreens ? Destination : Path.GetDirectoryName(Destination) ?? string.Empty;
        }
    }

    /// <summary>
    /// The base name the others come from.
    /// </summary>
    /// <remarks>
    /// The document's own while no destination has been chosen, so that the list says something
    /// from the moment the panel opens; and the chosen one as soon as there is one, which is
    /// what the export already did.
    /// </remarks>
    private string Stem => ByScreens || string.IsNullOrWhiteSpace(Destination)
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
    /// Whether it goes out by screens: the box ticked, and a format it makes sense for.
    /// </summary>
    /// <remarks>
    /// Same as the ROM: ticked and then the format changed, the box stays as it was and this
    /// does not.
    /// </remarks>
    private bool WithScreens => ByScreens && ShowsScreens;

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
        WithRom ? Assembler : null,
        WithScreens
            ? new ScreenSplit(ScreenHeader, Picked, ScreenIndex && !OneScreen, ScreenPages.Bytes)
            : null,
        Folder);

    /// <summary>The screen asked for, or nothing when all of them go.</summary>
    private ScreenNumber? Picked => OneScreen ? new ScreenNumber(ScreenColumn, ScreenRow) : null;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        string? picked = ByScreens
            ? await _mainWindowVm.Dialogs.PickFolderAsync(Text["PickExportFolder"])
            : await _mainWindowVm.Dialogs.PickFileToSaveAsync(
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

        // What stops the export is already written in the panel since it was answered; here all
        // there is to do is not go on.
        if (_document.Problem(Request) is { } problem)
        {
            ErrorMessage = problem;

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

    /// <summary>
    /// The panel closes: whatever was marked is no longer about to be written.
    /// </summary>
    /// <remarks>
    /// Here and not in Accept and Cancel: the panel also goes when another one opens on top of
    /// it, and then the mark would stay on with nobody left to write it.
    /// </remarks>
    public override void OnClosed() => _document.Preview(null);

    /// <summary>The destination follows the format, so that a path does not keep the old one.</summary>
    partial void OnFormatChanged(ExportChoice value)
    {
        // And if the new format does not go out by screens, the box drops: the destination goes
        // from being a folder to being a file, and leaving it ticked would leave it half-way.
        if (!ShowsScreens)
            ByScreens = false;

        if (!ByScreens && !string.IsNullOrWhiteSpace(Destination))
            Destination = Path.ChangeExtension(Destination, value.Extension);

        Refresh();
        ShowWhatGoesOut();
    }

    /// <summary>
    /// The destination changes meaning with the box, so it gets converted.
    /// </summary>
    /// <remarks>
    /// By screens it is the folder, and without them the file. Left as it was, a folder chosen
    /// before would be read as a file —the folder would be the one above— and the map would end
    /// up one level higher than where it was said.
    /// </remarks>
    partial void OnByScreensChanged(bool value)
    {
        if (!string.IsNullOrWhiteSpace(Destination))
        {
            Destination = value
                ? Path.GetDirectoryName(Destination) ?? Destination
                : Path.Combine(Destination, $"{_document.Stem}{Format.Extension}");
        }

        Refresh();
        ShowWhatGoesOut();
    }

    /// <summary>The header changes what is written, not what it is called.</summary>
    partial void OnScreenHeaderChanged(bool value) => Refresh();

    /// <summary>The index puts two files in the list, or takes them out.</summary>
    partial void OnScreenIndexChanged(bool value) => Refresh();

    /// <summary>And the pages change which files go with it: a table and one per page.</summary>
    partial void OnScreenPagesChanged(PageChoice value) => Refresh();

    /// <summary>And which ones go changes the whole list, and what is marked on the map.</summary>
    partial void OnOneScreenChanged(bool value) => Answered();

    /// <inheritdoc cref="OnOneScreenChanged"/>
    partial void OnScreenColumnChanged(int value) => Answered();

    /// <inheritdoc cref="OnOneScreenChanged"/>
    partial void OnScreenRowChanged(int value) => Answered();

    /// <summary>The list of what is going out and, where it can be seen, what is going out.</summary>
    private void Answered()
    {
        Refresh();
        ShowWhatGoesOut();
    }

    /// <summary>
    /// That the document shows what is going out where it can be seen.
    /// </summary>
    /// <remarks>
    /// Apart from <see cref="Refresh"/> on purpose, which runs with every letter of the
    /// destination: marking drags the view of the map to the screen, and that with every letter
    /// would be a window jumping about while typing.
    /// </remarks>
    private void ShowWhatGoesOut() => _document.Preview(Request);

    partial void OnDestinationChanged(string value) => Refresh();

    /// <summary>The ROM is one more file in the list, so the list has to hear about it.</summary>
    partial void OnWantsExampleRomChanged(bool value) => Refresh();

    /// <summary>Rebuilds the list of what is going to come out.</summary>
    private void Refresh()
    {
        Files.Clear();

        _total = 0;
        _existing = 0;

        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ExportPiece piece in _document.Pieces(Request))
        {
            string name = NameOf(piece);
            bool exists = Folder.Length > 0 && File.Exists(Path.Combine(Folder, name));

            written.Add(name);
            _total++;

            if (exists)
                _existing++;

            if (Files.Count < MaxListed)
                Files.Add(new ExportFileRow(name, exists));
        }

        // What stops the export is said here and not on accepting: finding out that the screen
        // does not fit the super tile after choosing a folder is too late.
        ErrorMessage = _document.Problem(Request);

        _leftovers = LeftoversIn(written);

        OnPropertyChanged(nameof(Overwrites));
        OnPropertyChanged(nameof(HasOverwrites));
        OnPropertyChanged(nameof(More));
        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(Leftovers));
        OnPropertyChanged(nameof(HasLeftovers));
    }

    /// <summary>
    /// What of the family of this export is already in the folder and is not about to be written.
    /// </summary>
    private IReadOnlyList<string> LeftoversIn(HashSet<string> written)
    {
        if (Folder.Length == 0 || !Directory.Exists(Folder))
            return [];

        try
        {
            return
            [
                .. _document.Family(Request)
                    .SelectMany(pattern => Directory.EnumerateFiles(Folder, pattern))
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Where(name => !written.Contains(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase),
            ];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be read is not a reason to say anything about what is in it.
            return [];
        }
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
