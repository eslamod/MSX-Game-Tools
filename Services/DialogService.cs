using Avalonia.Controls;
using Avalonia.Platform.Storage;
using MSX_SpritesEditor.Views;

namespace MSX_SpritesEditor.Services;

/// <summary>Muestra los diálogos sobre la ventana principal.</summary>
public sealed class DialogService(Window owner) : IDialogService
{
    private static FilePickerFileType PaletteFileType => new("Paleta MSX")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    private static FilePickerFileType ImageFileType => new("Imagen")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif"],
        MimeTypes = ["image/*"],
    };

    public async Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
    {
        var dialog = new ConfirmationWindow(title, message, confirmLabel);

        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ConfirmationWindow(title, message, "Aceptar", showCancel: false);

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
            FileTypeFilter = [TypeOf(kind)],
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
            FileTypeChoices = kind == PickerFileKind.Any ? null : [TypeOf(kind)],
            ShowOverwritePrompt = true,
        });

        return file?.TryGetLocalPath();
    }

    public async Task<bool?> ChooseAsync(string title, string message, string firstLabel, string secondLabel)
    {
        var dialog = new ConfirmationWindow(title, message, firstLabel, secondLabel, threeWay: true);

        return await dialog.ShowDialog<bool?>(owner);
    }

    private static FilePickerFileType TypeOf(PickerFileKind kind) =>
        kind == PickerFileKind.Image ? ImageFileType : PaletteFileType;
}
