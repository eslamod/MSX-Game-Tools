using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MSX_SpritesEditor.Views;

public partial class ConfirmationWindow : Window
{
    public ConfirmationWindow() => InitializeComponent();

    public ConfirmationWindow(string title, string message, string confirmLabel)
        : this()
    {
        Title = title;
        HeadingText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;
    }

    // Cerrar por la X equivale a cancelar: ShowDialog<bool> devuelve false.
    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
