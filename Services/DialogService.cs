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

    public async Task<string?> PickFileToOpenAsync(string title)
    {
        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [PaletteFileType],
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
