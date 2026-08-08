using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.ViewModels;

/// <summary>
/// Panel de edición de una paleta: se elige un color de la lista y se ajustan sus tres
/// componentes con sendos sliders. Los cambios se aplican al momento, no hay Aceptar.
/// </summary>
public partial class EditPaletteViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;

    [ObservableProperty]
    private PaletteColor _selectedColor;

    public EditPaletteViewModel(MainWindowViewModel mainWindowVm, ColorPalette palette)
    {
        _mainWindowVm = mainWindowVm;
        Palette = palette;

        // El 0 es transparente y no se puede editar: se arranca en el 1.
        _selectedColor = palette[1];

        Header = palette.Name;
        TagId = $"palette:{palette.Name}";
    }

    public ColorPalette Palette { get; }

    [RelayCommand]
    private void Close() => _mainWindowVm.RightPanViewModel = null;
}
