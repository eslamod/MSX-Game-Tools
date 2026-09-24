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
    /// <summary>Cuántos ficheros se enseñan por su nombre antes de resumir el resto.</summary>
    private const int MaxListed = 12;

    private readonly MainWindowViewModel _mainWindowVm;
    private readonly IExportDocument _document;

    private int _total;

    private int _existing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsExampleRom))]
    [NotifyPropertyChangedFor(nameof(ShowsScreens))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenOptions))]
    [NotifyPropertyChangedFor(nameof(ShowsScreenPick))]
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
    private bool _oneScreen;

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

        // Por la pantalla de lo que estuviera seleccionado: con un mapa de veinte pantallas,
        // arrancar siempre por la 1-1 es ponerse a contar.
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

    /// <summary>Lo que sólo se pregunta con las pantallas puestas: cuáles y con qué cabecera.</summary>
    public bool ShowsScreenOptions => ByScreens && ShowsScreens;

    /// <summary>Y el número, sólo cuando se ha dicho que va una sola.</summary>
    public bool ShowsScreenPick => ShowsScreenOptions && OneScreen;

    /// <summary>
    /// Lo contrario de <see cref="OneScreen"/>, para el otro botón del par.
    /// </summary>
    /// <remarks>
    /// Sólo hace caso cuando lo marcan: al marcar el otro, el grupo desmarca éste y escribe un
    /// <c>false</c> de vuelta, que aquí no significa nada.
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
    /// Los que no caben en la lista, cuando son muchos.
    /// </summary>
    /// <remarks>
    /// Un mapa de diez por diez pantallas son cien nombres, y cien nombres no se leen: lo que
    /// hace falta saber —cómo se llaman y cuántos pisan algo— se ve en los primeros y en la
    /// línea de los que ya existen, que cuenta todos.
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
        WithScreens ? new ScreenSplit(ScreenHeader, Picked) : null);

    /// <summary>La pantalla pedida, o nada cuando van todas.</summary>
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

        // Lo que impide exportar ya está escrito en el panel desde que se contestó; aquí sólo
        // hay que no seguir.
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
    /// El panel se cierra: lo que estuviera señalado deja de estar a punto de escribirse.
    /// </summary>
    /// <remarks>
    /// Aquí y no en Aceptar y Cancelar: el panel también se va cuando se abre otro encima, y
    /// entonces la marca se quedaría puesta sin nadie que la fuera a escribir.
    /// </remarks>
    public override void OnClosed() => _document.Preview(null);

    /// <summary>The destination follows the format, so that a path does not keep the old one.</summary>
    partial void OnFormatChanged(ExportChoice value)
    {
        // Y si el formato nuevo no sale por pantallas, la casilla se cae: el destino pasa de
        // ser una carpeta a ser un fichero, y dejarla puesta lo dejaría a medias.
        if (!ShowsScreens)
            ByScreens = false;

        if (!ByScreens && !string.IsNullOrWhiteSpace(Destination))
            Destination = Path.ChangeExtension(Destination, value.Extension);

        Refresh();
        ShowWhatGoesOut();
    }

    /// <summary>
    /// El destino cambia de significado con la casilla, así que se convierte.
    /// </summary>
    /// <remarks>
    /// Por pantallas es la carpeta y sin ella el fichero. Sin convertirlo, una carpeta elegida
    /// antes se leería como un fichero —la carpeta sería la de encima— y el mapa acabaría un
    /// nivel más arriba de donde se dijo.
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

    /// <summary>La cabecera cambia lo que se escribe, no cómo se llama.</summary>
    partial void OnScreenHeaderChanged(bool value) => Refresh();

    /// <summary>Y cuáles van cambia la lista entera, y lo que se señala en el mapa.</summary>
    partial void OnOneScreenChanged(bool value) => Answered();

    /// <inheritdoc cref="OnOneScreenChanged"/>
    partial void OnScreenColumnChanged(int value) => Answered();

    /// <inheritdoc cref="OnOneScreenChanged"/>
    partial void OnScreenRowChanged(int value) => Answered();

    /// <summary>La lista de lo que va a salir y, donde se vea, lo que va a salir.</summary>
    private void Answered()
    {
        Refresh();
        ShowWhatGoesOut();
    }

    /// <summary>
    /// Que el documento enseñe lo que va a salir donde se vea.
    /// </summary>
    /// <remarks>
    /// Aparte de <see cref="Refresh"/> a propósito, que se llama con cada letra del destino:
    /// señalar arrastra la vista del mapa hasta la pantalla, y eso con cada letra sería una
    /// ventana dándose saltos mientras se escribe.
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

        foreach (ExportPiece piece in _document.Pieces(Request))
        {
            string name = NameOf(piece);
            bool exists = Folder.Length > 0 && File.Exists(Path.Combine(Folder, name));

            _total++;

            if (exists)
                _existing++;

            if (Files.Count < MaxListed)
                Files.Add(new ExportFileRow(name, exists));
        }

        // Lo que impide exportar se dice aquí y no al aceptar: enterarse de que la pantalla no
        // cuadra con el supertile después de elegir carpeta es tarde.
        ErrorMessage = _document.Problem(Request);

        OnPropertyChanged(nameof(Overwrites));
        OnPropertyChanged(nameof(HasOverwrites));
        OnPropertyChanged(nameof(More));
        OnPropertyChanged(nameof(HasMore));
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
