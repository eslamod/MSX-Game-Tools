using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

public partial class SpritesEditorViewModel : PanelBaseViewModel
{
    private readonly SpriteBank _spriteBank;
    private readonly PaletteLibrary _palettes;
    private readonly IDialogService _dialogs;
    private readonly ReferenceImageLibrary _backgrounds;

    /// <summary>La paleta a cuyos cambios de color estamos suscritos ahora mismo.</summary>
    private ColorPalette _watchedPalette;

    /// <summary>La vista se resuscribe para repintar el lienzo al cambiar de sprite.</summary>
    public event Action<Sprite>? RefreshRequested;

    [ObservableProperty]
    private Sprite _currentSprite;

    /// <summary>
    /// Miniatura seleccionada en la tira. Enlazada al SelectedItem del ListBox en los
    /// dos sentidos: es lo que mantiene sincronizados el lienzo de edición y la tira.
    /// </summary>
    /// <remarks>
    /// Por identidad y no por índice a propósito. Al borrar el sprite seleccionado el
    /// ListBox se limpia solo y escribe el hueco en el ViewModel; si esto fuera un
    /// índice, el valor de vuelta coincidiría con el que ya tenía el enlace y la
    /// selección no se recuperaría nunca.
    /// </remarks>
    [ObservableProperty]
    private ImageMini? _selectedThumbnail;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextSpriteCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousSpriteCommand))]
    private int _currentSpritePosition;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextSpriteCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddSpriteCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSpriteCommand))]
    private int _numberSprites;

    /// <summary>
    /// Fondo sobre el que se previsualiza el sprite, común al lienzo y a todas las
    /// miniaturas del banco. También es lo que se ve donde el sprite no pinta.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundColor))]
    private int _backgroundColorIndex;

    /// <summary>Qué enseña el panel de la derecha: los patrones del banco o los grupos.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPatterns))]
    [NotifyPropertyChangedFor(nameof(ShowsGroups))]
    [NotifyPropertyChangedFor(nameof(ShowsRowColors))]
    [NotifyPropertyChangedFor(nameof(ShowsSpriteColor))]
    private ThumbnailMode _thumbnailMode = ThumbnailMode.Patterns;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteGroupCommand))]
    private SpriteGroupViewModel? _selectedGroup;

    /// <param name="dialogs">
    /// Para confirmar el borrado de un sprite. Sin él no se pregunta nada, que es lo
    /// que quieren los tests.
    /// </param>
    /// <param name="backgrounds">
    /// Imágenes de referencia del espacio de trabajo. Sin ellas el editor funciona
    /// igual, simplemente no hay fondos que elegir.
    /// </param>
    public SpritesEditorViewModel(
        SpriteBank bank,
        PaletteLibrary palettes,
        IDialogService? dialogs = null,
        ReferenceImageLibrary? backgrounds = null,
        EditorPreferences? preferences = null)
    {
        Preferences = preferences ?? new EditorPreferences();

        _spriteBank = bank;
        _palettes = palettes;
        _dialogs = dialogs ?? new SilentDialogService();
        _backgrounds = backgrounds ?? new ReferenceImageLibrary();
        _watchedPalette = palettes.ActivePalette;
        _backgroundColorIndex = _watchedPalette.DefaultBackgroundIndex;

        // Cargar o borrar una imagen aparece y desaparece los controles de fondo.
        _backgrounds.Tiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasBackgrounds));
            OnPropertyChanged(nameof(PatternBackgroundTile));

            foreach (SpriteGroupViewModel group in Groups)
                RenderGroup(group);
        };

        _palettes.PropertyChanged += OnLibraryPropertyChanged;
        _watchedPalette.ColorsChanged += OnActivePaletteColorsChanged;

        _currentSprite = bank.SpritesList[0];
        _currentSpritePosition = 1;
        _numberSprites = bank.SpritesList.Count;

        // La versión WPF creaba la lista vacía, así que la miniatura del primer
        // sprite del banco nunca llegaba a aparecer.
        foreach (Sprite sprite in bank.SpritesList)
        {
            if (sprite.ImageMini is not null)
                ImagesMiniList.Add(sprite.ImageMini);
        }

        _selectedThumbnail = _currentSprite.ImageMini;

        PixelSurface = new SpritePixelSurface(this);

        // El fondo lo guarda cada patrón, así que el selector lee y escribe siempre en
        // el que esté en el lienzo, no en uno fijo.
        PatternBackground = new BackgroundSelectionViewModel(
            _backgrounds, _dialogs, () => CurrentSprite.Background, reference => CurrentSprite.Background = reference);

        PatternBackground.Changed += () => OnPropertyChanged(nameof(PatternBackgroundTile));

        for (int row = 0; row < Sprite.Rows; row++)
        {
            RowColors.Add(new SpriteRowColorViewModel(
                row, _currentSprite.ArraySpriteRows[row], palettes, OnRowColorPicked));
        }

        foreach (SpriteGroup group in bank.Groups)
            TrackGroup(group);

        SelectedGroup = Groups.FirstOrDefault();

        RenderAllThumbnails();
    }

    /// <summary>
    /// El lienzo ha terminado un trazo sobre un patrón: hay que rehacer la composición
    /// de los grupos que lo usen. Se hace al soltar y no por pixel, que serían 2116
    /// pixeles de recomposición por cada uno pintado.
    /// </summary>
    public void NotifyPatternEdited(int patternIndex)
    {
        foreach (SpriteGroupViewModel group in Groups)
        {
            if (group.Group.Members.Any(member => member.PatternIndex == patternIndex))
                RenderGroup(group);
        }
    }

    /// <summary>Al pulsar una miniatura, el lienzo pasa a editar ese sprite.</summary>
    partial void OnSelectedThumbnailChanged(ImageMini? value)
    {
        // null llega cuando el ListBox limpia su selección al borrarse el elemento
        // seleccionado. Lo ignoramos: DeleteSprite reasigna la selección acto seguido.
        if (value is null)
            return;

        int index = ImagesMiniList.IndexOf(value);
        if (index >= 0)
            GoTo(index + 1);
    }

    public SpriteBank SpritesBank => _spriteBank;

    public PaletteLibrary Palettes => _palettes;

    /// <summary>La paleta activa de la biblioteca. Cambiarla repinta todo el banco.</summary>
    public ColorPalette ColorPalette => _palettes.ActivePalette;

    public ObservableCollection<ImageMini> ImagesMiniList { get; } = [];

    /// <summary>Una casilla por línea del sprite actual. Sólo se muestra en MSX2.</summary>
    public ObservableCollection<SpriteRowColorViewModel> RowColors { get; } = [];

    /// <summary>En MSX1 el sprite entero tiene un único color de frente.</summary>
    public bool IsMsx1 => _spriteBank.Type == SpriteBank.SpriteType.MSX;

    /// <summary>En MSX2 cada línea del sprite lleva su propio color.</summary>
    public bool IsMsx2 => !IsMsx1;

    /// <summary>Los 16 colores, para los desplegables de color de línea y de sprite.</summary>
    public IReadOnlyList<PaletteColor> Palette => ColorPalette.Colors;

    /// <summary>Del 1 al F: un fondo transparente dejaría el editor invisible.</summary>
    public IReadOnlyList<PaletteColor> BackgroundChoices => ColorPalette.BackgroundChoices;

    public PaletteColor BackgroundColor => ColorPalette[BackgroundColorIndex];

    /// <summary>Color del sprite en MSX1, donde todas las líneas comparten el mismo.</summary>
    public PaletteColor SpriteColor => ColorPalette[CurrentSprite.ArraySpriteRows[0].Color];

    /// <summary>Los grupos del banco, con su composición ya dibujada.</summary>
    public ObservableCollection<SpriteGroupViewModel> Groups { get; } = [];

    public bool ShowsPatterns => ThumbnailMode == ThumbnailMode.Patterns;

    public bool ShowsGroups => ThumbnailMode == ThumbnailMode.Groups;

    /// <summary>La columna de colores por línea es del patrón, no del grupo.</summary>
    public bool ShowsRowColors => IsMsx2 && ShowsPatterns;

    public bool ShowsSpriteColor => IsMsx1 && ShowsPatterns;

    [RelayCommand(CanExecute = nameof(CanAddGroup))]
    private void AddGroup()
    {
        SpriteGroup? group = _spriteBank.NewGroup(CurrentSpritePosition - 1);
        if (group is null)
            return;

        SelectedGroup = TrackGroup(group);
        AddGroupCommand.NotifyCanExecuteChanged();
    }

    private bool CanAddGroup() => _spriteBank.CanAddGroup;

    [RelayCommand(CanExecute = nameof(CanDeleteGroup))]
    private async Task DeleteGroupAsync()
    {
        if (SelectedGroup is null)
            return;

        SpriteGroupViewModel doomed = SelectedGroup;

        bool confirmed = await _dialogs.ConfirmAsync(
            "Eliminar grupo",
            $"Se va a eliminar el grupo «{doomed.Group.Name}». Esta acción no se puede deshacer.",
            "Eliminar");

        if (!confirmed)
            return;

        int index = Groups.IndexOf(doomed);

        // La selección se mueve antes de quitarlo, para que el ListBox no se quede
        // sin elemento seleccionado y escriba el hueco de vuelta.
        SelectedGroup = Groups.Count > 1
            ? Groups[index > 0 ? index - 1 : index + 1]
            : null;

        Groups.Remove(doomed);
        _spriteBank.Groups.Remove(doomed.Group);

        AddGroupCommand.NotifyCanExecuteChanged();
    }

    private bool CanDeleteGroup() => SelectedGroup is not null;

    /// <summary>Engancha un grupo del banco al panel y lo deja dibujado.</summary>
    private SpriteGroupViewModel TrackGroup(SpriteGroup group)
    {
        var viewModel = new SpriteGroupViewModel(group, _spriteBank, _palettes, _backgrounds, _dialogs);

        // Cambiar de fondo cambia cómo se compone: con referencia el hueco va
        // transparente, y sin ella vuelve al color de fondo.
        viewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(SpriteGroupViewModel.BackgroundTile) && sender is SpriteGroupViewModel changed)
                RenderGroup(changed);
        };

        group.Changed += OnGroupChanged;
        viewModel.EditTargetChanged += OnGroupEditTargetChanged;

        Groups.Add(viewModel);
        RenderGroup(viewModel);

        return viewModel;
    }

    /// <summary>
    /// El lienzo sigue el patrón del miembro que se esté tocando. Estar en modo Grupos
    /// no impide seguir dibujando: es la forma de ver el efecto en la composición.
    /// </summary>
    private void OnGroupEditTargetChanged(SpriteGroupMember member) => GoTo(member.PatternIndex + 1);

    partial void OnSelectedGroupChanged(SpriteGroupViewModel? value)
    {
        if (value?.SelectedMember is not null)
            GoTo(value.SelectedMember.PatternIndex + 1);
    }

    partial void OnThumbnailModeChanged(ThumbnailMode value)
    {
        if (value == ThumbnailMode.Groups && SelectedGroup?.SelectedMember is not null)
            GoTo(SelectedGroup.SelectedMember.PatternIndex + 1);
    }

    private void OnGroupChanged(SpriteGroup group)
    {
        SpriteGroupViewModel? viewModel = Groups.FirstOrDefault(g => ReferenceEquals(g.Group, group));
        if (viewModel is not null)
            RenderGroup(viewModel);
    }

    private void RenderGroup(SpriteGroupViewModel group) =>
        group.Render(ColorPalette, BackgroundColor.Color);

    /// <summary>Zoom y demás ajustes que sobreviven al cambio de pestaña.</summary>
    public EditorPreferences Preferences { get; }

    /// <summary>El patrón actual visto por el lienzo de pintado.</summary>
    public IPixelSurface PixelSurface { get; }

    /// <summary>Imágenes de referencia disponibles, para los desplegables de fondo.</summary>
    public ReferenceImageLibrary Backgrounds => _backgrounds;

    /// <summary>Sin ninguna cargada, los controles de fondo no se enseñan.</summary>
    public bool HasBackgrounds => _backgrounds.Tiles.Count > 0;

    /// <summary>Fondo que se ve detrás del lienzo mientras se dibuja este patrón.</summary>
    public ReferenceTile? PatternBackgroundTile
    {
        get => PatternBackground.Tile;
        set => PatternBackground.Apply(value?.Ref ?? BackgroundRef.None);
    }

    /// <summary>El desplegable de imágenes y el botón de celda del lienzo.</summary>
    public BackgroundSelectionViewModel PatternBackground { get; private set; } = null!;

    /// <summary>Opacidad del sprite sobre el fondo, en el lienzo de edición.</summary>
    [ObservableProperty]
    private double _patternOpacity = 1.0;

    private void RenderAllGroups()
    {
        foreach (SpriteGroupViewModel group in Groups)
            RenderGroup(group);
    }

    [RelayCommand(CanExecute = nameof(CanAddSprite))]
    private void AddSprite()
    {
        Sprite? sprite = _spriteBank.NewSprite();
        if (sprite?.ImageMini is null)
            return;

        ImagesMiniList.Add(sprite.ImageMini);
        NumberSprites = _spriteBank.SpritesList.Count;
        RenderThumbnail(sprite);

        // El sprite recién creado pasa a ser el que se edita.
        GoTo(NumberSprites);
    }

    private bool CanAddSprite() => NumberSprites < SpriteBank.MaxSprites;

    [RelayCommand(CanExecute = nameof(CanDeleteSprite))]
    private async Task DeleteSpriteAsync()
    {
        int index = CurrentSpritePosition - 1;

        // Borrar un sprite tampoco se puede deshacer.
        bool confirmed = await _dialogs.ConfirmAsync(
            "Eliminar sprite",
            $"Se va a eliminar el sprite {CurrentSpritePosition} de {NumberSprites}. Esta acción no se puede deshacer.",
            "Eliminar");

        if (!confirmed)
            return;

        _spriteBank.DeleteSprite(index);
        if ((uint)index < (uint)ImagesMiniList.Count)
            ImagesMiniList.RemoveAt(index);

        NumberSprites = _spriteBank.SpritesList.Count;

        // Se queda en la misma posición, que ahora ocupa el sprite siguiente,
        // salvo que se hubiera borrado el último.
        GoTo(Math.Min(index + 1, NumberSprites));
    }

    // Un banco siempre conserva al menos un sprite: si no, el editor se queda sin
    // nada que dibujar (en WPF se podía vaciar y el lienzo apuntaba a un sprite muerto).
    private bool CanDeleteSprite() => NumberSprites > 1;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextSprite() => GoTo(CurrentSpritePosition + 1);

    private bool CanGoNext() => CurrentSpritePosition < NumberSprites;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousSprite() => GoTo(CurrentSpritePosition - 1);

    private bool CanGoPrevious() => CurrentSpritePosition > 1;

    /// <summary>MSX1: el color elegido se aplica a las 16 líneas del sprite.</summary>
    [RelayCommand]
    private void PickSpriteColor(PaletteColor? color)
    {
        if (color is null)
            return;

        foreach (SpriteRow row in CurrentSprite.ArraySpriteRows)
            row.Color = color.Index;

        foreach (SpriteRowColorViewModel cell in RowColors)
            cell.Refresh();

        OnPropertyChanged(nameof(SpriteColor));
        RepaintCurrentSprite();
    }

    [RelayCommand]
    private void PickBackgroundColor(PaletteColor? color)
    {
        if (color is not null)
            BackgroundColorIndex = color.Index;
    }

    /// <summary>
    /// El fondo se ve en todo el banco, no sólo en el sprite actual: en las miniaturas
    /// de patrones, en las de grupos y en el lienzo. Va aquí y no en el comando para
    /// que valga también cuando lo fija un banco recién cargado de fichero.
    /// </summary>
    partial void OnBackgroundColorIndexChanged(int value)
    {
        RenderAllThumbnails();
        RenderAllGroups();
        RefreshRequested?.Invoke(CurrentSprite);
    }

    /// <summary>MSX2: el color elegido se aplica sólo a esa línea.</summary>
    private void OnRowColorPicked(int rowIndex, PaletteColor color)
    {
        CurrentSprite.ArraySpriteRows[rowIndex].Color = color.Index;

        RowColors[rowIndex].Refresh();
        OnPropertyChanged(nameof(SpriteColor));
        RepaintCurrentSprite();
    }

    /// <summary>Otra paleta pasa a ser la activa: hay que repintarlo todo con ella.</summary>
    private void OnLibraryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PaletteLibrary.ActivePalette))
            return;

        _watchedPalette.ColorsChanged -= OnActivePaletteColorsChanged;
        _watchedPalette = _palettes.ActivePalette;
        _watchedPalette.ColorsChanged += OnActivePaletteColorsChanged;

        OnPropertyChanged(nameof(ColorPalette));
        OnPropertyChanged(nameof(Palette));
        OnPropertyChanged(nameof(BackgroundChoices));

        RefreshPaletteDependentState();
    }

    /// <summary>
    /// Han cambiado los componentes de algún color de la paleta activa. El lienzo se
    /// repinta solo, porque cada color reutiliza siempre el mismo brush, pero las
    /// miniaturas son pixeles y hay que rehacerlas.
    /// </summary>
    private void OnActivePaletteColorsChanged(ColorPalette palette) => RefreshPaletteDependentState();

    private void RefreshPaletteDependentState()
    {
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(SpriteColor));

        foreach (SpriteRowColorViewModel cell in RowColors)
            cell.Refresh();

        foreach (SpriteGroupViewModel group in Groups)
            group.RefreshMemberColors();

        RenderAllThumbnails();
        RenderAllGroups();
        RefreshRequested?.Invoke(CurrentSprite);
    }

    private void RepaintCurrentSprite()
    {
        RenderThumbnail(CurrentSprite);
        RefreshRequested?.Invoke(CurrentSprite);
    }

    private void RenderThumbnail(Sprite sprite)
    {
        if (sprite.ImageMini is not null)
            SpriteRenderer.Render(sprite, ColorPalette, BackgroundColor.Color, sprite.ImageMini);
    }

    private void RenderAllThumbnails()
    {
        foreach (Sprite sprite in _spriteBank.SpritesList)
            RenderThumbnail(sprite);
    }

    private void GoTo(int position)
    {
        if (position < 1 || position > NumberSprites)
            return;

        Sprite target = _spriteBank.SpritesList[position - 1];

        // Comparar también el sprite y no sólo la posición: al borrar, la posición
        // puede no cambiar pero el sprite que la ocupa sí, y hay que repintar.
        // Y al revés, el enlace bidireccional de la lista de miniaturas reescribe
        // el mismo índice constantemente y no debe provocar repintados.
        if (CurrentSpritePosition == position && ReferenceEquals(CurrentSprite, target))
            return;

        CurrentSpritePosition = position;
        CurrentSprite = target;
        SelectedThumbnail = target.ImageMini;

        AttachRowColors(target);

        RefreshRequested?.Invoke(CurrentSprite);
    }

    private void AttachRowColors(Sprite sprite)
    {
        for (int row = 0; row < RowColors.Count; row++)
            RowColors[row].Attach(sprite.ArraySpriteRows[row]);

        OnPropertyChanged(nameof(SpriteColor));
    }
}
