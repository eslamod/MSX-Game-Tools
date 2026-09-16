using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

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
/// and, when it has them, its super tiles and its attributes. The file picker of the system
/// only warns about the one that gets named, so the other three could be overwritten without a
/// word. Here they are all listed before a single one is written.
/// </para>
/// <para>
/// And it is where the two questions that are coming will fit: whether the asm of an example
/// ROM is wanted as well, and which assembler it is for.
/// </para>
/// </remarks>
public partial class ExportViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;
    private readonly TileSetEditorViewModel _tiles;

    [ObservableProperty]
    private ExportChoice _format;

    /// <summary>Where the first of the files goes; the others take their name from it.</summary>
    [ObservableProperty]
    private string _destination = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public ExportViewModel(MainWindowViewModel mainWindowVm, TileSetEditorViewModel tiles)
    {
        _mainWindowVm = mainWindowVm;
        _tiles = tiles;

        Formats =
        [
            new(ExportFormat.Assembler, Text["ExportFormatAsm"], ".asm"),
            new(ExportFormat.Binary, Text["ExportFormatBin"], ".bin"),
            new(ExportFormat.Png, Text["ExportFormatPng"], ".png"),
        ];

        _format = Formats[0];

        Header = $"{Text["ExportTitle"]}: {tiles.DocumentName}";
        TagId = "export";

        Refresh();
    }

    private static Localizer Text => Localizer.Instance;

    /// <summary>The formats this document can come out in.</summary>
    public IReadOnlyList<ExportChoice> Formats { get; }

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
        ? SpriteBankExporter.LabelOf(_tiles.TileSet.Name)
        : Path.GetFileNameWithoutExtension(Destination);

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

        try
        {
            foreach (Piece piece in Pieces())
                await piece.Write(Path.Combine(Folder, NameOf(piece)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // In the panel and not in a dialog: the panel stays open with everything as it was,
            // which is what it takes to fix the destination and try again.
            ErrorMessage = exception.Message;

            return;
        }

        await Told();

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

    /// <summary>Rebuilds the list of what is going to come out.</summary>
    private void Refresh()
    {
        Files.Clear();

        foreach (Piece piece in Pieces())
        {
            string name = NameOf(piece);

            Files.Add(new ExportFileRow(
                name, Folder.Length > 0 && File.Exists(Path.Combine(Folder, name))));
        }

        OnPropertyChanged(nameof(Overwrites));
        OnPropertyChanged(nameof(HasOverwrites));
    }

    private string NameOf(Piece piece) => $"{Stem}{piece.Suffix}{Format.Extension}";

    /// <summary>
    /// What comes out of exporting this in this format: the end of each name and who writes it.
    /// </summary>
    /// <remarks>
    /// One list for showing and for writing. With two, the day one of them changed the panel
    /// would be promising one thing and writing another.
    /// </remarks>
    private IEnumerable<Piece> Pieces()
    {
        TileSet tileSet = _tiles.TileSet;
        AsmStyle style = _mainWindowVm.Preferences.AsmStyle;

        if (Format.Format == ExportFormat.Png)
        {
            yield return new Piece(string.Empty, path =>
            {
                PngFile.Write(
                    path,
                    TileSetPngConverter.ToPixels(tileSet, _tiles.ColorPalette),
                    TileSetPngConverter.FullSize);

                return Task.CompletedTask;
            });

            yield break;
        }

        bool binary = Format.Format == ExportFormat.Binary;

        yield return new Piece("_patterns", path => binary
            ? File.WriteAllBytesAsync(path, TileSetExporter.PatternsToBinary(tileSet))
            : File.WriteAllTextAsync(path, TileSetExporter.PatternsToAssembler(tileSet, style)));

        yield return new Piece("_colors", path => binary
            ? File.WriteAllBytesAsync(path, TileSetExporter.ColorsToBinary(tileSet))
            : File.WriteAllTextAsync(path, TileSetExporter.ColorsToAssembler(tileSet, style)));

        // La tabla de supertiles sale con el juego y no con el mapa: es del juego, y todos
        // los mapas dibujados con el comparten la misma. Con cada mapa se repetiria igual.
        if (tileSet.HasSuperTiles)
        {
            yield return new Piece("_supertiles", path => binary
                ? File.WriteAllBytesAsync(path, SuperTileExporter.ToBinary(tileSet))
                : File.WriteAllTextAsync(path, SuperTileExporter.ToAssembler(tileSet, style)));
        }

        // Y la de atributos sólo si se han definido: quien no los usa no tiene por qué
        // encontrarse un fichero de 256 ceros que no sabe para qué es.
        if (tileSet.AttributeNames.Any)
        {
            yield return new Piece("_attributes", path => binary
                ? File.WriteAllBytesAsync(path, TileSetExporter.AttributesToBinary(tileSet))
                : File.WriteAllTextAsync(path, TileSetExporter.AttributesToAssembler(tileSet, style)));
        }
    }

    /// <summary>
    /// What has to be known after writing it, which is not in the files themselves.
    /// </summary>
    /// <remarks>
    /// The three copies in VRAM and the ceiling of super tiles a map can name: two things that
    /// are nowhere in what was exported, and without which nobody knows what to do with it.
    /// </remarks>
    private async Task Told()
    {
        if (Format.Format == ExportFormat.Png)
            return;

        TileSet tileSet = _tiles.TileSet;

        string done = Text.Format(
            "ExportedTileSetBody",
            $"{Stem}_patterns{Format.Extension}",
            $"{Stem}_colors{Format.Extension}",
            TileSetExporter.ScreenThirds);

        if (tileSet.HasSuperTiles)
        {
            done += " " + Text.Format(
                "ExportedSuperTiles",
                $"{Stem}_supertiles{Format.Extension}",
                SuperTileExporter.CountOf(tileSet));

            // Una celda del mapa es un byte, asi que de 256 para arriba hay supertiles que
            // ningun mapa puede nombrar. Mejor decirlo que dejar una tabla que no cuadra.
            if (tileSet.Blocks.Count > SuperTileExporter.MaxSuperTiles)
            {
                done += " " + Text.Format(
                    "ExportedSuperTilesTooMany", tileSet.Blocks.Count, SuperTileExporter.MaxSuperTiles);
            }
        }

        await _mainWindowVm.Dialogs.ShowMessageAsync(Text["ExportedTileSetTitle"], done);
    }

    private static PickerFileKind KindOf(ExportFormat format) => format switch
    {
        ExportFormat.Binary => PickerFileKind.Binary,
        ExportFormat.Png => PickerFileKind.Image,
        _ => PickerFileKind.Assembler,
    };

    /// <summary>One of the files going out: how its name ends, and who writes it.</summary>
    private sealed record Piece(string Suffix, Func<string, Task> Write);
}
