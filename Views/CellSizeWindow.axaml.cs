using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MSX_GameTools.Views;

/// <summary>
/// Pregunta con qué lado de celda trocear una hoja de sprites.
/// </summary>
public partial class CellSizeWindow : Window
{
    public CellSizeWindow() => InitializeComponent();

    public CellSizeWindow(string title, string message, int suggested, int maximum)
        : this()
    {
        Title = title;
        HeadingText.Text = title;
        MessageText.Text = message;

        SizeBox.Maximum = maximum;
        SizeBox.Value = Math.Clamp(suggested, 1, maximum);
    }

    /// <summary>Cerrar por la X o cancelar devuelve <c>null</c>: no se carga nada.</summary>
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private void OnConfirm(object? sender, RoutedEventArgs e) =>
        Close(SizeBox.Value is { } value ? (int?)value : null);

    private void OnPreset(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out int size))
            SizeBox.Value = Math.Min(size, SizeBox.Maximum);
    }
}
