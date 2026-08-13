using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MSX_GameTools.Views;

public partial class EditPropertiesView : UserControl
{
    public EditPropertiesView() => InitializeComponent();

    /// <summary>
    /// El nombre queda seleccionado al abrir.
    /// </summary>
    /// <remarks>
    /// Es a lo que se viene aquí casi siempre, y así con F2 el gesto entero es pulsar y
    /// escribir. Sin esto habría que ir al cuadro con el ratón, que es justo lo que un
    /// atajo de teclado venía a ahorrar.
    /// </remarks>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        NameBox.Focus();
        NameBox.SelectAll();
    }
}
