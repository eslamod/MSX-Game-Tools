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
            FileTypeFilter = [kind == PickerFileKind.Image ? ImageFileType : PaletteFileType],
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFileToSaveAsync(string title, string suggestedFileName)
    {
        IStorageFile? file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "json",
            FileTypeChoices = [PaletteFileType],
            ShowOverwritePrompt = true,
        });

        return file?.TryGetLocalPath();
    }
}
