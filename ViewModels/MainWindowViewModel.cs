using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Media;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly Dictionary<string, PanelBaseViewModel> _panels = [];

    private readonly SettingsStore? _settings;

    private PanelBaseViewModel? _rightPanViewModel;

    private double _rightPanelWidth = MinRightPanelWidth;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDocumentCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveDocumentAsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSpriteBankBinaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSpriteBankAssemblerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportTileSetBinaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportTileSetAssemblerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportTileSetPngCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowBlocksCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportMapCsvCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportMapBinaryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportMapAssemblerCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResizeMapCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReplaceTilesCommand))]
    private PanelBaseViewModel? _selectedTab;

    /// <param name="dialogs">
    /// La aplicación inyecta el servicio real; si no se pasa ninguno no se abre nada,
    /// que es lo que quieren los tests y cualquier uso sin ventana.
    /// </param>
    /// <param name="settings">
    /// Dónde se guardan el idioma y los zooms. Sin él no se lee ni se escribe nada, que es
    /// lo que quieren los tests: los ajustes de quien ejecuta las pruebas no se tocan.
    /// </param>
    public MainWindowViewModel(IDialogService? dialogs = null, SettingsStore? settings = null)
    {
        Dialogs = dialogs ?? new SilentDialogService();
        _settings = settings;

        // La variante, por un solo sitio: aquí llegan tanto la que viene del fichero de
        // ajustes al arrancar —CopyFrom pasa por los setters— como la que se elige en el
        // panel de preferencias. Sin esto habría que acordarse de aplicarla en los dos.
        Preferences.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorPreferences.ThemeVariant))
                AppTheme.Apply(Preferences.ThemeVariant);
        };

        Palettes.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(PaletteLibrary.ActivePalette))
                return;

            // Elegir en la barra le cambia la paleta al documento que está delante, y sólo
            // a ése: la paleta va dentro de su fichero, no es un ajuste del programa.
            if (SelectedTab is IPaletteDocument document)
                document.ColorPalette = Palettes.ActivePalette;

            // Y los comandos de paleta dependen de cuál sea (la estándar no se puede editar
            // ni eliminar) y de cuántas queden (nunca se borra la última).
            RefreshPaletteCommands();
        };

        Palettes.Palettes.CollectionChanged += (_, _) => RefreshPaletteCommands();

        TreeGeneralVm.OpenItemCommand = OpenTreeItemCommand;
        TreeGeneralVm.DeleteItemCommand = DeleteTreeItemCommand;
        TreeGeneralVm.ShowPropertiesCommand = ShowPropertiesCommand;

        Backgrounds.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ReferenceImageLibrary.SelectedImage))
                DeleteBackgroundCommand.NotifyCanExecuteChanged();
        };
    }

    /// <summary>Los textos, que se escriben mucho por aquí.</summary>
    private static Localizer Text => Localizer.Instance;

    private void RefreshPaletteCommands()
    {
        EditPaletteCommand.NotifyCanExecuteChanged();
        DeletePaletteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>La barra de paletas enseña la del documento que pasa a estar delante.</summary>
    /// <summary>
    /// Al dejar un juego de tiles se guarda lo que tuviera marcado.
    /// </summary>
    /// <remarks>
    /// Aquí y no al marcar: mientras el juego sigue delante, lo que cae al estampar se copia
    /// en ese momento, para que retocar un tile marcado se vea en lo que se suelta. Eso deja
    /// de poder hacerse justo cuando el juego deja de verse, y es ahí donde se congela.
    /// </remarks>
    partial void OnSelectedTabChanging(PanelBaseViewModel? oldValue, PanelBaseViewModel? newValue)
    {
        if (oldValue is TileSetEditorViewModel leaving && leaving.TakeSelection() is { } taken)
            TilesInHand = taken;
    }

    partial void OnSelectedTabChanged(PanelBaseViewModel? value)
    {
        if (value is IPaletteDocument document)
            Palettes.ActivePalette = document.ColorPalette;

        // Al llegar se le da lo que se traiga, y eso pasa a ser lo que cae: manda lo último
        // que se cogió. Lo que salió de este mismo juego no se le devuelve.
        if (value is TileSetEditorViewModel arriving)
            arriving.Receive(TilesInHand);

        // El banco ya mira el mismo portapapeles que los demás; lo que no sabe es que ha
        // cambiado mientras él no estaba delante, y de eso depende que pegar esté encendido.
        if (value is SpritesEditorViewModel bank)
            bank.RefreshClipboardState();
    }

    /// <summary>
    /// Los paneles que dibujan con una paleta, estén en una pestaña o no.
    /// </summary>
    /// <remarks>
    /// Un mapa sale aquí aunque no tenga paleta propia: la suya es la de su juego, y lo
    /// que se le asigne va a parar allí.
    /// </remarks>
    private IEnumerable<IPaletteDocument> PaletteDocuments => _panels.Values.OfType<IPaletteDocument>();

    /// <summary>
    /// Los documentos que dibujan con esa misma paleta.
    /// </summary>
    /// <remarks>
    /// Por referencia y no por contenido, que es como se comparten: al abrir un fichero,
    /// <see cref="PaletteLibrary.Adopt"/> devuelve la paleta que ya estaba si coinciden el
    /// nombre y los 16 colores, así que dos documentos acaban apuntando al mismo objeto.
    /// Mover un color de sitio los afecta a todos, y reajustar sólo el que está delante
    /// dejaría a los demás con los colores cambiados y sin avisar.
    /// </remarks>
    public IReadOnlyList<IPaletteDocument> DocumentsWith(ColorPalette palette) =>
        [.. PaletteDocuments.Where(document => ReferenceEquals(document.ColorPalette, palette))];

    /// <summary>
    /// Las paletas del proyecto, de donde eligen la suya los documentos.
    /// </summary>
    /// <remarks>
    /// Es un catálogo compartido, no la paleta con la que se dibuja: eso lo lleva cada
    /// documento, porque va dentro de su fichero. Lo que enseña la barra es la del que
    /// esté delante.
    /// </remarks>
    public PaletteLibrary Palettes { get; } = new();

    /// <summary>
    /// Imagenes de referencia que se pueden poner de fondo. Recurso del espacio de
    /// trabajo, como las paletas: se cargan una vez y las usa quien quiera.
    /// </summary>
    public ReferenceImageLibrary Backgrounds { get; } = new();

    /// <summary>
    /// Ajustes de presentación comunes a todas las pestañas, como el zoom.
    /// </summary>
    public EditorPreferences Preferences { get; } = new();

    /// <summary>Lo último que se abrió, para volver a ello sin buscarlo.</summary>
    public RecentFiles Recent { get; } = new();

    /// <summary>
    /// El trozo de tiles que se está llevando de un juego a otro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vive aquí y no en el editor porque el editor se queda atrás: lo que se marcó en un
    /// juego tiene que sobrevivir a irse a otro, que es justamente lo que se quiere hacer.
    /// </para>
    /// <para>
    /// Sin botón nuevo y sin menú: el gesto ya existía —marcar aquí, estampar allí— y lo
    /// único que le faltaba era cruzar la pestaña. Un «copiar de...» habría dejado la
    /// selección del origen invisible desde donde se pega; así, lo que se lleva en la mano
    /// se ve bajo el ratón antes de soltarlo.
    /// </para>
    /// </remarks>
    public CopiedTiles? TilesInHand { get; private set; }

    /// <summary>
    /// El patrón de sprite copiado, uno para todos los bancos abiertos.
    /// </summary>
    /// <remarks>
    /// Lo mismo que <see cref="TilesInHand"/> y por lo mismo, pero más sencillo: lo que se
    /// copia ya es una copia suelta desde el momento de copiarlo, así que no hay nada que
    /// congelar al dejar el banco. Basta con que el sitio donde se guarda no sea el banco.
    /// </remarks>
    public SpriteClipboard SpritesInHand { get; } = new();

    /// <summary>Se reparte a los paneles que necesiten confirmar algo destructivo.</summary>
    public IDialogService Dialogs { get; }

    public TreeGeneralViewModel TreeGeneralVm { get; } = new();

    public ObservableCollection<PanelBaseViewModel> Tabs { get; } = [];

    /// <summary>
    /// Los paneles del lateral derecho.
    /// </summary>
    /// <remarks>
    /// Conviven los formularios de un solo uso (crear un juego, editar la paleta), que se
    /// cierran solos al aceptar, y las herramientas que se quedan puestas mientras se
    /// trabaja, como los bloques de un juego de tiles.
    /// </remarks>
    public ObservableCollection<PanelBaseViewModel> RightPanels { get; } = [];

    /// <summary>Lo más estrecho que puede quedar el lateral sin volverse inservible.</summary>
    public const double MinRightPanelWidth = 380;

    /// <summary>Lo más ancho, para que no se coma la zona de edición.</summary>
    public const double MaxRightPanelWidth = 760;

    /// <summary>
    /// Ancho de la columna del lateral.
    /// </summary>
    /// <remarks>
    /// El ancho que dejó el separador la última vez, para volver a abrir con él. Lo aplica
    /// la ventana: una definición de columna no está en el árbol y no le llega un enlace.
    /// </remarks>
    public double RightPanelWidth
    {
        get => _rightPanelWidth;
        set => _rightPanelWidth = Math.Clamp(value, MinRightPanelWidth, MaxRightPanelWidth);
    }

    /// <summary>
    /// El panel del lateral que se está viendo.
    /// </summary>
    /// <remarks>
    /// Asignarlo abre ese panel; asignar <c>null</c> cierra el que estuviera delante, que
    /// es lo que hacen los formularios al aceptar o cancelar.
    /// </remarks>
    public PanelBaseViewModel? RightPanViewModel
    {
        get => _rightPanViewModel;
        set
        {
            if (value is null)
            {
                CloseRightPanel(_rightPanViewModel);
                return;
            }

            if (!RightPanels.Contains(value))
                RightPanels.Add(value);

            SetProperty(ref _rightPanViewModel, value);
        }
    }

    /// <summary>
    /// Cierra un panel del lateral.
    /// </summary>
    /// <remarks>
    /// La selección se mueve antes de quitarlo de la colección: si se quita primero, el
    /// TabControl escribe <c>null</c> de vuelta y el lateral se queda en blanco aunque
    /// queden paneles.
    /// </remarks>
    [RelayCommand]
    public void CloseRightPanel(PanelBaseViewModel? panel)
    {
        if (panel is null)
            return;

        int index = RightPanels.IndexOf(panel);
        if (index < 0)
            return;

        SetProperty(
            ref _rightPanViewModel,
            RightPanels.Count > 1 ? RightPanels[index == 0 ? 1 : index - 1] : null,
            nameof(RightPanViewModel));

        RightPanels.RemoveAt(index);

        panel.OnClosed();
    }

    public int CurrentSpriteBankCounter { get; set; }

    public int CurrentTileSetCounter { get; set; }

    [RelayCommand]
    private void AddSpriteBank() => OpenForm(() => new EditSpriteBankViewModel(this));

    [RelayCommand]
    private void AddTileSet() => OpenForm(() => new EditTileSetViewModel(this));

    [RelayCommand]
    private void AddMap() => OpenForm(() => new EditMapViewModel(this));

    /// <summary>
    /// Los juegos de tiles que hay en el proyecto.
    /// </summary>
    /// <remarks>
    /// Un mapa son números de tile: hace falta elegir con qué juego se dibuja, y sin
    /// ninguno abierto no hay mapa que crear.
    /// </remarks>
    public IReadOnlyList<TileSetEditorViewModel> TileSets =>
        [.. _panels.Values.OfType<TileSetEditorViewModel>()];

    /// <summary>
    /// El juego de tiles con el que se dibuja un mapa, si está abierto.
    /// </summary>
    /// <remarks>
    /// Por identidad, que es lo que no cambia al renombrar. Los mapas guardados antes de
    /// que la identidad existiera no la traen, y para ésos se recurre al nombre; en cuanto
    /// se vuelven a guardar ya llevan la del juego y dejan de depender de él.
    /// </remarks>
    public TileSetEditorViewModel? TileSetOf(TileMap map) =>
        map.TileSetId != Guid.Empty
            ? TileSets.FirstOrDefault(tiles => tiles.TileSet.Id == map.TileSetId)
            : TileSets.FirstOrDefault(tiles => tiles.TileSet.Name == map.TileSetName);

    /// <summary>
    /// Le cambia el nombre a un documento, con todo lo que eso arrastra.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El nombre sale en la pestaña, en el árbol y, en un juego de tiles, también en el
    /// panel de sus bloques y en el nombre que cada uno de sus mapas lleva apuntado. Va
    /// aquí porque es lo único que ve todas esas piezas a la vez.
    /// </para>
    /// <para>
    /// El fichero no se renombra. El documento y su fichero son cosas distintas: cómo se
    /// llama un mapa es del mapa, y dónde vive lo decide quien lo guarda.
    /// </para>
    /// </remarks>
    /// <returns><c>false</c> si el nombre no vale o es el que ya tenía.</returns>
    public bool Rename(PanelBaseViewModel? panel, string? name)
    {
        string clean = name?.Trim() ?? string.Empty;

        if (panel is not { IsDocument: true } || clean.Length == 0 || clean == panel.DocumentName)
            return false;

        panel.DocumentName = clean;
        panel.RefreshHeader();

        TreeGeneralVm.Rename(panel.TagId, panel.Header);
        panel.Touch();

        if (panel is TileSetEditorViewModel tiles)
            RenameFollowers(tiles);

        return true;
    }

    /// <summary>
    /// Lo que cuelga de un juego de tiles y lleva su nombre escrito.
    /// </summary>
    /// <remarks>
    /// Los mapas guardan cómo se llamaba su juego, y aunque ya no es lo que los une —van
    /// por identidad— sí es lo que se lee en su fichero, así que se pone al día y quedan
    /// sin guardar. Antes de la identidad esto no era cosmético: era perderlos.
    /// </remarks>
    private void RenameFollowers(TileSetEditorViewModel tiles)
    {
        foreach (TileBlocksViewModel blocks in _panels.Values.OfType<TileBlocksViewModel>())
        {
            if (ReferenceEquals(blocks.TileSet, tiles.TileSet))
                blocks.Header = $"{Localization.Localizer.Instance["TreeBlocks"]}: {tiles.TileSet.Name}";
        }

        foreach (MapEditorViewModel map in MapsOf(tiles))
        {
            if (map.Map.TileSetName == tiles.TileSet.Name)
                continue;

            map.Map.TileSetName = tiles.TileSet.Name;
            map.Touch();
        }
    }

    /// <summary>Los mapas que se dibujan con ese juego de tiles, tengan pestaña o no.</summary>
    public IReadOnlyList<MapEditorViewModel> MapsOf(TileSetEditorViewModel tiles) =>
        [.. _panels.Values.OfType<MapEditorViewModel>().Where(map => map.Map.TileSetId == tiles.TileSet.Id)];

    /// <summary>Abre las propiedades de un elemento del árbol en el lateral.</summary>
    /// <remarks>
    /// Se cambia el que hubiera abierto en vez de apilar otro, igual que con el editor de
    /// paletas: dos paneles de propiedades de cosas distintas sólo confunden.
    /// </remarks>
    [RelayCommand]
    private void ShowProperties(ItemTree? item)
    {
        if (item is not { IsPanelNode: true } || GetPanelFromDic(item.Tag) is not { } panel)
            return;

        ShowPropertiesOf(panel);
    }

    /// <summary>
    /// Abre las propiedades de un documento, venga del árbol o de su propio editor.
    /// </summary>
    /// <remarks>
    /// Suelto del comando del árbol porque hay dos puertas a lo mismo: el menú del nodo y
    /// el botón del editor de tiles. Ahí es donde de verdad se buscan las propiedades del
    /// juego que se está dibujando, y por el árbol no las encontraba nadie.
    /// </remarks>
    public void ShowPropertiesOf(PanelBaseViewModel panel)
    {
        if (!panel.IsDocument)
            return;

        if (RightPanels.OfType<EditPropertiesViewModel>().FirstOrDefault() is { } open)
            CloseRightPanel(open);

        RightPanViewModel = new EditPropertiesViewModel(this, panel);
    }

    /// <summary>Abre un mapa en una pestaña nueva y lo cuelga del árbol.</summary>
    public MapEditorViewModel OpenMap(TileMap map, TileSetEditorViewModel tiles)
    {
        map.TileSetId = tiles.TileSet.Id;
        map.TileSetName = tiles.TileSet.Name;

        var panel = new MapEditorViewModel(map, tiles, Preferences)
        {
            TagId = $"map{CurrentMapCounter}",
        };

        panel.RefreshHeader();

        CurrentMapCounter++;

        AddPanelToDic(panel);
        Tabs.Add(panel);
        SelectedTab = panel;
        TreeGeneralVm.AddMap(panel.Header, panel.TagId, panel);

        return panel;
    }

    public int CurrentMapCounter { get; set; }

    /// <summary>
    /// Abre un formulario del lateral, o trae el que ya estuviera abierto.
    /// </summary>
    /// <remarks>
    /// Sin esto, cada pulsación del botón dejaba otra pestaña igual. Se trae la que hay en
    /// vez de deshabilitar el botón: con el lateral sin el foco, un botón apagado no dice
    /// por qué y el usuario se queda mirándolo.
    /// </remarks>
    private void OpenForm<T>(Func<T> create)
        where T : PanelBaseViewModel
    {
        RightPanViewModel = RightPanels.OfType<T>().FirstOrDefault() ?? create();
    }

    /// <summary>Abre un juego de tiles en una pestaña nueva y lo cuelga del árbol.</summary>
    /// <param name="palette">
    /// La paleta que traía el fichero. Sin ella, la que enseñe la barra, que es la del
    /// documento que se estaba mirando: es la que se espera al crear un juego nuevo.
    /// </param>
    /// <param name="borderColorIndex">
    /// El borde guardado en el fichero. Sin él manda el del editor, que lo busca en la
    /// paleta: fijarlo aquí en el 1 daba por negro un índice que en una paleta generada
    /// a partir de un png es el primer color de la imagen.
    /// </param>
    public TileSetEditorViewModel OpenTileSet(
        TileSet tileSet, ColorPalette? palette = null, int? borderColorIndex = null)
    {
        var panel = new TileSetEditorViewModel(tileSet, palette ?? Palettes.ActivePalette, Preferences)
        {
            TagId = $"tls{CurrentTileSetCounter}",
        };

        panel.RefreshHeader();

        if (borderColorIndex is int border)
            panel.BorderColorIndex = border;

        // El panel de bloques nace con el juego aunque no se abra: es suyo, y asi el
        // nodo del arbol ya esta ahi con lo que trajera el fichero.
        var blocks = new TileBlocksViewModel(panel)
        {
            TagId = $"blk{CurrentTileSetCounter}",
            Header = $"{Localization.Localizer.Instance["TreeBlocks"]}: {tileSet.Name}",
        };

        CurrentTileSetCounter++;

        AddPanelToDic(panel);
        AddPanelToDic(blocks);
        Tabs.Add(panel);
        SelectedTab = panel;
        TreeGeneralVm.AddTileSet(panel.Header, panel.TagId, panel, blocks);

        return panel;
    }

    /// <summary>Abre un banco en una pestaña nueva y lo cuelga del árbol.</summary>
    /// <inheritdoc cref="OpenTileSet" path="/param[@name='palette']"/>
    /// <inheritdoc cref="OpenTileSet" path="/param[@name='borderColorIndex']"/>
    public SpritesEditorViewModel OpenSpriteBank(
        SpriteBank bank, ColorPalette? palette = null, int? backgroundColorIndex = null)
    {
        var panel = new SpritesEditorViewModel(
            bank, palette ?? Palettes.ActivePalette, Dialogs, Backgrounds, Preferences, SpritesInHand)
        {
            TagId = $"spb{CurrentSpriteBankCounter}",
        };

        panel.RefreshHeader();

        if (backgroundColorIndex is int background)
            panel.BackgroundColorIndex = background;

        CurrentSpriteBankCounter++;

        AddPanelToDic(panel);
        Tabs.Add(panel);
        SelectedTab = panel;
        TreeGeneralVm.AddSpriteBank(panel.Header, panel.TagId, panel);

        return panel;
    }

    // ------------------------------------------------------------------ ajustes

    /// <summary>
    /// Aplica los ajustes guardados. Lo llama la aplicación antes de enseñar la ventana.
    /// </summary>
    /// <remarks>
    /// El idioma vacío es «el que hable la máquina», que es con el que arranca el
    /// <see cref="Localization.Localizer"/> mientras nadie diga otra cosa.
    /// </remarks>
    public void LoadSettings()
    {
        if (_settings is null)
            return;

        Settings settings = _settings.Load();

        if (settings.Language.Length > 0)
            Localization.Localizer.Instance.Language = settings.Language;

        Preferences.CopyFrom(settings.Preferences);
        Recent.Reset(settings.Recent);

        // Y a mano además de por el aviso de arriba: si lo guardado coincide con lo que ya
        // había, el setter no avisa de nada y la variante se quedaría sin aplicar.
        AppTheme.Apply(Preferences.ThemeVariant);
    }

    /// <summary>Guarda los ajustes. Se llama al aceptar las preferencias y al salir.</summary>
    public void SaveSettings() =>
        _settings?.Save(new Settings(
            Localization.Localizer.Instance.Language,
            Preferences,
            [.. Recent.Items]));

    /// <summary>
    /// Apunta algo recién abierto y lo deja escrito en el acto.
    /// </summary>
    /// <remarks>
    /// Sin esperar a cerrar la ventana. Esta lista existe justo para las sesiones de
    /// probar y volver a cargar, que son las que acaban a lo bruto: si sólo se guardara al
    /// salir por la puerta, se perdería en el caso para el que se hizo.
    /// </remarks>
    private void Remember(string path, RecentKind kind)
    {
        Recent.Add(path, kind);
        SaveSettings();
    }

    /// <summary>
    /// Vuelve a abrir algo de la lista de recientes.
    /// </summary>
    /// <remarks>
    /// Un proyecto sustituye todo lo abierto y por eso avisa antes, igual que si se
    /// hubiera entrado por Abrir proyecto; un documento se suma a lo que haya.
    /// </remarks>
    [RelayCommand]
    private async Task OpenRecentAsync(RecentItem? item)
    {
        if (item is null)
            return;

        // Se mira antes de intentarlo para poder quitarlo: lo normal es que un reciente
        // que ya no está sea uno que se movió o se borró, y dejarlo ahí para que vuelva a
        // fallar mañana no ayuda a nadie.
        if (!File.Exists(item.Path))
        {
            Recent.Remove(item.Path);
            SaveSettings();

            await Dialogs.ShowMessageAsync(
                Text["RecentMissingTitle"],
                Text.Format("RecentMissingBody", item.Path));

            return;
        }

        if (item.Kind == RecentKind.Project)
        {
            if (!await ConfirmDiscardAsync("DiscardOnOpen", "DiscardOnOpenSave", "DiscardOnOpenDrop"))
                return;

            await OpenProjectPathAsync(item.Path);

            return;
        }

        await OpenPathAsync(item.Path);
    }

    [RelayCommand]
    private void ClearRecent()
    {
        Recent.Clear();
        SaveSettings();
    }

    /// <summary>Abre las preferencias en el lateral.</summary>
    [RelayCommand]
    private void ShowPreferences() => OpenForm(() => new EditPreferencesViewModel(this));

    // ------------------------------------------------------------------ el proyecto

    /// <summary>El fichero del proyecto, o nulo mientras no se haya guardado ninguno.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProjectName))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string? _projectPath;

    /// <summary>El índice tal y como quedó la última vez que se escribió.</summary>
    private string? _savedProjectText;

    public string ProjectName =>
        ProjectPath is null ? "Sin proyecto" : Path.GetFileNameWithoutExtension(ProjectPath);

    public string WindowTitle => $"MSX Game Tools — {ProjectName}";

    /// <summary>Los documentos del proyecto, estén su pestaña abierta o no.</summary>
    private IEnumerable<PanelBaseViewModel> Documents => _panels.Values.Where(panel => panel.IsDocument);

    /// <summary>
    /// Si el índice del proyecto se diferencia del que hay escrito.
    /// </summary>
    /// <remarks>
    /// Es lo mismo que hace un documento con su fichero: se construye lo que se
    /// escribiría y se compara. Cambia al añadir o quitar un documento, al guardar uno en
    /// otro sitio, y al crear o eliminar una paleta.
    /// </remarks>
    private bool HasProjectChanges()
    {
        if (ProjectPath is null)
            return false;

        // Lo que todavía no tiene fichero no sale en el índice, pero guardar el proyecto le
        // pondría uno y lo metería dentro: para el proyecto eso es un cambio, aunque el
        // documento esté recién creado y vacío.
        if (Documents.Any(document => document.FilePath is null))
            return true;

        return ProjectSerializer.Serialize(BuildProject(ProjectPath)) != _savedProjectText;
    }

    [RelayCommand]
    private Task SaveProject() => WriteProjectAsync(askForPath: false);

    [RelayCommand]
    private Task SaveProjectAs() => WriteProjectAsync(askForPath: true);

    /// <summary>
    /// Escribe el proyecto: primero sus documentos y después el índice.
    /// </summary>
    /// <remarks>
    /// A los documentos que todavía no tienen fichero se les pone uno con su nombre junto
    /// al proyecto, en vez de encadenar un selector por cada uno: guardar el proyecto es
    /// un gesto, no una ronda de preguntas. Quien quiera otro sitio para uno concreto
    /// tiene Guardar como en su pestaña.
    /// </remarks>
    /// <returns><c>false</c> si se canceló o algo no se pudo escribir.</returns>
    private async Task<bool> WriteProjectAsync(bool askForPath)
    {
        string? path = askForPath || ProjectPath is null
            ? await Dialogs.PickFileToSaveAsync(
                Text["PickSaveProject"],
                $"{CleanFileName(ProjectName)}{ProjectSerializer.Extension}",
                PickerFileKind.Project)
            : ProjectPath;

        if (path is null)
            return false;

        string folder = Path.GetDirectoryName(path) ?? string.Empty;
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (PanelBaseViewModel document in Documents)
        {
            bool isNew = document.FilePath is null;

            if (isNew)
                document.FilePath = FreeFileName(folder, document.DocumentName, taken);
            else
                taken.Add(document.FilePath!);

            // Los que ya estaban guardados y no se han tocado no se reescriben: el
            // proyecto no tiene por qué cambiarle la fecha a todo lo que agrupa.
            if ((isNew || document.HasUnsavedChanges()) && !await SaveAsync(document, askForPath: false))
                return false;
        }

        string text = ProjectSerializer.Serialize(BuildProject(path));

        try
        {
            await File.WriteAllTextAsync(path, text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorSaveProject"], exception.Message);

            return false;
        }

        ProjectPath = path;
        _savedProjectText = text;

        return true;
    }

    /// <summary>
    /// El índice de lo que hay abierto, con las rutas relativas a ese fichero de proyecto.
    /// </summary>
    /// <remarks>
    /// En un orden fijo y no en el que se abrieron: así el índice sólo cambia cuando
    /// cambia lo que dice, y compararlo para saber si hay que guardarlo significa algo.
    /// </remarks>
    private Project BuildProject(string projectPath)
    {
        string folder = Path.GetDirectoryName(projectPath) ?? string.Empty;

        IEnumerable<ProjectItem> items = Documents
            .Where(document => document.FilePath is not null)
            .Select(document => new ProjectItem(
                KindOf(document), Path.GetRelativePath(folder, document.FilePath!)))
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase);

        return new Project(
            Path.GetFileNameWithoutExtension(projectPath),
            [.. items],

            // La estándar no: existe siempre y no es de nadie.
            [.. Palettes.Palettes.Where(palette => !palette.IsReadOnly)],
            [.. Backgrounds.Images.Select(image => new BackgroundImageRef(image.Path, image.CellSize))]);
    }

    private static ProjectItemKind KindOf(PanelBaseViewModel document) => document switch
    {
        TileSetEditorViewModel => ProjectItemKind.TileSet,
        SpritesEditorViewModel => ProjectItemKind.SpriteBank,
        _ => ProjectItemKind.Map,
    };

    /// <summary>Un nombre de fichero libre en esa carpeta, a partir del del documento.</summary>
    private static string FreeFileName(string folder, string documentName, HashSet<string> taken)
    {
        string stem = CleanFileName(documentName);

        for (int number = 1; ; number++)
        {
            string candidate = Path.Combine(folder, number == 1 ? $"{stem}.json" : $"{stem} {number}.json");

            // Los ya cogidos por otro documento además de los que haya en disco. Con lo de
            // disco solo bastaría casi siempre, porque cada uno se escribe antes de que el
            // siguiente pida nombre; pero si alguien ha borrado un fichero por fuera, su
            // documento sigue apuntando ahí y otro se lo quedaría.
            if (taken.Add(candidate) && !File.Exists(candidate))
                return candidate;
        }
    }

    /// <summary>
    /// Abre un proyecto, cerrando lo que hubiera.
    /// </summary>
    /// <remarks>
    /// Sustituye en vez de añadir: un proyecto es todo lo que se está haciendo, y mezclar
    /// dos dejaría un árbol que no es ninguno de los dos y no se sabría guardar.
    /// </remarks>
    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        if (!await ConfirmDiscardAsync("DiscardOnOpen", "DiscardOnOpenSave", "DiscardOnOpenDrop"))
            return;

        string? path = await Dialogs.PickFileToOpenAsync(Text["PickOpenProject"], PickerFileKind.Project);
        if (path is null)
            return;

        await OpenProjectPathAsync(path);
    }

    /// <summary>Abre un proyecto del que ya se sabe la ruta, sin volver a preguntar.</summary>
    /// <remarks>
    /// El aviso de cambios sin guardar lo da quien elige el fichero, que es quien sabe si
    /// venimos del menú de abrir o del de recientes.
    /// </remarks>
    private async Task OpenProjectPathAsync(string path)
    {
        Project project;

        try
        {
            project = ProjectSerializer.Deserialize(await File.ReadAllTextAsync(path));
        }
        catch (FileFormatException exception)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorProjectInvalid"], exception.Message);

            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorOpenFile"], exception.Message);

            return;
        }

        CloseEverything();

        foreach (ColorPalette palette in project.Palettes)
            Palettes.Adopt(palette);

        await LoadBackgroundsAsync(project.Backgrounds);

        string folder = Path.GetDirectoryName(path) ?? string.Empty;
        var missing = new List<string>();

        // Por tipo y no en el orden del fichero: un mapa necesita su juego de tiles ya
        // abierto para saber con qué se dibuja.
        foreach (ProjectItem item in project.Items.OrderBy(item => item.Kind))
            await OpenProjectItemAsync(item, folder, missing);

        ProjectPath = path;
        _savedProjectText = ProjectSerializer.Serialize(BuildProject(path));

        // Sólo el proyecto, no lo que trae dentro: quien abre un proyecto de veinte piezas
        // ha abierto una cosa, y apuntar las veinte dejaría la lista inservible de un golpe.
        Remember(path, RecentKind.Project);

        // Se abre lo que se pueda y se dice qué ha faltado: que un fichero se haya movido
        // no es razón para quedarse sin el resto del proyecto.
        if (missing.Count > 0)
        {
            await Dialogs.ShowMessageAsync(
                Text["MissingItemsTitle"],
                Text.Format("MissingItemsBody", string.Join(Environment.NewLine, missing)));
        }
    }

    private async Task OpenProjectItemAsync(ProjectItem item, string folder, List<string> missing)
    {
        string path = Path.GetFullPath(Path.Combine(folder, item.Path));

        try
        {
            string json = await File.ReadAllTextAsync(path);

            switch (item.Kind)
            {
                case ProjectItemKind.TileSet:
                    LoadedTileSet tiles = TileSetSerializer.Deserialize(json);

                    OpenTileSet(tiles.TileSet, Palettes.Adopt(tiles.Palette), tiles.BorderColorIndex)
                        .MarkSaved(path);

                    break;

                case ProjectItemKind.SpriteBank:
                    LoadedSpriteBank bank = SpriteBankSerializer.Deserialize(json);

                    await LoadBackgroundsAsync(bank.Backgrounds);
                    OpenSpriteBank(bank.Bank, Palettes.Adopt(bank.Palette), bank.BackgroundColorIndex)
                        .MarkSaved(path);

                    break;

                default:
                    TileMap map = MapSerializer.Deserialize(json);

                    if (TileSetOf(map) is not { } owner)
                    {
                        missing.Add(Text.Format("MissingItemTileSet", item.Path, map.TileSetName));

                        return;
                    }

                    OpenMap(map, owner).MarkSaved(path);

                    break;
            }
        }
        catch (Exception exception)
            when (exception is FileFormatException or IOException or UnauthorizedAccessException)
        {
            missing.Add(Text.Format("MissingItemReason", item.Path, exception.Message));
        }
    }

    /// <summary>
    /// Empieza un proyecto de cero.
    /// </summary>
    /// <remarks>
    /// Hasta ahora la única forma de empezar uno era reiniciar el programa.
    /// </remarks>
    [RelayCommand]
    private async Task NewProjectAsync()
    {
        if (!await ConfirmDiscardAsync("DiscardOnNew", "DiscardOnNewSave", "DiscardOnNewDrop"))
            return;

        CloseEverything();

        ProjectPath = null;
        _savedProjectText = null;
    }

    /// <summary>Deja el editor como recién arrancado, sin documentos ni árbol.</summary>
    /// <remarks>
    /// Las paletas y las imágenes de referencia también se van: son del proyecto que se
    /// está cerrando, y dejarlas puestas metería las de uno dentro del siguiente.
    /// </remarks>
    private void CloseEverything()
    {
        while (RightPanels.Count > 0)
            CloseRightPanel(RightPanels[^1]);

        SelectedTab = null;
        Tabs.Clear();
        _panels.Clear();
        TreeGeneralVm.Clear();

        // La estándar no se puede quitar y no hace falta: existe siempre y no es de nadie.
        foreach (ColorPalette palette in Palettes.Palettes.Where(palette => !palette.IsReadOnly).ToList())
            Palettes.Remove(palette);

        foreach (ReferenceImage image in Backgrounds.Images.ToList())
            Backgrounds.Remove(image);

        // Los identificadores se reparten por contador y el diccionario está vacío: si no
        // volvieran a empezar, el árbol del proyecto nuevo heredaría los números del viejo.
        CurrentSpriteBankCounter = 0;
        CurrentTileSetCounter = 0;
        CurrentMapCounter = 0;
    }

    // ------------------------------------------------------------------ guardar

    /// <summary>
    /// Guarda el documento que esté delante en el fichero del que salió.
    /// </summary>
    /// <remarks>
    /// Si todavía no tiene fichero se pide uno, que es lo que espera cualquiera al pulsar
    /// Guardar en algo recién creado; a partir de ahí ya no vuelve a preguntar.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanSaveDocument))]
    private Task SaveDocumentAsync() => SaveAsync(SelectedTab, askForPath: false);

    [RelayCommand(CanExecute = nameof(CanSaveDocument))]
    private Task SaveDocumentAsAsync() => SaveAsync(SelectedTab, askForPath: true);

    private bool CanSaveDocument() => SelectedTab is { IsDocument: true };

    /// <summary>
    /// Escribe un documento y apunta dónde ha quedado.
    /// </summary>
    /// <remarks>
    /// Lo que se escribe es exactamente lo que el panel compara para saber si tiene
    /// cambios sin guardar, así que después de esto no puede quedarse marcado por error.
    /// Da igual de qué tipo sea: un banco, un juego de tiles y un mapa se guardan aquí.
    /// </remarks>
    /// <returns><c>false</c> si se canceló el selector o no se pudo escribir.</returns>
    private async Task<bool> SaveAsync(PanelBaseViewModel? panel, bool askForPath)
    {
        if (panel is not { IsDocument: true })
            return false;

        // La clave lleva el tipo al final: la frase entera está escrita una vez por tipo.
        string? path = askForPath || panel.FilePath is null
            ? await Dialogs.PickFileToSaveAsync(
                Text[$"PickSave{panel.KindKey}"], SuggestedFileName(panel.DocumentName))
            : panel.FilePath;

        if (path is null)
            return false;

        string text = panel.ToFileText();

        try
        {
            await File.WriteAllTextAsync(path, text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text[$"ErrorSave{panel.KindKey}"], exception.Message);

            return false;
        }

        panel.MarkSaved(path, text);

        return true;
    }

    /// <summary>
    /// Los documentos que se perderían ahora mismo, comprobando su contenido de verdad.
    /// </summary>
    /// <remarks>
    /// Se mira todo el proyecto y no sólo las pestañas abiertas: cerrar una pestaña no
    /// cierra el documento, sigue vivo en el árbol y su trabajo sigue sin guardar.
    /// </remarks>
    public IReadOnlyList<PanelBaseViewModel> UnsavedDocuments() =>
        [.. _panels.Values.Where(panel => panel.HasUnsavedChanges())];

    /// <summary>Si hay algo que perder, para saber si hace falta preguntar antes de cerrar.</summary>
    public bool HasAnythingToLose() => UnsavedDocuments().Count > 0 || HasProjectChanges();

    /// <summary>
    /// Pregunta qué hacer con lo que está sin guardar antes de cerrar.
    /// </summary>
    /// <remarks>
    /// Las tres salidas son necesarias: guardar y salir, salir perdiéndolo, y volver atrás.
    /// Si al guardar se cancela un selector de fichero no se sale, que sería tirar justo lo
    /// que se acababa de pedir conservar.
    /// </remarks>
    /// <returns><c>true</c> si se puede cerrar.</returns>
    public Task<bool> ConfirmExitAsync() =>
        ConfirmDiscardAsync("DiscardOnExit", "DiscardOnExitSave", "DiscardOnExitDrop");

    /// <inheritdoc cref="ConfirmExitAsync"/>
    /// <param name="headline">Clave de qué se va a hacer: cambia entre salir y abrir otro.</param>
    /// <param name="saveLabel">Clave de lo que dice el botón de guardar antes de seguir.</param>
    /// <param name="discardLabel">Clave de lo que dice el de seguir perdiéndolo.</param>
    private async Task<bool> ConfirmDiscardAsync(string headline, string saveLabel, string discardLabel)
    {
        IReadOnlyList<PanelBaseViewModel> pending = UnsavedDocuments();
        bool projectChanged = HasProjectChanges();

        if (pending.Count == 0 && !projectChanged)
            return true;

        IEnumerable<string> lines = pending.Select(panel => $"  · {panel.DocumentName} ({panel.DocumentKind})");

        // El proyecto aparte de sus documentos: puede estar sin guardar sólo porque se
        // haya añadido o quitado alguno, sin que ninguno tenga cambios.
        if (projectChanged)
            lines = lines.Append($"  · {ProjectName} ({Text["KindProject"]})");

        bool? save = await Dialogs.ChooseAsync(
            Text["UnsavedTitle"],
            Text.Format("UnsavedList", Text[headline], string.Join(Environment.NewLine, lines)),
            Text[saveLabel],
            Text[discardLabel]);

        if (save is null)
            return false;

        if (save is false)
            return true;

        // Con un proyecto abierto, guardarlo guarda además todos sus documentos y les
        // pone fichero a los que no tengan: es un solo paso en vez de uno por pestaña.
        if (ProjectPath is not null)
            return await WriteProjectAsync(askForPath: false);

        foreach (PanelBaseViewModel panel in pending)
        {
            if (!await SaveAsync(panel, askForPath: false))
                return false;
        }

        return true;
    }

    private bool IsSpriteBankSelected() => SelectedTab is SpritesEditorViewModel;

    /// <summary>Abre un banco que ya se ha leído del disco.</summary>
    private async Task ReadSpriteBankAsync(string json, string path)
    {
        LoadedSpriteBank loaded = SpriteBankSerializer.Deserialize(json);
        ColorPalette palette = Palettes.Adopt(loaded.Palette);

        await LoadBackgroundsAsync(loaded.Backgrounds);
        OpenSpriteBank(loaded.Bank, palette, loaded.BackgroundColorIndex).MarkSaved(path);
    }

    /// <summary>
    /// Crea una copia editable de la que enseña la barra y abre su editor.
    /// </summary>
    /// <remarks>
    /// La copia pasa a estar seleccionada, así que el documento que esté delante se queda
    /// con ella: es lo que se quiere al duplicar una paleta para retocarla sin estropear la
    /// original ni los demás documentos que la usen.
    /// </remarks>
    [RelayCommand]
    private void AddPalette() => OpenForm(() => new NewPaletteViewModel(this));

    /// <summary>Crea la paleta que pide el formulario y la deja abierta para editarla.</summary>
    public void CreatePalette(string name, ColorPalette from) =>
        OpenPaletteEditor(Palettes.Add(name, from));

    [RelayCommand(CanExecute = nameof(CanEditPalette))]
    private void EditPalette() => OpenPaletteEditor(Palettes.ActivePalette);

    /// <summary>
    /// Abre el editor de esa paleta, sin dejar dos editores abiertos.
    /// </summary>
    /// <remarks>
    /// Si el que hay es de otra paleta se cambia por éste: dos editores de paleta a la vez
    /// no sirven para nada y se acaban apilando pestañas iguales.
    /// </remarks>
    private void OpenPaletteEditor(ColorPalette palette)
    {
        if (RightPanels.OfType<EditPaletteViewModel>().FirstOrDefault() is { } open)
        {
            if (open.Palette == palette)
            {
                RightPanViewModel = open;
                return;
            }

            CloseRightPanel(open);
        }

        RightPanViewModel = new EditPaletteViewModel(this, palette);
    }

    private bool CanEditPalette() => !Palettes.ActivePalette.IsReadOnly;

    [RelayCommand(CanExecute = nameof(CanDeletePalette))]
    private async Task DeletePaletteAsync()
    {
        ColorPalette palette = Palettes.ActivePalette;

        // Eliminar una paleta no se puede deshacer, y el botón está pegado a los
        // otros dos: mejor un clic de más que perder el trabajo.
        bool confirmed = await Dialogs.ConfirmAsync(
            Text["DeletePaletteTitle"],
            Text.Format("DeletePaletteBody", palette.Name),
            Text["DeleteLabel"]);

        if (!confirmed)
            return;

        if (RightPanViewModel is EditPaletteViewModel editing && editing.Palette == palette)
            RightPanViewModel = null;

        if (!Palettes.Remove(palette))
            return;

        // Los documentos que dibujaban con ella se quedan con la que haya pasado a estar
        // seleccionada: si no, seguirían con una paleta que ya no está en la biblioteca y
        // la barra no podría enseñarla al volver a su pestaña.
        foreach (IPaletteDocument document in PaletteDocuments)
        {
            if (ReferenceEquals(document.ColorPalette, palette))
                document.ColorPalette = Palettes.ActivePalette;
        }
    }

    private bool CanDeletePalette() => Palettes.CanRemove(Palettes.ActivePalette);

    /// <summary>
    /// Carga un png o jpg como imagen de referencia. Si no cabe en el lienzo de un grupo
    /// se pregunta con qué lado trocearla, y cada trozo pasa a ser un fondo.
    /// </summary>
    [RelayCommand]
    private async Task LoadBackgroundAsync()
    {
        string? path = await Dialogs.PickFileToOpenAsync(Text["PickLoadReference"], PickerFileKind.Image);
        if (path is null)
            return;

        try
        {
            PixelSize size = ReferenceImage.Measure(path);

            int cellSize = 0;

            if (size.Width > ReferenceImageSlicer.SingleTileMax || size.Height > ReferenceImageSlicer.SingleTileMax)
            {
                int? answer = await Dialogs.AskCellSizeAsync(
                    Text["CellSizeTitle"],
                    Text.Format("CellSizeBody", Path.GetFileName(path), size.Width, size.Height),
                    suggested: 16,
                    maximum: Math.Max(size.Width, size.Height));

                if (answer is not { } chosen)
                    return;

                cellSize = chosen;
            }

            Backgrounds.Load(path, cellSize);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorLoadImage"], exception.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteBackground))]
    private async Task DeleteBackgroundAsync()
    {
        if (Backgrounds.SelectedImage is not { } image)
            return;

        // Se borra la imagen entera, no un fondo suelto: de una hoja de sprites salen
        // decenas y quitarlos de uno en uno no serviría de nada. Por eso se dice cuántos
        // se lleva por delante.
        bool confirmed = await Dialogs.ConfirmAsync(
            Text["DeleteReferenceTitle"],
            Text.Format("DeleteReferenceBody", image.Tiles.Count, Path.GetFileName(image.Path)),
            Text["DeleteLabel"]);

        if (!confirmed)
            return;

        Backgrounds.Remove(image);
    }

    private bool CanDeleteBackground() => Backgrounds.SelectedImage is not null;

    /// <summary>
    /// Recarga las imágenes que traía un banco. Que falte una no impide abrirlo: los
    /// grupos que la usaran se quedan sin fondo y ya está.
    /// </summary>
    private async Task LoadBackgroundsAsync(IReadOnlyList<BackgroundImageRef> references)
    {
        var missing = new List<string>();

        foreach (BackgroundImageRef reference in references)
        {
            if (Backgrounds.Images.Any(image =>
                    string.Equals(image.Path, reference.Path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                Backgrounds.Load(reference.Path, reference.CellSize);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                missing.Add(Path.GetFileName(reference.Path));
            }
        }

        if (missing.Count > 0)
        {
            await Dialogs.ShowMessageAsync(
                Text["MissingReferencesTitle"],
                Text.Format("MissingReferencesBody", string.Join(", ", missing)));
        }
    }

    [RelayCommand]
    private async Task SavePaletteAsync()
    {
        ColorPalette palette = Palettes.ActivePalette;

        string? path = await Dialogs.PickFileToSaveAsync(Text["PickSavePalette"], SuggestedFileName(palette.Name));
        if (path is null)
            return;

        try
        {
            await File.WriteAllTextAsync(path, PaletteSerializer.Serialize(palette));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorSavePalette"], exception.Message);
        }
    }

    /// <summary>
    /// Abre un fichero del editor, sea de lo que sea.
    /// </summary>
    /// <remarks>
    /// Uno solo en vez de cuatro. Antes había que acertar con la entrada de menú que
    /// correspondía a tu fichero, y equivocarse decía que no era válido aunque estuviera
    /// perfecto; eso no es una pregunta que haya que hacerle a nadie, se mira el fichero.
    /// </remarks>
    [RelayCommand]
    private async Task OpenAsync()
    {
        string? path = await Dialogs.PickFileToOpenAsync(Text["PickOpen"]);
        if (path is null)
            return;

        await OpenPathAsync(path);
    }

    /// <summary>
    /// Abre un documento del que ya se sabe la ruta.
    /// </summary>
    /// <remarks>
    /// Separado de elegirlo para que un reciente entre por aquí. Si el menú de recientes
    /// tuviera su propia forma de abrir, tendría también sus propios fallos: lo que se
    /// reabre pasa por el mismo sitio que lo que se abre a mano.
    /// </remarks>
    private async Task OpenPathAsync(string path)
    {
        try
        {
            string json = await File.ReadAllTextAsync(path);

            switch (EditorFile.KindOf(json))
            {
                case EditorFileKind.SpriteBank:
                    await ReadSpriteBankAsync(json, path);
                    Remember(path, RecentKind.SpriteBank);
                    break;

                case EditorFileKind.TileSet:
                    ReadTileSet(json, path);
                    Remember(path, RecentKind.TileSet);
                    break;

                case EditorFileKind.Map:
                    await ReadMapAsync(json, path);
                    Remember(path, RecentKind.Map);
                    break;

                case EditorFileKind.Palette:
                    Palettes.Import(PaletteSerializer.Deserialize(json));
                    Remember(path, RecentKind.Palette);
                    break;

                default:
                    // Vale igual para un fichero de otra cosa que para uno estropeado: en
                    // los dos casos lo que se sabe es que no se reconoce lo que hay dentro.
                    await Dialogs.ShowMessageAsync(
                        Text["UnknownFileTitle"],
                        Text.Format("UnknownFileBody", Path.GetFileName(path)));

                    break;
            }
        }
        catch (FileFormatException exception)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorFileInvalid"], exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorOpenFile"], exception.Message);
        }
    }

    /// <summary>
    /// Con qué filtro se abre el selector al exportar, que es el formato que se va a
    /// escribir. La misma decisión que la extensión, y va junto a ella en cada llamada.
    /// </summary>
    private static PickerFileKind FormatOf(bool binary) =>
        binary ? PickerFileKind.Binary : PickerFileKind.Assembler;

    [RelayCommand(CanExecute = nameof(IsSpriteBankSelected))]
    private Task ExportSpriteBankBinaryAsync() => ExportSpriteBankAsync(binary: true);

    [RelayCommand(CanExecute = nameof(IsSpriteBankSelected))]
    private Task ExportSpriteBankAssemblerAsync() => ExportSpriteBankAsync(binary: false);

    /// <summary>
    /// Escribe las dos tablas. Se pide un nombre base y de ahí salen los dos ficheros,
    /// para no encadenar dos selectores seguidos.
    /// </summary>
    private async Task ExportSpriteBankAsync(bool binary)
    {
        if (SelectedTab is not SpritesEditorViewModel editor)
            return;

        SpriteBank bank = editor.SpritesBank;
        string extension = binary ? ".bin" : ".asm";

        string? path = await Dialogs.PickFileToSaveAsync(
            Text[binary ? "PickExportBinary" : "PickExportAssembler"],
            $"{SpriteBankExporter.LabelOf(bank.Name)}{extension}",
            FormatOf(binary));

        if (path is null)
            return;

        string folder = Path.GetDirectoryName(path) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(path);

        string patternsPath = Path.Combine(folder, $"{stem}_patterns{extension}");
        string groupsPath = Path.Combine(folder, $"{stem}_groups{extension}");

        try
        {
            if (binary)
            {
                await File.WriteAllBytesAsync(patternsPath, SpriteBankExporter.PatternsToBinary(bank));
                await File.WriteAllBytesAsync(groupsPath, SpriteBankExporter.GroupsToBinary(bank));
            }
            else
            {
                await File.WriteAllTextAsync(patternsPath, SpriteBankExporter.PatternsToAssembler(bank));
                await File.WriteAllTextAsync(groupsPath, SpriteBankExporter.GroupsToAssembler(bank));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorExportSpriteBank"], exception.Message);

            return;
        }

        // El nombre elegido se reparte en dos, asi que conviene decir cuales han salido.
        await Dialogs.ShowMessageAsync(
            Text["ExportedSpriteBankTitle"],
            Text.Format("ExportedTwoFiles", Path.GetFileName(patternsPath), Path.GetFileName(groupsPath)));
    }

    private bool IsTileSetSelected() => SelectedTab is TileSetEditorViewModel;

    /// <summary>
    /// Abre el panel de bloques del juego de tiles que esté delante.
    /// </summary>
    /// <remarks>
    /// Es el mismo panel que trae el doble clic en su nodo del árbol, no uno nuevo: los
    /// bloques son del juego y sólo hay unos. Existe porque el doble clic en un nodo no
    /// lo descubre nadie, y los bloques hacen falta mientras se dibujan los tiles.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(IsTileSetSelected))]
    private void ShowBlocks()
    {
        if (SelectedTab is TileSetEditorViewModel tiles && BlocksOf(tiles) is { } blocks)
            RightPanViewModel = blocks;
    }

    /// <summary>El panel de bloques de ese juego, que nació con él.</summary>
    private TileBlocksViewModel? BlocksOf(TileSetEditorViewModel tiles) =>
        _panels.Values
            .OfType<TileBlocksViewModel>()
            .FirstOrDefault(blocks => ReferenceEquals(blocks.TileSet, tiles.TileSet));

    /// <inheritdoc cref="ReadSpriteBankAsync"/>
    private void ReadTileSet(string json, string path)
    {
        LoadedTileSet loaded = TileSetSerializer.Deserialize(json);

        OpenTileSet(loaded.TileSet, Palettes.Adopt(loaded.Palette), loaded.BorderColorIndex).MarkSaved(path);
    }

    // ------------------------------------------------------------------ mapas

    private bool IsMapSelected() => SelectedTab is MapEditorViewModel;

    /// <summary>
    /// Abre un mapa que ya se ha leído del disco.
    /// </summary>
    /// <remarks>
    /// El juego de tiles no viene dentro: hace falta tenerlo abierto en el proyecto. Se
    /// dice cuál falta en vez de abrir un mapa que no se podría ni dibujar.
    /// </remarks>
    private async Task ReadMapAsync(string json, string path)
    {
        TileMap map = MapSerializer.Deserialize(json);

        if (TileSetOf(map) is not { } tileSet)
        {
            await Dialogs.ShowMessageAsync(
                Text["MissingTileSetTitle"],
                Text.Format("MissingTileSetBody", map.Name, map.TileSetName));

            return;
        }

        OpenMap(map, tileSet).MarkSaved(path);
    }

    [RelayCommand(CanExecute = nameof(IsMapSelected))]
    private async Task ExportMapCsvAsync()
    {
        if (SelectedTab is not MapEditorViewModel editor)
            return;

        string? path = await Dialogs.PickFileToSaveAsync(
            Text["PickExportMapCsv"],
            $"{CleanFileName(editor.Map.Name)}.csv",
            PickerFileKind.Csv);

        if (path is null)
            return;

        try
        {
            await File.WriteAllTextAsync(path, MapCsv.Write(editor.Map));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorExportMap"], exception.Message);
        }
    }

    /// <summary>
    /// Trae un csv como un mapa nuevo.
    /// </summary>
    /// <remarks>
    /// Un csv no dice con qué juego de tiles se dibuja, así que se coge el del mapa que
    /// esté delante; si no hay ninguno, el único juego abierto. Con varios y sin mapa
    /// delante no hay forma de adivinarlo y se pide que se elija abriendo uno.
    /// </remarks>
    [RelayCommand]
    private async Task ImportMapCsvAsync()
    {
        if (TileSetForImport() is not { } tileSet)
        {
            await Dialogs.ShowMessageAsync(
                Text["NoTileSetTitle"],
                Text["NoTileSetCsvBody"]);

            return;
        }

        string? path = await Dialogs.PickFileToOpenAsync(Text["PickImportMapCsv"], PickerFileKind.Any);
        if (path is null)
            return;

        try
        {
            TileMap map = MapCsv.Read(
                await File.ReadAllTextAsync(path),
                Path.GetFileNameWithoutExtension(path));

            map.BackgroundColorIndex = tileSet.ColorPalette.DefaultBackgroundIndex;

            OpenMap(map, tileSet);
        }
        catch (FileFormatException exception)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorCsvImport"], exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorOpenFile"], exception.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(IsMapSelected))]
    private void ResizeMap()
    {
        if (SelectedTab is MapEditorViewModel editor)
            OpenForm(() => new ResizeMapViewModel(this, editor));
    }

    [RelayCommand(CanExecute = nameof(IsMapSelected))]
    private void ReplaceTiles()
    {
        if (SelectedTab is MapEditorViewModel editor)
            OpenForm(() => new ReplaceTilesViewModel(this, editor));
    }

    [RelayCommand(CanExecute = nameof(IsMapSelected))]
    private Task ExportMapBinaryAsync() => ExportMapAsync(binary: true);

    [RelayCommand(CanExecute = nameof(IsMapSelected))]
    private Task ExportMapAssemblerAsync() => ExportMapAsync(binary: false);

    /// <summary>
    /// Escribe la tabla de nombres con su tamaño delante.
    /// </summary>
    /// <remarks>
    /// Avisa si el mapa tiene celdas vacías: en un byte no cabe el hueco y van a salir con
    /// el tile de relleno, que más vale decirlo que escribirlo en silencio.
    /// </remarks>
    private async Task ExportMapAsync(bool binary)
    {
        if (SelectedTab is not MapEditorViewModel editor)
            return;

        TileMap map = editor.Map;
        string extension = binary ? "bin" : "asm";

        string? path = await Dialogs.PickFileToSaveAsync(
            Text[binary ? "PickExportMapBinary" : "PickExportMapAssembler"],
            $"{CleanFileName(map.Name)}.{extension}",
            FormatOf(binary));

        if (path is null)
            return;

        try
        {
            if (binary)
                await File.WriteAllBytesAsync(path, MapExporter.ToBinary(map));
            else
                await File.WriteAllTextAsync(path, MapExporter.ToAssembler(map));

            if (HasEmptyCells(map))
            {
                await Dialogs.ShowMessageAsync(
                    Text["ExportedMapTitle"],
                    Text.Format("ExportedMapEmptyBody", map.EmptyTile));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorExportMap"], exception.Message);
        }
    }

    private static bool HasEmptyCells(TileMap map)
    {
        TileGrid flat = map.Flatten();

        for (int row = 0; row < flat.Height; row++)
        {
            for (int column = 0; column < flat.Width; column++)
            {
                if (flat[column, row] is null)
                    return true;
            }
        }

        return false;
    }

    /// <inheritdoc cref="ImportMapCsvAsync"/>
    [RelayCommand]
    private async Task ImportMapBinaryAsync()
    {
        if (TileSetForImport() is not { } tileSet)
        {
            await Dialogs.ShowMessageAsync(
                Text["NoTileSetTitle"],
                Text["NoTileSetBinaryBody"]);

            return;
        }

        string? path = await Dialogs.PickFileToOpenAsync(Text["PickImportMapBinary"], PickerFileKind.Any);
        if (path is null)
            return;

        try
        {
            TileMap map = MapExporter.FromBinary(
                await File.ReadAllBytesAsync(path),
                Path.GetFileNameWithoutExtension(path));

            map.BackgroundColorIndex = tileSet.ColorPalette.DefaultBackgroundIndex;

            OpenMap(map, tileSet);
        }
        catch (FileFormatException exception)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorBinaryImport"], exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorOpenFile"], exception.Message);
        }
    }

    /// <summary>
    /// Con qué juego de tiles se dibuja lo que se importa.
    /// </summary>
    /// <remarks>
    /// Manda lo que haya delante, que es la señal más explícita que puede dar el usuario:
    /// el juego que tenga abierto, o el del mapa que esté editando. Sólo cuando no hay ni
    /// una cosa ni la otra se recurre a que haya un único juego en el proyecto.
    /// </remarks>
    private TileSetEditorViewModel? TileSetForImport() => SelectedTab switch
    {
        TileSetEditorViewModel tiles => tiles,
        MapEditorViewModel map => TileSetOf(map.Map),
        _ => TileSets.Count == 1 ? TileSets[0] : null,
    };

    [RelayCommand(CanExecute = nameof(IsTileSetSelected))]
    private Task ExportTileSetBinaryAsync() => ExportTileSetAsync(binary: true);

    [RelayCommand(CanExecute = nameof(IsTileSetSelected))]
    private Task ExportTileSetAssemblerAsync() => ExportTileSetAsync(binary: false);

    /// <summary>
    /// Escribe la tabla de patrones y la de colores. Igual que con los bancos, se pide un
    /// nombre base y de ahí salen los dos ficheros.
    /// </summary>
    private async Task ExportTileSetAsync(bool binary)
    {
        if (SelectedTab is not TileSetEditorViewModel editor)
            return;

        TileSet tileSet = editor.TileSet;
        string extension = binary ? ".bin" : ".asm";

        string? path = await Dialogs.PickFileToSaveAsync(
            Text[binary ? "PickExportBinary" : "PickExportAssembler"],
            $"{SpriteBankExporter.LabelOf(tileSet.Name)}{extension}",
            FormatOf(binary));

        if (path is null)
            return;

        string folder = Path.GetDirectoryName(path) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(path);

        string patternsPath = Path.Combine(folder, $"{stem}_patterns{extension}");
        string colorsPath = Path.Combine(folder, $"{stem}_colors{extension}");

        // La tabla de supertiles sale con el juego y no con el mapa: es del juego, y todos
        // los mapas dibujados con el comparten la misma. Con cada mapa se repetiria igual.
        string superPath = Path.Combine(folder, $"{stem}_supertiles{extension}");

        // Y la de atributos sólo si se han definido: quien no los usa no tiene por qué
        // encontrarse un fichero de 256 ceros que no sabe para qué es.
        string attributesPath = Path.Combine(folder, $"{stem}_attributes{extension}");

        try
        {
            if (binary)
            {
                await File.WriteAllBytesAsync(patternsPath, TileSetExporter.PatternsToBinary(tileSet));
                await File.WriteAllBytesAsync(colorsPath, TileSetExporter.ColorsToBinary(tileSet));

                if (tileSet.HasSuperTiles)
                    await File.WriteAllBytesAsync(superPath, SuperTileExporter.ToBinary(tileSet));

                if (tileSet.AttributeNames.Any)
                    await File.WriteAllBytesAsync(attributesPath, TileSetExporter.AttributesToBinary(tileSet));
            }
            else
            {
                await File.WriteAllTextAsync(patternsPath, TileSetExporter.PatternsToAssembler(tileSet));
                await File.WriteAllTextAsync(colorsPath, TileSetExporter.ColorsToAssembler(tileSet));

                if (tileSet.HasSuperTiles)
                    await File.WriteAllTextAsync(superPath, SuperTileExporter.ToAssembler(tileSet));

                if (tileSet.AttributeNames.Any)
                    await File.WriteAllTextAsync(attributesPath, TileSetExporter.AttributesToAssembler(tileSet));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorExportTileSet"], exception.Message);

            return;
        }

        string done = Text.Format(
            "ExportedTileSetBody",
            Path.GetFileName(patternsPath),
            Path.GetFileName(colorsPath),
            TileSetExporter.ScreenThirds);

        if (tileSet.HasSuperTiles)
        {
            done += " " + Text.Format(
                "ExportedSuperTiles", Path.GetFileName(superPath), SuperTileExporter.CountOf(tileSet));

            // Una celda del mapa es un byte, asi que de 256 para arriba hay supertiles que
            // ningun mapa puede nombrar. Mejor decirlo que dejar una tabla que no cuadra.
            if (tileSet.Blocks.Count > SuperTileExporter.MaxSuperTiles)
            {
                done += " " + Text.Format(
                    "ExportedSuperTilesTooMany",
                    tileSet.Blocks.Count,
                    SuperTileExporter.MaxSuperTiles);
            }
        }

        await Dialogs.ShowMessageAsync(Text["ExportedTileSetTitle"], done);
    }

    [RelayCommand]
    private Task ExportPaletteBinaryAsync() => ExportPaletteAsync(binary: true);

    [RelayCommand]
    private Task ExportPaletteAssemblerAsync() => ExportPaletteAsync(binary: false);

    /// <summary>
    /// Escribe la paleta que enseña la barra en el formato del registro del V9938.
    /// </summary>
    /// <remarks>
    /// Un solo fichero, a diferencia de los bancos y los tilesets: son 32 bytes y no hay
    /// dos tablas que separar.
    /// </remarks>
    private async Task ExportPaletteAsync(bool binary)
    {
        ColorPalette palette = Palettes.ActivePalette;
        string extension = binary ? ".bin" : ".asm";

        string? path = await Dialogs.PickFileToSaveAsync(
            Text[binary ? "PickExportPaletteBinary" : "PickExportPaletteAssembler"],
            $"{SpriteBankExporter.LabelOf(palette.Name)}_palette{extension}",
            FormatOf(binary));

        if (path is null)
            return;

        try
        {
            if (binary)
                await File.WriteAllBytesAsync(path, PaletteExporter.ToBinary(palette));
            else
                await File.WriteAllTextAsync(path, PaletteExporter.ToAssembler(palette));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorExportPalette"], exception.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(IsTileSetSelected))]
    private async Task ExportTileSetPngAsync()
    {
        if (SelectedTab is not TileSetEditorViewModel editor)
            return;

        string? path = await Dialogs.PickFileToSaveAsync(
            Text["PickExportTileSetPng"],
            $"{SpriteBankExporter.LabelOf(editor.TileSet.Name)}.png",
            PickerFileKind.Image);

        if (path is null)
            return;

        try
        {
            PngFile.Write(
                path,
                TileSetPngConverter.ToPixels(editor.TileSet, editor.ColorPalette),
                TileSetPngConverter.FullSize);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorExportPng"], exception.Message);
        }
    }

    /// <summary>
    /// Trae un png como juego de tiles, comprobando antes que se puede.
    /// </summary>
    /// <remarks>
    /// Se rechaza en vez de aproximar en silencio: una imagen que no cumple las reglas de
    /// GRAPHIC 2 se puede convertir igualmente, pero el resultado no se parece a lo que
    /// dibujaste y no sabrías por qué. Es mejor decir dónde está el problema.
    /// </remarks>
    [RelayCommand]
    private async Task ImportTileSetPngAsync()
    {
        string? path = await Dialogs.PickFileToOpenAsync(Text["PickImportTileSetPng"], PickerFileKind.Image);
        if (path is null)
            return;

        int[] pixels;
        PixelSize size;

        try
        {
            (pixels, size) = PngFile.Read(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorOpenImage"], exception.Message);

            return;
        }

        string name = Path.GetFileNameWithoutExtension(path);

        ColorPalette? palette = await ChoosePaletteForImportAsync(pixels, name);
        if (palette is null)
            return;

        // Después de la paleta y no antes: si la imagen trae más colores de los que caben, ahí
        // ya se ha rechazado, y no hay por qué preguntar el modo de un juego que no va a
        // existir. Se pregunta por lo mismo que al crearlo a mano: no se puede cambiar después.
        bool? graphic2 = await Dialogs.ChooseAsync(
            Text["ImportTileSetModeTitle"],
            Text["ImportTileSetModeBody"],
            Text["NewTileSetModeGraphic2"],
            Text["NewTileSetModeGraphic1"]);

        if (graphic2 is not bool chosen)
            return;

        TileSet.GraphicMode mode = chosen
            ? TileSet.GraphicMode.Graphic2
            : TileSet.GraphicMode.Graphic1;

        TileSetImportResult result = TileSetPngConverter.Analyse(pixels, size, palette, name, mode);

        if (!result.Ok)
        {
            await Dialogs.ShowMessageAsync(Text["ErrorImageImport"], Describe(result));

            return;
        }

        OpenTileSet(result.TileSet!, Palettes.Adopt(palette));
    }

    /// <summary>
    /// Decide con qué paleta se lee la imagen: la activa, o una nueva con sus colores.
    /// </summary>
    /// <remarks>
    /// Sólo se ofrece generar cuando los colores caben, y caben quince y no dieciséis: el
    /// índice 0 del VDP es transparente y darle un color haría que esos pixeles enseñaran
    /// el borde en la máquina.
    /// </remarks>
    private async Task<ColorPalette?> ChoosePaletteForImportAsync(int[] pixels, string name)
    {
        IReadOnlyList<Color> colors = TileSetPngConverter.DistinctColors(pixels);

        if (colors.Count > TileSetPngConverter.MaxGeneratedColors)
        {
            await Dialogs.ShowMessageAsync(
                Text["TooManyColorsTitle"],
                Text.Format("TooManyColorsBody", colors.Count, TileSetPngConverter.MaxGeneratedColors));

            return null;
        }

        bool? generate = await Dialogs.ChooseAsync(
            Text["ImageColorsTitle"],
            Text.Format("ImageColorsBody", colors.Count, Palettes.ActivePalette.Name),
            Text["ImageColorsNew"],
            Text["ImageColorsExisting"]);

        return generate switch
        {
            null => null,
            true => TileSetPngConverter.BuildPalette(Text.Format("ImportedPngSuffix", name), colors),
            false => Palettes.ActivePalette,
        };
    }

    private static string Describe(TileSetImportResult result)
    {
        IEnumerable<string> lines = result.Problems
            .Take(TileSetPngConverter.MaxReportedProblems)
            .Select(problem => problem.Message);

        string text = string.Join(Environment.NewLine, lines);

        return result.Problems.Count > TileSetPngConverter.MaxReportedProblems
            ? $"{text}{Environment.NewLine}...y alguno más."
            : text;
    }

    /// <summary>El nombre que le puso el usuario puede llevar caracteres que no valen en un fichero.</summary>
    private static string CleanFileName(string name)
    {
        string clean = string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim();

        return clean.Length == 0 ? "sin nombre" : clean;
    }

    /// <summary>Lo que se propone al guardar un documento, que siempre va en json.</summary>
    private static string SuggestedFileName(string name) => $"{CleanFileName(name)}.json";

    /// <summary>
    /// Vuelve a enseñar el elemento del árbol. Es el doble clic.
    /// </summary>
    /// <remarks>
    /// Si su pestaña estaba cerrada se vuelve a abrir con el estado que tenía: cerrar
    /// una pestaña sólo la quita de la vista, el panel sigue vivo.
    /// </remarks>
    [RelayCommand]
    private void OpenTreeItem(ItemTree? item)
    {
        if (item is { IsPanelNode: true })
            AddVisiblePanel(item.Tag);
    }

    /// <summary>
    /// Elimina el elemento del proyecto: se va del árbol, se cierra su pestaña y se
    /// olvida el panel.
    /// </summary>
    /// <remarks>
    /// Es lo contrario de cerrar la pestaña, y por eso pregunta. Cerrar es reversible con
    /// un doble clic; esto no. El aviso sólo habla de perder trabajo cuando de verdad hay
    /// algo sin guardar: repetirlo siempre acaba en que nadie lo lee.
    /// </remarks>
    [RelayCommand]
    private async Task DeleteTreeItemAsync(ItemTree? item)
    {
        if (item is not { CanDelete: true })
            return;

        bool unsaved = GetPanelFromDic(item.Tag)?.HasUnsavedChanges() ?? false;

        bool confirmed = await Dialogs.ConfirmAsync(
            Text["DeleteItemTitle"],
            Text.Format(unsaved ? "DeleteItemBodyUnsaved" : "DeleteItemBody", item.DisplayText),
            Text["DeleteLabel"]);

        if (!confirmed)
            return;

        if (GetPanelFromDic(item.Tag) is { } panel)
        {
            CloseTab(panel);
            _panels.Remove(item.Tag);
        }

        // Lo que cuelga del elemento se va con él: los bloques de un juego de tiles no
        // son un elemento aparte y no tienen sentido sin sus tiles.
        foreach (ItemTree child in item.Childs)
        {
            if (GetPanelFromDic(child.Tag) is { } tool)
                CloseRightPanel(tool);

            _panels.Remove(child.Tag);
        }

        TreeGeneralVm.Remove(item);
    }

    /// <summary>
    /// Quita la pestaña de la vista sin tocar el proyecto. El panel se queda guardado y
    /// el nodo del árbol lo puede volver a traer con un doble clic.
    /// </summary>
    [RelayCommand]
    private void CloseTab(PanelBaseViewModel? panel)
    {
        if (panel is null)
            return;

        int position = Tabs.IndexOf(panel);
        if (position < 0)
            return;

        Tabs.Remove(panel);

        // Hay que decir qué queda seleccionado: al desaparecer la suya, el TabControl
        // escribe null y se quedaría sin ninguna con pestañas todavía abiertas.
        SelectedTab = Tabs.Count > 0 ? Tabs[Math.Min(position, Tabs.Count - 1)] : null;
    }

    public void AddPanelToDic(PanelBaseViewModel panel) => _panels.TryAdd(panel.TagId, panel);

    public PanelBaseViewModel? GetPanelFromDic(string panelId) => _panels.GetValueOrDefault(panelId);

    public void AddVisiblePanel(string panelId)
    {
        PanelBaseViewModel? panel = GetPanelFromDic(panelId);
        if (panel is null)
            return;

        // Una herramienta va al lateral, para verla a la vez que lo que se edita.
        if (panel.IsTool)
        {
            RightPanViewModel = panel;
            return;
        }

        if (!Tabs.Contains(panel))
            Tabs.Add(panel);

        SelectedTab = panel;
    }
}