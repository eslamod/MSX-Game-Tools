using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.ViewModels;

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
        => _mainWindowVm = mainWindowVm;

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

        var bank = new SpriteBank(Type);
        var panel = new SpritesEditorViewModel(bank, _mainWindowVm.Palettes)
        {
            TagId = $"spb{_mainWindowVm.CurrentSpriteBankCounter}",
            Header = $"{Name} (SP)",
        };
        _mainWindowVm.CurrentSpriteBankCounter++;

        _mainWindowVm.AddPanelToDic(panel);
        _mainWindowVm.Tabs.Add(panel);
        _mainWindowVm.SelectedTab = panel;
        _mainWindowVm.TreeGeneralVm.AddSpriteBank(panel.Header, panel.TagId, panel);

        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelSpriteBank() => _mainWindowVm.RightPanViewModel = null;
}
