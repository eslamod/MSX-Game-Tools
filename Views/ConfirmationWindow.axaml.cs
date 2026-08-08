using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MSX_SpritesEditor.Views;

public partial class ConfirmationWindow : Window
{
    public ConfirmationWindow() => InitializeComponent();

    /// <param name="showCancel">
    /// <c>false</c> lo convierte en un aviso de un solo botón, sin nada que cancelar.
    /// </param>
    public ConfirmationWindow(string title, string message, string confirmLabel, bool showCancel = true)
        : this()
    {
        Title = title;
        HeadingText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;
        CancelButton.IsVisible = showCancel;

        if (!showCancel)
        {
            ConfirmButton.IsDefault = true;
            ConfirmButton.IsCancel = true;
        }
    }

    // Cerrar por la X equivale a cancelar: ShowDialog<bool> devuelve false.
    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
