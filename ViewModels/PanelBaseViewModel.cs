using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_SpritesEditor.ViewModels;

/// <summary>Base de todo panel que puede vivir en una pestaña o en el panel derecho.</summary>
public abstract partial class PanelBaseViewModel : ObservableObject
{
    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private string _tagId = string.Empty;

    /// <summary>
    /// Si el panel es una herramienta y no un documento.
    /// </summary>
    /// <remarks>
    /// Una herramienta vive en el panel de la derecha y se ve a la vez que lo que se está
    /// editando; un documento ocupa una pestaña del centro. Los bloques son herramienta:
    /// hay que verlos mientras se dibujan los tiles.
    /// </remarks>
    public virtual bool IsTool => false;
}
