using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

public partial class EditSpriteBankViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private SpriteBank.SpriteType _type = SpriteBank.SpriteType.MSX;

    // En WPF esto era un MessageBox.Show desde el ViewModel. Ahora el error se
    // enlaza a la propia vista: sin diálogo modal y sin acoplar VM y UI.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public EditSpriteBankViewModel(MainWindowViewModel mainWindowVm)
    {
        _mainWindowVm = mainWindowVm;

        // Con la cabecera vacia la pestaña parecia rota.
        Header = "Agregar banco de sprites";
        TagId = "new:spritebank";
    }

    public IReadOnlyList<SpriteBank.SpriteType> SpriteTypes { get; } = Enum.GetValues<SpriteBank.SpriteType>();

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private void AcceptSpriteBank()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "El nombre del banco de sprites no puede estar vacío.";
            return;
        }

        ErrorMessage = null;

        // Recien creado y vacio: no hay nada que perder todavia, asi que sale sin marcar.
        _mainWindowVm.OpenSpriteBank(new SpriteBank(Type, Name)).MarkClean();
        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelSpriteBank() => _mainWindowVm.RightPanViewModel = null;
}
