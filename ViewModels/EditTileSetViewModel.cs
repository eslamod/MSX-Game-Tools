using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

/// <summary>Formulario de creación de un juego de tiles.</summary>
/// <remarks>
/// Sólo pide el nombre. A diferencia de un banco de sprites no hay tipo que elegir: el
/// formato de un tile es el mismo en GRAPHIC 2 y en GRAPHIC 3, y lo único que cambia en
/// MSX2 es que los 16 colores se pueden redefinir, que ya es cosa de la paleta activa.
/// </remarks>
public partial class EditTileSetViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>Con qué colores se dibuja, que el juego guarda dentro de su fichero.</summary>
    [ObservableProperty]
    private ColorPalette _palette;

    public EditTileSetViewModel(MainWindowViewModel mainWindowVm)
    {
        _mainWindowVm = mainWindowVm;
        _palette = mainWindowVm.Palettes.ActivePalette;

        // Con la cabecera vacia la pestaña parecia rota.
        Header = Localizer.Instance["NewTileSetTitle"];
        TagId = "new:tileset";
    }

    /// <summary>
    /// Las paletas del proyecto, para elegir con cuál nace el juego.
    /// </summary>
    /// <remarks>
    /// Se pregunta aquí en vez de darle la que enseñe la barra y ya está: la paleta va
    /// dentro del fichero del juego, así que es una decisión suya, y hacerla a escondidas
    /// dejaba al usuario sin saber de dónde le había salido.
    /// </remarks>
    public IReadOnlyList<ColorPalette> Palettes => _mainWindowVm.Palettes.Palettes;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private void AcceptTileSet()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "El nombre del juego de tiles no puede estar vacío.";
            return;
        }

        ErrorMessage = null;

        // Recien creado y vacio: no hay nada que perder todavia, asi que sale sin marcar.
        _mainWindowVm.OpenTileSet(new TileSet(Name), Palette).MarkClean();
        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelTileSet() => _mainWindowVm.RightPanViewModel = null;
}
