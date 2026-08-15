using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Formulario de creación de una paleta.
/// </summary>
/// <remarks>
/// Pregunta de cuál se copia y no clona sin más la que esté seleccionada: los 16 colores
/// de partida son medio trabajo hecho, y la que quieres de base rara vez es la que
/// resulta que estabas mirando. La del MSX sirve de origen como cualquier otra, aunque
/// sus nombres no viajen con la copia: ésos son de la máquina.
/// </remarks>
public partial class NewPaletteViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private ColorPalette _source;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public NewPaletteViewModel(MainWindowViewModel mainWindowVm)
    {
        _mainWindowVm = mainWindowVm;

        // La cabecera se fija al abrir, así que un cambio de idioma con él ya abierto no
        // la mueve. Se abren y se cierran en un momento; no compensa más.
        Header = Localizer.Instance["NewPaletteTitle"];
        TagId = "new:palette";

        _name = mainWindowVm.Palettes.SuggestName();

        // Tal como están al abrir el formulario, como en el de mapas.
        Sources = [.. mainWindowVm.Palettes.Palettes];
        _source = mainWindowVm.Palettes.ActivePalette;
    }

    /// <summary>Las paletas entre las que elegir el origen.</summary>
    public IReadOnlyList<ColorPalette> Sources { get; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private void AcceptPalette()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = Localizer.Instance["NewPaletteNoName"];
            return;
        }

        if (_mainWindowVm.Palettes.Palettes.Any(palette => palette.Name == Name))
        {
            ErrorMessage = Localizer.Instance.Format("NewPaletteRepeated", Name);
            return;
        }

        ErrorMessage = null;

        // El formulario fuera antes de abrir el editor: si no, se queda en la lista de
        // paneles del lateral y el siguiente «crear» reabre éste, con el nombre que
        // proponía entonces y que ya está cogido.
        _mainWindowVm.RightPanViewModel = null;

        _mainWindowVm.CreatePalette(Name, Source);
    }

    [RelayCommand]
    private void CancelPalette() => _mainWindowVm.RightPanViewModel = null;
}
