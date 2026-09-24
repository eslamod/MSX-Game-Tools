using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MSX_GameTools.Localization;
using MSX_GameTools.Views;

namespace MSX_GameTools.Services;

/// <summary>Muestra los diálogos sobre la ventana principal.</summary>
public sealed class DialogService(Window owner) : IDialogService
{
    /// <summary>
    /// Los ficheros del editor: paletas, bancos, juegos de tiles y mapas.
    /// </summary>
    /// <remarks>
    /// Se llamaba «Paleta MSX» porque fue lo primero que se guardó, y desde entonces salía
    /// eso mismo al abrir un mapa o un tileset.
    /// </remarks>
    private static FilePickerFileType EditorFileType => new(Localizer.Instance["FilterEditorFiles"])
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    private static FilePickerFileType ProjectFileType => new(Localizer.Instance["FilterProject"])
    {
        Patterns = [$"*{ProjectSerializer.Extension}"],
    };

    private static FilePickerFileType ImageFileType => new(Localizer.Instance["FilterImage"])
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif"],
        MimeTypes = ["image/*"],
    };

    /// <summary>
    /// Lo que sale de exportar, cada formato con su filtro.
    /// </summary>
    /// <remarks>
    /// Se abrían con el de los ficheros del editor, así que al exportar a ensamblador el
    /// selector proponía un nombre acabado en <c>.asm</c> y a la vez enseñaba sólo los
    /// json, sin un solo <c>.asm</c> a la vista.
    /// </remarks>
    private static FilePickerFileType AssemblerFileType => new(Localizer.Instance["FilterAssembler"])
    {
        Patterns = ["*.asm"],
    };

    private static FilePickerFileType BinaryFileType => new(Localizer.Instance["FilterBinary"])
    {
        Patterns = ["*.bin"],
    };

    /// <summary>Se escribe igual en los tres idiomas, así que no pasa por el diccionario.</summary>
    private static FilePickerFileType CsvFileType => new("CSV")
    {
        Patterns = ["*.csv"],
        MimeTypes = ["text/csv"],
    };

    public async Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
    {
        var dialog = new ConfirmationWindow(title, message, confirmLabel);

        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ConfirmationWindow(title, message, Localizer.Instance["FormAccept"], showCancel: false);

        await dialog.ShowDialog(owner);
    }

    public async Task<int?> AskCellSizeAsync(string title, string message, int suggested, int maximum)
    {
        var dialog = new CellSizeWindow(title, message, suggested, maximum);

        return await dialog.ShowDialog<int?>(owner);
    }

    public async Task<int?> PickReferenceCellAsync(Entities.ReferenceImage image, int currentCell)
    {
        var dialog = new ReferenceCellWindow(image, currentCell);

        return await dialog.ShowDialog<int?>(owner);
    }

    public async Task<string?> PickFileToOpenAsync(string title, PickerFileKind kind = PickerFileKind.Json)
    {
        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = FiltersFor(kind),
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFileToSaveAsync(
        string title, string suggestedFileName, PickerFileKind kind = PickerFileKind.Json)
    {
        IStorageFile? file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = Path.GetExtension(suggestedFileName).TrimStart('.'),
            FileTypeChoices = FiltersFor(kind),
            ShowOverwritePrompt = true,
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        IReadOnlyList<IStorageFolder> folders = await owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<bool?> ChooseAsync(string title, string message, string firstLabel, string secondLabel)
    {
        var dialog = new ConfirmationWindow(title, message, firstLabel, secondLabel, threeWay: true);

        return await dialog.ShowDialog<bool?>(owner);
    }

    /// <summary>
    /// Qué tipos ofrece el diálogo, o <c>null</c> para no filtrar nada.
    /// </summary>
    /// <remarks>
    /// <c>Any</c> significa cualquier fichero, y hay que decirlo con un <c>null</c>: al
    /// abrir, el filtro se aplicaba siempre, así que importar un csv acababa enseñando
    /// sólo los json del editor.
    /// </remarks>
    public static IReadOnlyList<FilePickerFileType>? FiltersFor(PickerFileKind kind) => kind switch
    {
        PickerFileKind.Any => null,
        PickerFileKind.Image => [ImageFileType],
        PickerFileKind.Project => [ProjectFileType],
        PickerFileKind.Assembler => [AssemblerFileType],
        PickerFileKind.Binary => [BinaryFileType],
        PickerFileKind.Csv => [CsvFileType],
        _ => [EditorFileType],
    };
}
