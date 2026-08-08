using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_SpritesEditor.ViewModels;

/// <summary>Base de todo panel que puede vivir en una pestaña o en el panel derecho.</summary>
public abstract partial class PanelBaseViewModel : ObservableObject
{
    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private string _tagId = string.Empty;
}
