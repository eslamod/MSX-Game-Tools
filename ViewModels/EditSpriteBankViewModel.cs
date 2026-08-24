using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

public partial class EditSpriteBankViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private SpriteBank.SpriteType _type = SpriteBank.SpriteType.MSX;

    /// <summary>
    /// Cuántos patrones va a tener el banco.
    /// </summary>
    /// <remarks>
    /// Los 64 son de la tabla de patrones de la VRAM, no del banco. Un juego que tenga los
    /// patrones en ROM y vaya redefiniendo los que necesita cada animación puede tener
    /// muchos más, y 256 son 8 KB, que en una MegaROM no es nada. Por eso se puede pasar de
    /// 64, con el aviso al lado y no en un diálogo aparte: es una decisión de cómo está
    /// hecho el juego, no un error que haya que confirmar.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverVram))]
    private int _capacity = SpriteBank.MaxSprites;

    // En WPF esto era un MessageBox.Show desde el ViewModel. Ahora el error se
    // enlaza a la propia vista: sin diálogo modal y sin acoplar VM y UI.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <inheritdoc cref="EditTileSetViewModel.Palette"/>
    [ObservableProperty]
    private ColorPalette _palette;

    public EditSpriteBankViewModel(MainWindowViewModel mainWindowVm)
    {
        _mainWindowVm = mainWindowVm;
        _palette = mainWindowVm.Palettes.ActivePalette;

        // Con la cabecera vacia la pestaña parecia rota.
        Header = Localizer.Instance["NewSpriteBankTitle"];
        TagId = "new:spritebank";
    }

    public IReadOnlyList<SpriteBank.SpriteType> SpriteTypes { get; } = Enum.GetValues<SpriteBank.SpriteType>();

    public IReadOnlyList<int> Capacities => SpriteBank.Capacities;

    /// <summary>Si el tamaño elegido ya no cabe en la tabla de patrones de la VRAM.</summary>
    public bool IsOverVram => Capacity > SpriteBank.MaxSprites;

    /// <inheritdoc cref="EditTileSetViewModel.Palettes"/>
    public IReadOnlyList<ColorPalette> Palettes => _mainWindowVm.Palettes.Palettes;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private void AcceptSpriteBank()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = Localizer.Instance["NewSpriteBankNoName"];
            return;
        }

        ErrorMessage = null;

        // Recien creado y vacio: no hay nada que perder todavia, asi que sale sin marcar.
        _mainWindowVm.OpenSpriteBank(new SpriteBank(Type, Name, Capacity), Palette).MarkClean();
        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelSpriteBank() => _mainWindowVm.RightPanViewModel = null;
}
