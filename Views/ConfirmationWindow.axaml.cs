using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MSX_GameTools.Views;

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

    /// <summary>
    /// Tres salidas en vez de dos: una para cada opción y una para cancelar.
    /// </summary>
    /// <remarks>
    /// Con <c>ShowDialog&lt;bool?&gt;</c>, cerrar por la X devuelve null, que es
    /// cancelar. Hace falta cuando las dos opciones hacen algo y no vale confundir
    /// "la otra" con "déjalo".
    /// </remarks>
    public ConfirmationWindow(string title, string message, string yesLabel, string noLabel, bool threeWay)
        : this(title, message, yesLabel)
    {
        AlternativeButton.Content = noLabel;
        AlternativeButton.IsVisible = threeWay;
    }

    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);

    private void OnAlternative(object? sender, RoutedEventArgs e) => Close(false);

    // Null y no false: con ShowDialog<bool?> es lo que distingue cancelar de la segunda
    // opcion, y con ShowDialog<bool> sigue llegando como false, que era el comportamiento
    // de antes. Cerrar por la X pasa por aqui igual.
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
