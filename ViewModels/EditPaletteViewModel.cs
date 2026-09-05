using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using Avalonia.Media;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

/// <summary>Una ranura de la paleta con la que se compara.</summary>
/// <param name="Same">Si en esa ranura las dos paletas tienen el mismo color.</param>
public sealed record ComparedColor(int Index, IBrush Brush, string Hex, bool Same);

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

    /// <summary>La vuelta del último ajuste aplicado, o nada si no hay ninguno.</summary>
    private int[]? _appliedBack;

    /// <summary>
    /// A quién se le aplicó, para deshacérselo a ésos y no a otros.
    /// </summary>
    /// <remarks>
    /// Guardado y no vuelto a preguntar: un documento abierto después del ajuste no pasó
    /// por él, y reajustarlo al revés le movería los colores que nadie le había movido.
    /// </remarks>
    private IReadOnlyList<IPaletteDocument> _appliedTo = [];

    [ObservableProperty]
    private PaletteColor _selectedColor;

    /// <summary>
    /// La paleta que se enseña al lado para compararla, o nada.
    /// </summary>
    /// <remarks>
    /// Aquí y no en un panel aparte: comparar sirve para mover colores hasta que dos paletas
    /// se parezcan, y mover colores es justo lo que hace este panel. En uno aparte habría que
    /// ir y volver a cada arrastre.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComparing))]
    [NotifyPropertyChangedFor(nameof(ComparedColors))]
    private ColorPalette? _compared;

    public EditPaletteViewModel(MainWindowViewModel mainWindowVm, ColorPalette palette)
    {
        _mainWindowVm = mainWindowVm;
        Palette = palette;

        // El 0 es transparente y no se puede editar: se arranca en el 1.
        _selectedColor = palette[1];

        Header = palette.Name;
        TagId = $"palette:{palette.Name}";

        // Editar un color o moverlo de sitio cambia cuáles coinciden con la de al lado, y
        // esa marca es justo lo que se está mirando mientras se arrastra.
        palette.ColorsChanged += _ => OnPropertyChanged(nameof(ComparedColors));
    }

    public ColorPalette Palette { get; }

    /// <summary>Las otras paletas, que son con las que tiene sentido compararse.</summary>
    public IReadOnlyList<ColorPalette> Comparisons =>
        [.. _mainWindowVm.Palettes.Palettes.Where(other => !ReferenceEquals(other, Palette))];

    public bool IsComparing => Compared is not null;

    /// <summary>
    /// La paleta de al lado, ranura a ranura y con cuáles ya coinciden.
    /// </summary>
    /// <remarks>
    /// Por índice y no por color, que es como se compara de verdad: lo que se quiere saber es
    /// qué hay en el 4 aquí y qué hay en el 4 allí, porque es lo que decide qué mover.
    /// </remarks>
    public IReadOnlyList<ComparedColor> ComparedColors
    {
        get
        {
            if (Compared is not { } other)
                return [];

            return
            [
                .. Enumerable.Range(0, ColorPalette.Size).Select(index => new ComparedColor(
                    index,
                    other.GetBrush(index),
                    other[index].HexRgb,
                    other.GetColor(index) == Palette.GetColor(index))),
            ];
        }
    }

    /// <summary>Deja de comparar.</summary>
    [RelayCommand]
    private void StopComparing() => Compared = null;

    /// <summary>Hay colores movidos de sitio y los dibujos aún no se han reajustado.</summary>
    public bool HasSwaps => !_swaps.IsEmpty;

    /// <summary>Hay un ajuste aplicado que todavía se puede deshacer.</summary>
    /// <remarks>
    /// Con movimientos pendientes no: a mitad de camino «deshacer» sería ambiguo —¿lo
    /// pendiente o lo aplicado?— y el botón que sale ahí es el de aplicar.
    /// </remarks>
    public bool CanUndoApplied => _appliedBack is not null && !HasSwaps;

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
        if (!Palette.Swap(one, other))
            return false;

        _swaps.Swap(one, other);

        NotifySwapsChanged();

        return true;
    }

    /// <summary>Devuelve cada color movido a la ranura de la que salió.</summary>
    /// <remarks>
    /// Por la permutación al revés y no por una copia de cómo estaba la paleta antes del
    /// primer intercambio: con la copia, deshacer se llevaba por delante <b>todo</b> lo
    /// hecho desde entonces. Si movías un color y luego lo retocabas, al deshacer volvía
    /// el de antes con su nombre viejo y el retoque desaparecía sin haber avisado.
    /// </remarks>
    public void DiscardSwaps()
    {
        if (_swaps.IsEmpty)
            return;

        MoveColorsBack(_swaps.Back());

        Forget();
    }

    /// <summary>Devuelve cada color a la ranura de la que salió, con lo retocado puesto.</summary>
    private void MoveColorsBack(IReadOnlyList<int> back)
    {
        // Copia de cómo están ahora, con los retoques puestos, para poder repartirlos sin
        // pisar los que aún no se han movido.
        PaletteColor[] moved = [.. Palette.Colors.Select(color => color.Clone())];

        for (int slot = 0; slot < moved.Length; slot++)
            Palette[back[slot]].TakeFrom(moved[slot]);
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

        // Con qué deshacerlo, que hasta ahora aplicar no tenía marcha atrás y toca varios
        // documentos de golpe.
        _appliedBack = _swaps.Back();
        _appliedTo = affected;

        Forget();
    }

    /// <summary>
    /// Devuelve el último ajuste aplicado: los colores a sus ranuras y los dibujos a sus
    /// índices.
    /// </summary>
    /// <remarks>
    /// De un tiro y sin preguntar, que para eso es deshacer, y sin rehacer: como el
    /// estampado del juego de tiles. Se pierde al cerrar el panel.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanUndoApplied))]
    private void UndoApplied()
    {
        if (_appliedBack is null)
            return;

        // La paleta antes que los dibujos: reajustarlos es lo que los repinta, y así lo
        // hacen ya con los colores en su sitio.
        MoveColorsBack(_appliedBack);

        // Sólo a los que siguen abiertos con esta paleta: uno cerrado por el camino ya no
        // se ve, y uno abierto después no pasó por el ajuste.
        IReadOnlyList<IPaletteDocument> open = _mainWindowVm.DocumentsWith(Palette);

        foreach (IPaletteDocument document in _appliedTo.Where(open.Contains))
            document.RemapColors(_appliedBack);

        _appliedBack = null;
        _appliedTo = [];

        NotifySwapsChanged();
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

    /// <summary>Ya no hay movimientos pendientes.</summary>
    private void Forget()
    {
        _swaps.Reset();

        NotifySwapsChanged();
    }

    private void NotifySwapsChanged()
    {
        OnPropertyChanged(nameof(HasSwaps));
        OnPropertyChanged(nameof(CanUndoApplied));

        ApplyCommand.NotifyCanExecuteChanged();
        UndoAppliedCommand.NotifyCanExecuteChanged();
    }
}
