using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Panel de edición de una paleta: se elige un color de la lista y se ajustan sus tres
/// componentes con sendos sliders. Los cambios se aplican al momento, no hay Aceptar.
/// </summary>
/// <remarks>
/// Los intercambios de color son la excepción: mover un color de sitio deja mal todo lo
/// que estuviera pintado con él, así que se acumulan y se confirman con Aplicar, que es
/// quien reajusta los dibujos.
/// </remarks>
public partial class EditPaletteViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;

    private static Localizer Text => Localizer.Instance;

    /// <summary>Dónde ha acabado cada color desde que se abrió el panel.</summary>
    private readonly PaletteSwaps _swaps = new();

    /// <summary>Cómo estaba la paleta antes del primer intercambio, para poder deshacer.</summary>
    private PaletteColor[]? _before;

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

    /// <summary>Hay colores movidos de sitio y los dibujos aún no se han reajustado.</summary>
    public bool HasSwaps => !_swaps.IsEmpty;

    /// <summary>
    /// Intercambia dos colores de ranura y apunta el movimiento. Devuelve si se ha hecho.
    /// </summary>
    /// <remarks>
    /// La paleta cambia en el acto, así que los dibujos se ven con los colores cambiados
    /// hasta que se aplica. Es a propósito: es la única forma de ver si la colocación
    /// nueva es la que se quería antes de tocar nada.
    /// </remarks>
    public bool SwapColors(int one, int other)
    {
        // La copia antes de tocar, y sólo la primera vez: es a lo que se vuelve al
        // descartar, por muchos intercambios que se encadenen.
        PaletteColor[] before = _before ?? [.. Palette.Colors.Select(color => color.Clone())];

        if (!Palette.Swap(one, other))
            return false;

        _before = before;
        _swaps.Swap(one, other);

        NotifySwapsChanged();

        return true;
    }

    /// <summary>Devuelve la paleta a como estaba antes del primer intercambio.</summary>
    public void DiscardSwaps()
    {
        if (_before is null)
            return;

        for (int index = 0; index < _before.Length; index++)
            Palette[index].TakeFrom(_before[index]);

        Forget();
    }

    /// <summary>
    /// Si el panel se va por cualquier otra vía, los intercambios pendientes se deshacen.
    /// </summary>
    /// <remarks>
    /// Dejarlos puestos sin reajustar los dibujos es la peor de las salidas: la paleta
    /// queda bien y todo lo pintado con ella, mal, sin que se haya dicho nada.
    /// </remarks>
    public override void OnClosed() => DiscardSwaps();

    /// <summary>
    /// Acepta los colores donde están y reajusta los índices de todo lo que los usaba.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSwaps))]
    private async Task ApplyAsync()
    {
        IReadOnlyList<IPaletteDocument> affected = _mainWindowVm.DocumentsWith(Palette);

        string names = string.Join(
            ", ", affected.OfType<PanelBaseViewModel>().Select(panel => panel.Header));

        bool accepted = await _mainWindowVm.Dialogs.ConfirmAsync(
            Text["PaletteApplyTitle"],
            Text.Format("PaletteApplyBody", names),
            Text["PaletteApplyAccept"]);

        if (!accepted)
            return;

        // Una sola pasada con la permutación entera: encadenar los intercambios uno a uno
        // sobre los dibujos mandaría un color a dos sitios.
        int[] table = _swaps.Table();

        foreach (IPaletteDocument document in affected)
            document.RemapColors(table);

        Forget();
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        if (HasSwaps && !await AskWhatToDoWithSwaps())
            return;

        _mainWindowVm.RightPanViewModel = null;
    }

    /// <summary>Se cierra con colores movidos sin aplicar. Devuelve si se puede seguir.</summary>
    private async Task<bool> AskWhatToDoWithSwaps()
    {
        bool? apply = await _mainWindowVm.Dialogs.ChooseAsync(
            Text["PaletteSwapsPendingTitle"],
            Text["PaletteSwapsPendingMessage"],
            Text["PaletteSwapsApply"],
            Text["PaletteSwapsDiscard"]);

        if (apply is not bool answer)
            return false;

        if (!answer)
        {
            DiscardSwaps();

            return true;
        }

        await ApplyAsync();

        // Si se echó atrás en la confirmación, siguen pendientes y no se cierra.
        return !HasSwaps;
    }

    /// <summary>Ya no hay nada pendiente: ni movimientos que aplicar ni copia a la que volver.</summary>
    private void Forget()
    {
        _swaps.Reset();
        _before = null;

        NotifySwapsChanged();
    }

    private void NotifySwapsChanged()
    {
        OnPropertyChanged(nameof(HasSwaps));
        ApplyCommand.NotifyCanExecuteChanged();
    }
}
