using Avalonia.Controls;
using MSX_SpritesEditor.Views;

namespace MSX_SpritesEditor.Services;

/// <summary>Muestra los diálogos sobre la ventana principal.</summary>
public sealed class DialogService(Window owner) : IDialogService
{
    public async Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
    {
        var dialog = new ConfirmationWindow(title, message, confirmLabel);

        return await dialog.ShowDialog<bool>(owner);
    }
}
