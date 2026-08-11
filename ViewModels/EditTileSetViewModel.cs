using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.ViewModels;

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

    public EditTileSetViewModel(MainWindowViewModel mainWindowVm)
    {
        _mainWindowVm = mainWindowVm;

        // Con la cabecera vacia la pestaña parecia rota.
        Header = "Agregar tileset";
        TagId = "new:tileset";
    }

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

        _mainWindowVm.OpenTileSet(new TileSet(Name));
        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelTileSet() => _mainWindowVm.RightPanViewModel = null;
}
