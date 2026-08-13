using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;

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

        // Recien creado y vacio: no hay nada que perder todavia, asi que sale sin marcar.
        _mainWindowVm.OpenTileSet(new TileSet(Name)).MarkClean();
        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelTileSet() => _mainWindowVm.RightPanViewModel = null;
}
