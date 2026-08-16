using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

public partial class SpritesEditorViewModel : PanelBaseViewModel, IPaletteDocument
{
    private readonly SpriteBank _spriteBank;
    private readonly IDialogService _dialogs;
    private readonly ReferenceImageLibrary _backgrounds;

    /// <summary>El patrón copiado, que es de la ventana y no de este banco.</summary>
    private readonly SpriteClipboard _clipboard;

    /// <summary>La paleta del banco, a cuyos cambios de color estamos suscritos.</summary>
    private ColorPalette _palette;

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
    private int _currentSpriteIndex;

    /// <summary>
    /// Cuántos patrones tiene el banco, que ahora son siempre los mismos.
    /// </summary>
    /// <remarks>
    /// Se queda como propiedad y no como constante porque la vista la enseña en «0 / 63» y
    /// la navegación la usa de tope.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextSpriteCommand))]
    [NotifyPropertyChangedFor(nameof(LastSpriteIndex))]
    private int _numberSprites;

    /// <summary>
    /// El número del último patrón, para enseñar «0 / 63» y no «0 / 64».
    /// </summary>
    /// <remarks>
    /// Los patrones se numeran desde 0, como los cuenta el VDP y como los escribe quien
    /// programa el juego. La cuenta y el último número se diferencian en uno, y el que
    /// sirve para leer un número de patrón es éste.
    /// </remarks>
    public int LastSpriteIndex => NumberSprites - 1;

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
    /// <param name="clipboard">
    /// El patrón copiado, común a todos los bancos abiertos. Sin él, el editor tiene el suyo
    /// y copiar y pegar siguen funcionando dentro de este banco.
    /// </param>
    /// <inheritdoc cref="TileSetEditorViewModel(TileSet, ColorPalette, EditorPreferences)" path="/param[@name='palette']"/>
    public SpritesEditorViewModel(
        SpriteBank bank,
        ColorPalette palette,
        IDialogService? dialogs = null,
        ReferenceImageLibrary? backgrounds = null,
        EditorPreferences? preferences = null,
        SpriteClipboard? clipboard = null)
    {
        Preferences = preferences ?? new EditorPreferences();

        _spriteBank = bank;
        _dialogs = dialogs ?? new SilentDialogService();
        _backgrounds = backgrounds ?? new ReferenceImageLibrary();
        _clipboard = clipboard ?? new SpriteClipboard();
        _palette = palette;
        _backgroundColorIndex = palette.DefaultBackgroundIndex;

        // Cargar o borrar una imagen aparece y desaparece los controles de fondo.
        _backgrounds.Tiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasBackgrounds));
            OnPropertyChanged(nameof(PatternBackgroundTile));

            foreach (SpriteGroupViewModel group in Groups)
                RenderGroup(group);
        };

        _palette.ColorsChanged += OnPaletteColorsChanged;

        _currentSprite = bank.SpritesList[0];
        _currentSpriteIndex = 0;
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

        // Qué imagen de referencia lleva cada patrón se guarda en el banco.
        PatternBackground.Changed += () =>
        {
            Touch();
            OnPropertyChanged(nameof(PatternBackgroundTile));
        };

        for (int row = 0; row < Sprite.Rows; row++)
        {
            RowColors.Add(new SpriteRowColorViewModel(
                row, _currentSprite.ArraySpriteRows[row], () => ColorPalette, OnRowColorPicked));
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
            GoTo(index);
    }

    public override bool IsDocument => true;

    public override string DocumentName
    {
        get => _spriteBank.Name;
        set => _spriteBank.Name = value;
    }

    public override string HeaderTag => "SP";

    public override string KindKey => "SpriteBank";

    /// <inheritdoc cref="TileSetEditorViewModel.ToFileText"/>
    public override string ToFileText() => SpriteBankSerializer.Serialize(
        _spriteBank, ColorPalette, BackgroundColorIndex, [.. _backgrounds.Images]);

    public SpriteBank SpritesBank => _spriteBank;

    /// <inheritdoc/>
    /// <remarks>Cambiarla repinta todo el banco: patrones, grupos y lienzo.</remarks>
    public ColorPalette ColorPalette
    {
        get => _palette;
        set
        {
            if (ReferenceEquals(_palette, value))
                return;

            _palette.ColorsChanged -= OnPaletteColorsChanged;
            _palette = value;
            _palette.ColorsChanged += OnPaletteColorsChanged;

            OnPropertyChanged(nameof(ColorPalette));
            OnPropertyChanged(nameof(Palette));
            OnPropertyChanged(nameof(BackgroundChoices));

            RefreshPaletteDependentState();
        }
    }

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
        SpriteGroup? group = _spriteBank.NewGroup(CurrentSpriteIndex);
        if (group is null)
            return;

        SelectedGroup = TrackGroup(group);

        Touch();
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
            Localizer.Instance["DeleteGroupTitle"],
            Localizer.Instance.Format("DeleteGroupBody", doomed.Group.Name),
            Localizer.Instance["DeleteLabel"]);

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

        Touch();
        AddGroupCommand.NotifyCanExecuteChanged();
    }

    private bool CanDeleteGroup() => SelectedGroup is not null;

    /// <summary>Engancha un grupo del banco al panel y lo deja dibujado.</summary>
    private SpriteGroupViewModel TrackGroup(SpriteGroup group)
    {
        var viewModel = new SpriteGroupViewModel(group, _spriteBank, () => ColorPalette, _backgrounds, _dialogs);

        // Cambiar de fondo cambia cómo se compone: con referencia el hueco va
        // transparente, y sin ella vuelve al color de fondo.
        viewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(SpriteGroupViewModel.BackgroundTile) && sender is SpriteGroupViewModel changed)
            {
                Touch();
                RenderGroup(changed);
            }
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
    private void OnGroupEditTargetChanged(SpriteGroupMember member) => GoTo(member.PatternIndex);

    partial void OnSelectedGroupChanged(SpriteGroupViewModel? value)
    {
        if (value?.SelectedMember is not null)
            GoTo(value.SelectedMember.PatternIndex);
    }

    partial void OnThumbnailModeChanged(ThumbnailMode value)
    {
        if (value == ThumbnailMode.Groups && SelectedGroup?.SelectedMember is not null)
            GoTo(SelectedGroup.SelectedMember.PatternIndex);
    }

    private void OnGroupChanged(SpriteGroup group)
    {
        Touch();

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

    /// <summary>
    /// Copia el patrón que se está editando en el primer hueco libre.
    /// </summary>
    /// <remarks>
    /// Hacer una variación de un sprite —el mismo bicho mirando al otro lado— obligaba a
    /// redibujarlo entero. En el primer hueco libre y no detrás del original, porque detrás
    /// habría que correr los de atrás y correr un índice es justo lo que este banco ya no
    /// hace.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanDuplicateSprite))]
    private void DuplicateSprite()
    {
        int free = _spriteBank.DuplicateSprite(CurrentSpriteIndex);

        if (free < 0)
            return;

        Touch();
        RenderThumbnail(_spriteBank.SpritesList[free]);

        // Se va a la copia, que es sobre la que se va a trabajar.
        GoTo(free);
    }

    private bool CanDuplicateSprite() => _spriteBank.FirstEmpty() >= 0;

    public bool HasCopiedSprite => _clipboard.Content is not null;

    /// <summary>
    /// Vuelve a mirar el portapapeles, que es de la ventana y lo puede haber llenado otro banco.
    /// </summary>
    /// <remarks>
    /// Lo llama la ventana al poner este banco delante. Sin esto, copiar en un banco dejaba el
    /// botón de pegar apagado en los demás hasta cerrarlos y volverlos a abrir.
    /// </remarks>
    public void RefreshClipboardState()
    {
        OnPropertyChanged(nameof(HasCopiedSprite));
        PasteSpriteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Se lleva el patrón que se está editando.</summary>
    /// <remarks>
    /// Duplicar sirve para «otro igual, donde quepa»; esto es para «éste, ahí», que es lo
    /// que hace falta cuando el número importa. Y es lo que se espera de un botón derecho
    /// sobre una miniatura.
    /// </remarks>
    [RelayCommand]
    private void CopySprite()
    {
        _clipboard.Put(CurrentSprite, _spriteBank.Type);

        RefreshClipboardState();
    }

    /// <summary>
    /// Suelta lo copiado en el patrón que se está viendo, venga del banco que venga.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Si el destino tiene algo, se pregunta antes en vez de no dejar: machacar un patrón a
    /// veces es justo lo que se quiere, y obligar a vaciarlo primero son dos pasos para el
    /// mismo resultado. Preguntar impide que pase sin querer, que es el riesgo de verdad
    /// cuando no hay deshacer.
    /// </para>
    /// <para>
    /// Y si lo copiado trae varios colores de línea y este banco es MSX1, se avisa de que se
    /// queda de uno solo. Se avisa antes de perderlo y no después, y sólo cuando de verdad se
    /// pierde algo: un patrón que ya iba de un color entra igual en los dos bancos.
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasCopiedSprite))]
    private async Task PasteSpriteAsync()
    {
        if (_clipboard.Content is not { } copied)
            return;

        bool loses = IsMsx1 && copied.HasSeveralColors;

        if (!CurrentSprite.IsEmpty || loses)
        {
            // Los dos motivos caben en un aviso: preguntar dos veces seguidas por la misma
            // pulsación se contesta que sí sin leer, que es justo lo que el aviso evita.
            var body = new List<string>();

            if (loses)
                body.Add(Localizer.Instance["PasteSpriteFlattenBody"]);

            if (!CurrentSprite.IsEmpty)
                body.Add(Localizer.Instance.Format("PasteSpriteBody", CurrentSpriteIndex));

            bool confirmed = await _dialogs.ConfirmAsync(
                Localizer.Instance["PasteSpriteTitle"],
                string.Join("\n\n", body),
                Localizer.Instance["PasteLabel"]);

            if (!confirmed)
                return;
        }

        CurrentSprite.CopyFrom(copied);

        // En un banco MSX1 el patrón va de un color, así que el lienzo tiene que enseñarlo de
        // uno: dejarlo de 16 pintaría algo que la máquina no puede.
        if (IsMsx1)
            CurrentSprite.FlattenColor();

        Touch();
        RenderThumbnail(CurrentSprite);
        RefreshRequested?.Invoke(CurrentSprite);

        // Acaban de cambiar los 16 colores de línea, y las casillas de la columna los tienen
        // leídos de antes.
        foreach (SpriteRowColorViewModel cell in RowColors)
            cell.Refresh();

        OnPropertyChanged(nameof(SpriteColor));
    }

    /// <summary>Deja el patrón en blanco sin quitarlo del banco.</summary>
    [RelayCommand]
    private async Task ClearSpriteAsync()
    {
        // Vaciar tampoco se puede deshacer: en el editor de sprites no hay historia.
        bool confirmed = await _dialogs.ConfirmAsync(
            Localizer.Instance["ClearSpriteTitle"],
            Localizer.Instance.Format("ClearSpriteBody", CurrentSpriteIndex),
            Localizer.Instance["ClearLabel"]);

        if (!confirmed)
            return;

        CurrentSprite.Clear();

        Touch();
        RenderThumbnail(CurrentSprite);
        RefreshRequested?.Invoke(CurrentSprite);

        // Los colores de linea del lienzo cuelgan del sprite, y acaban de cambiar todos.
        OnPropertyChanged(nameof(SpriteColor));
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextSprite() => GoTo(CurrentSpriteIndex + 1);

    private bool CanGoNext() => CurrentSpriteIndex < NumberSprites - 1;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousSprite() => GoTo(CurrentSpriteIndex - 1);

    private bool CanGoPrevious() => CurrentSpriteIndex > 0;

    /// <summary>MSX1: el color elegido se aplica a las 16 líneas del sprite.</summary>
    [RelayCommand]
    private void PickSpriteColor(PaletteColor? color)
    {
        if (color is null)
            return;

        foreach (SpriteRow row in CurrentSprite.ArraySpriteRows)
            row.Color = color.Index;

        Touch();

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
        Touch();

        RenderAllThumbnails();
        RenderAllGroups();
        RefreshRequested?.Invoke(CurrentSprite);
    }

    /// <inheritdoc/>
    public void RemapColors(IReadOnlyList<int> table)
    {
        _spriteBank.RemapColors(table);
        BackgroundColorIndex = table[BackgroundColorIndex];

        // Aunque el fondo no se haya movido: los patrones y los grupos sí.
        RefreshPaletteDependentState();
    }

    /// <summary>MSX2: el color elegido se aplica sólo a esa línea.</summary>
    private void OnRowColorPicked(int rowIndex, PaletteColor color)
    {
        CurrentSprite.ArraySpriteRows[rowIndex].Color = color.Index;

        Touch();

        RowColors[rowIndex].Refresh();
        OnPropertyChanged(nameof(SpriteColor));
        RepaintCurrentSprite();
    }

    /// <summary>
    /// Han cambiado los componentes de algún color de la paleta del banco. El lienzo se
    /// repinta solo, porque cada color reutiliza siempre el mismo brush, pero las
    /// miniaturas son pixeles y hay que rehacerlas.
    /// </summary>
    private void OnPaletteColorsChanged(ColorPalette palette) => RefreshPaletteDependentState();

    private void RefreshPaletteDependentState()
    {
        // La paleta va dentro del fichero del banco, asi que tocarla lo deja sin guardar.
        Touch();

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

    private void GoTo(int index)
    {
        if (index < 0 || index >= NumberSprites)
            return;

        Sprite target = _spriteBank.SpritesList[index];

        // Comparar también el sprite y no sólo el índice: al vaciar, el índice
        // puede no cambiar pero el sprite que lo ocupa sí, y hay que repintar.
        // Y al revés, el enlace bidireccional de la lista de miniaturas reescribe
        // el mismo índice constantemente y no debe provocar repintados.
        if (CurrentSpriteIndex == index && ReferenceEquals(CurrentSprite, target))
            return;

        CurrentSpriteIndex = index;
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
