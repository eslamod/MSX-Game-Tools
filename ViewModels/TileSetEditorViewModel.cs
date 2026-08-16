using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Qué hace el ratón sobre la rejilla de los 256 tiles.
/// </summary>
/// <remarks>
/// Los mismos tres modos que el editor de mapas y con los mismos nombres: quien sabe usar
/// aquél no tiene que aprender nada aquí. «Estampar» en vez de «Pegar» porque dice lo que
/// «Pegar» calla, que se puede soltar varias veces seguidas.
/// </remarks>
public enum TileTool
{
    /// <summary>Elegir el tile que se edita en el lienzo, que es lo de siempre.</summary>
    Edit,

    /// <summary>Marcar un rectángulo de tiles para copiarlo.</summary>
    Select,

    /// <summary>Soltar en otro sitio lo que se marcó.</summary>
    Stamp,
}

/// <summary>
/// Edición de un juego de tiles: el patrón actual en el lienzo y los 256 en la rejilla.
/// </summary>
public partial class TileSetEditorViewModel : PanelBaseViewModel, IPaletteDocument
{
    private readonly TileSet _tileSet;

    /// <summary>Lo que había donde se estampó por última vez, y dónde.</summary>
    private TileSetPatch? _overwritten;

    private int _overwrittenLeft;

    private int _overwrittenTop;

    /// <summary>La paleta del juego, a cuyos cambios de color estamos suscritos.</summary>
    private ColorPalette _palette;

    [ObservableProperty]
    private Tile _currentTile;

    /// <summary>
    /// Miniatura seleccionada en la rejilla, enlazada en los dos sentidos.
    /// </summary>
    /// <remarks>
    /// Por identidad y no por índice, igual que en los sprites: un índice de vuelta
    /// coincidiría con el que el enlace ya cree tener y la selección no se movería.
    /// </remarks>
    [ObservableProperty]
    private ImageMini? _selectedThumbnail;

    /// <summary>
    /// Qué tile está en el lienzo, de 0 a 255.
    /// </summary>
    /// <remarks>
    /// Desde 0 porque es el número que gastan los mapas, los bloques y el código del juego:
    /// el tile 0 del mapa es este 0. Enseñar «1 / 256» obligaba a restar uno de cabeza cada
    /// vez que se miraba un número de tile fuera del editor.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextTileCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousTileCommand))]
    private int _currentTileIndex;

    /// <summary>
    /// Color del borde, que es lo que se ve donde un tile use el código 0.
    /// </summary>
    /// <remarks>
    /// En GRAPHIC 2 el código de color 0 es transparente y deja pasar el color del borde
    /// (R#7). Sin esto el editor pintaba el 0 como un color más de la paleta y enseñaba
    /// algo que la máquina no iba a mostrar.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BorderColor))]
    private int _borderColorIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditTool))]
    [NotifyPropertyChangedFor(nameof(IsSelectTool))]
    [NotifyPropertyChangedFor(nameof(IsStampTool))]
    private TileTool _tool = TileTool.Edit;

    /// <summary>El rectángulo marcado en la rejilla, en celdas.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private MapRegion? _selection;

    /// <param name="palette">
    /// Con qué colores se dibuja y se guarda. Es del juego: la trae su fichero, y si es
    /// nuevo, la que estuviera en la barra al crearlo.
    /// </param>
    public TileSetEditorViewModel(TileSet tileSet, ColorPalette palette, EditorPreferences? preferences = null)
    {
        Preferences = preferences ?? new EditorPreferences();
        _tileSet = tileSet;
        _palette = palette;
        _borderColorIndex = palette.DefaultBackgroundIndex;

        _currentTile = tileSet.ListOfTiles[0];

        foreach (Tile tile in tileSet.ListOfTiles)
        {
            if (tile.ImageMini is not null)
                Thumbnails.Add(tile.ImageMini);

            TileMarks.Add(new TileMarkViewModel());
        }

        _selectedThumbnail = _currentTile.ImageMini;

        for (int row = 0; row < Tile.Rows; row++)
        {
            RowColors.Add(new TileRowColorViewModel(
                row, _currentTile.ArrayTileRows[row], () => ColorPalette, OnRowColorPicked));
        }

        PixelSurface = new TilePixelSurface(this);

        _palette.ColorsChanged += OnPaletteColorsChanged;

        // Un juego que viene de fichero puede traerlos ya definidos.
        RefreshAttributes();

        RenderAll();
    }

    /// <summary>La vista se resuscribe para repintar el lienzo al cambiar de tile.</summary>
    public event Action? RefreshRequested;

    /// <summary>
    /// Ha cambiado la paleta del juego, o alguno de sus colores.
    /// </summary>
    /// <remarks>
    /// Lo escuchan los mapas que se dibujan con este juego: sus tiles acaban de cambiar de
    /// color y su fondo también, que es un índice de esta paleta.
    /// </remarks>
    public event Action? PaletteChanged;

    /// <summary>
    /// Los bloques del juego han cambiado: se ha creado, borrado o retocado alguno.
    /// </summary>
    /// <remarks>
    /// Lo escuchan los mapas que se dibujan con este juego, que enseñan los bloques en su
    /// selector de abajo. Los bloques se editan en su propio panel, que puede estar abierto
    /// a la vez que el mapa, así que sin avisar el selector se queda con los de antes.
    /// </remarks>
    public event Action? BlocksChanged;

    /// <summary>Lo llama el panel de bloques cuando toca alguno.</summary>
    public void NotifyBlocksChanged() => BlocksChanged?.Invoke();

    public override bool IsDocument => true;

    public override string DocumentName
    {
        get => _tileSet.Name;
        set => _tileSet.Name = value;
    }

    public override string HeaderTag => "TS";

    public override string KindKey => "TileSet";

    /// <summary>
    /// El juego con su paleta y sus bloques, que es lo que va al fichero.
    /// </summary>
    /// <remarks>
    /// La paleta que se escribe es la del juego, no la que esté mirando la ventana: con
    /// dos juegos abiertos con paletas distintas, guardar uno escribía los colores del
    /// otro dentro de su fichero.
    /// </remarks>
    public override string ToFileText() =>
        TileSetSerializer.Serialize(_tileSet, ColorPalette, BorderColorIndex);

    public TileSet TileSet => _tileSet;

    /// <summary>Zoom y demás ajustes que sobreviven al cambio de pestaña.</summary>
    public EditorPreferences Preferences { get; }

    /// <inheritdoc/>
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

            RefreshPalette();
        }
    }

    /// <summary>Los colores que puede tomar el borde. El 0 no, que es el transparente.</summary>
    public IReadOnlyList<PaletteColor> BorderChoices => ColorPalette.BackgroundChoices;

    public PaletteColor BorderColor => ColorPalette[BorderColorIndex];

    /// <summary>El tile actual visto por el lienzo de pintado.</summary>
    public IPixelSurface PixelSurface { get; }

    /// <summary>Las 256 miniaturas, que la vista reparte en una rejilla de 32 por 8.</summary>
    public ObservableCollection<ImageMini> Thumbnails { get; } = [];

    /// <summary>Una casilla por línea del tile, con sus dos colores.</summary>
    public ObservableCollection<TileRowColorViewModel> RowColors { get; } = [];

    /// <summary>Los atributos definidos, con lo que tenga puesto el tile de delante.</summary>
    public ObservableCollection<TileFlagViewModel> TileAttributes { get; } = [];

    /// <summary>Qué huecos de la rejilla van teñidos, uno por tile.</summary>
    public ObservableCollection<TileMarkViewModel> TileMarks { get; } = [];

    /// <summary>
    /// El atributo que se está mirando en la rejilla, o ninguno.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uno solo a la vez: con dos teñidos a la vez habría que distinguirlos por color y
    /// entonces hay que aprenderse los colores. Lo que se quiere saber es «cuáles tienen
    /// éste», y para eso basta con uno.
    /// </para>
    /// <para>
    /// Lo enciende el ojo del panel de propiedades. Se apaga solo en cuanto se toca la
    /// rejilla —elegir un tile, marcar un trozo, estampar— porque a partir de ahí el tinte
    /// estorba: falsea los colores de justo lo que se ha ido a mirar.
    /// </para>
    /// </remarks>
    public int? HighlightedAttribute
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;

            RefreshMarks();
        }
    }

    /// <summary>Pone el tinte donde toque, o lo quita de todas partes.</summary>
    private void RefreshMarks()
    {
        for (int index = 0; index < TileMarks.Count; index++)
        {
            TileMarks[index].IsMarked = HighlightedAttribute is int bit
                                        && _tileSet.ListOfTiles[index].Has(bit);
        }
    }

    /// <summary>
    /// Cualquier cosa que sea elegir tiles apaga el tinte.
    /// </summary>
    /// <remarks>
    /// Se pone donde se toca la rejilla y no en un solo sitio porque son tres caminos
    /// distintos —el clic que cambia de tile, marcar un rectángulo y estamparlo— y los tres
    /// significan que ya se ha dejado de mirar y se ha empezado a trabajar.
    /// </remarks>
    private void StopHighlighting() => HighlightedAttribute = null;

    /// <summary>
    /// Si este juego usa atributos. Mientras no, el bloque entero no se enseña.
    /// </summary>
    /// <remarks>
    /// La premisa de la funcionalidad: quien no los quiera no tiene que enterarse de que
    /// existen. Se definen en las propiedades del juego cuando hagan falta, y sólo entonces
    /// aparecen aquí.
    /// </remarks>
    public bool HasAttributes => TileAttributes.Count > 0;

    /// <summary>
    /// Rehace la lista con los atributos que tengan nombre ahora mismo.
    /// </summary>
    /// <remarks>
    /// Lo llama el panel de propiedades al aceptar: hasta entonces la lista puede estar
    /// vacía porque no había ninguno definido.
    /// </remarks>
    public void RefreshAttributes()
    {
        TileAttributes.Clear();

        foreach (int bit in _tileSet.AttributeNames.Defined)
        {
            TileAttributes.Add(new TileFlagViewModel(
                bit,
                _tileSet.AttributeNames[bit],
                CurrentTile.Has(bit),
                SetAttribute));
        }

        OnPropertyChanged(nameof(HasAttributes));
    }

    /// <summary>
    /// Marca o desmarca un atributo en el tile que se está editando.
    /// </summary>
    /// <remarks>
    /// La salida temprana no es un ahorro, es lo que distingue marcar de sólo enseñar: al
    /// cambiar de tile las casillas se mueven solas y esto salta igual, y sin ella recorrer
    /// los 256 tiles con las flechas dejaría el juego marcado como sin guardar sin haber
    /// tocado nada. Aquí y en un solo sitio: la misma guarda repetida en la casilla dejaría
    /// las dos sin vigilar, porque bastaría una para que la comprobación pasara.
    /// </remarks>
    private void SetAttribute(int bit, bool on)
    {
        if (CurrentTile.Has(bit) == on)
            return;

        CurrentTile.SetAttribute(bit, on);

        Touch();

        // Por si se está mirando justo ese: marcarlo tiene que verse en la rejilla al
        // momento, no la próxima vez que se encienda el ojo.
        RefreshMarks();
    }

    /// <summary>Pone las casillas como las tenga el tile de delante.</summary>
    private void ShowAttributesOfCurrentTile()
    {
        foreach (TileFlagViewModel flag in TileAttributes)
            flag.IsOn = CurrentTile.Has(flag.Bit);
    }

    public int TileCount => TileSet.TileCount;

    /// <summary>Lo que se lee al lado de las flechas: «12 / 255».</summary>
    public string TileLabel => $"{CurrentTileIndex} / {TileCount - 1}";

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextTile() => GoTo(CurrentTileIndex + 1);

    private bool CanGoNext() => CurrentTileIndex < TileCount - 1;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousTile() => GoTo(CurrentTileIndex - 1);

    private bool CanGoPrevious() => CurrentTileIndex > 0;

    /// <summary>Lleva el lienzo al tile de esa posición, 0-255.</summary>
    public void GoTo(int index)
    {
        if ((uint)index >= (uint)TileCount)
            return;

        StopHighlighting();

        CurrentTile = _tileSet.ListOfTiles[index];
        CurrentTileIndex = index;
        SelectedThumbnail = CurrentTile.ImageMini;

        for (int row = 0; row < RowColors.Count; row++)
            RowColors[row].Attach(CurrentTile.ArrayTileRows[row]);

        ShowAttributesOfCurrentTile();

        RefreshRequested?.Invoke();
    }

    /// <summary>Repinta la miniatura del tile actual entera.</summary>
    public void RenderCurrent()
    {
        if (CurrentTile.ImageMini is not null)
            TileRenderer.Render(CurrentTile, ColorPalette, BorderColor.Color, CurrentTile.ImageMini);
    }

    partial void OnSelectedThumbnailChanged(ImageMini? value)
    {
        if (value is null || ReferenceEquals(value, CurrentTile.ImageMini))
            return;

        int index = Thumbnails.IndexOf(value);
        if (index >= 0)
            GoTo(index);
    }

    partial void OnCurrentTileIndexChanged(int value) => OnPropertyChanged(nameof(TileLabel));

    /// <summary>El borde se ve en todos los tiles que usen el 0, no sólo en el actual.</summary>
    partial void OnBorderColorIndexChanged(int value)
    {
        Touch();

        RenderAll();
        RefreshRequested?.Invoke();
    }

    [RelayCommand]
    private void PickBorderColor(PaletteColor? color)
    {
        if (color is not null)
            BorderColorIndex = color.Index;
    }

    /// <inheritdoc/>
    public void RemapColors(IReadOnlyList<int> table)
    {
        _tileSet.RemapColors(table);
        BorderColorIndex = table[BorderColorIndex];

        // Aunque el borde no se haya movido: los 256 tiles sí, y hay que repintarlos.
        RefreshPalette();
    }

    // ------------------------------------------------------------------ copiar y estampar

    public bool IsEditTool => Tool == TileTool.Edit;

    public bool IsSelectTool => Tool == TileTool.Select;

    public bool IsStampTool => Tool == TileTool.Stamp;

    public bool HasSelection => Selection is not null;

    /// <summary>
    /// El trozo traído de otro juego, si se trae alguno.
    /// </summary>
    /// <remarks>
    /// Lo pone el espacio de trabajo al llegar a esta pestaña. El editor no sabe de dónde
    /// sale ni quién más hay abierto: sólo que tiene algo en la mano.
    /// </remarks>
    public CopiedTiles? InHand
    {
        get;
        set
        {
            field = value;

            OnPropertyChanged(nameof(HasStamp));
            OnPropertyChanged(nameof(StampWidth));
        }
    }

    /// <summary>Si hay algo que estampar, sea de aquí o de otro juego.</summary>
    public bool HasStamp => Selection is not null || InHand is not null;

    /// <summary>Cuántos tiles de ancho mide lo que se va a estampar.</summary>
    public int StampWidth => Selection?.Width ?? InHand?.Patch.Width ?? 0;

    /// <summary>
    /// Las miniaturas de lo que se va a estampar, por filas.
    /// </summary>
    /// <remarks>
    /// Lo de aquí sale de las miniaturas vivas del juego, para que retocar un tile marcado
    /// se vea en el fantasma; lo traído sale de la copia que se hizo al cambiar de pestaña,
    /// que es lo último que se vio del otro juego.
    /// </remarks>
    public IReadOnlyList<ImageMini> StampPreview
    {
        get
        {
            if (InHand is { } hand && Selection is null)
                return hand.Preview;

            if (Selection is not { } region)
                return [];

            var tiles = new List<ImageMini>(region.Width * region.Height);

            for (int row = 0; row < region.Height; row++)
            {
                for (int column = 0; column < region.Width; column++)
                {
                    int index = ((region.Top + row) * TileSet.Columns) + region.Left + column;

                    if ((uint)index < (uint)Thumbnails.Count)
                        tiles.Add(Thumbnails[index]);
                }
            }

            return tiles;
        }
    }

    /// <summary>
    /// Congela lo marcado para poder llevárselo, o nada si no hay nada marcado.
    /// </summary>
    /// <remarks>
    /// Lo llama el espacio de trabajo al dejar esta pestaña. Copiar aquí y no al marcar es
    /// lo que deja intacto el comportamiento de dentro de un juego: mientras el origen está
    /// delante manda lo que se ve, y se guarda justo cuando deja de verse.
    /// </remarks>
    public CopiedTiles? TakeSelection()
    {
        if (Selection is not { } region)
            return null;

        return new CopiedTiles(
            _tileSet.Copy(region.Left, region.Top, region.Width, region.Height),
            [.. StampPreview],
            _tileSet.Name);
    }

    /// <summary>Si hay un estampado que devolver.</summary>
    public bool CanUndoStamp => _overwritten is not null;

    /// <summary>Marca un rectángulo de la rejilla como origen de lo que se va a estampar.</summary>
    public void SelectRegion(int left, int top, int width, int height)
    {
        StopHighlighting();

        Selection = new MapRegion(left, top, width, height);
    }

    /// <summary>
    /// Suelta lo marcado con su esquina en esa celda.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los dibujos se copian ahora y no al marcar, así lo que cae es lo que se está viendo
    /// en la rejilla: la vista enseña el trozo bajo el ratón con las miniaturas de verdad,
    /// y si se copiara al marcar, retocar un tile de origen dejaría el fantasma diciendo
    /// una cosa y el estampado haciendo otra.
    /// </para>
    /// <para>
    /// Sobrescribe: aquí no hay huecos, los 256 tiles existen siempre. Y se guarda lo que
    /// había, que machacar el trabajo de una hora sin vuelta atrás no es aceptable en algo
    /// que se hace con un clic.
    /// </para>
    /// </remarks>
    public void StampAt(int column, int row)
    {
        // Lo de aquí manda sobre lo traído: si hay algo marcado en este juego, es lo que se
        // está mirando y lo que se espera que caiga.
        TileSetPatch? copied = Selection is { } source

            // La copia sale entera antes de escribir nada, asi que estampar encima de lo
            // marcado, o solapandolo, no se pisa a si mismo.
            ? _tileSet.Copy(source.Left, source.Top, source.Width, source.Height)
            : InHand?.Patch;

        if (copied is null)
            return;

        StopHighlighting();

        _overwrittenLeft = Math.Clamp(column, 0, TileSet.Columns - 1);
        _overwrittenTop = Math.Clamp(row, 0, TileSet.GridRows - 1);
        _overwritten = _tileSet.Stamp(_overwrittenLeft, _overwrittenTop, copied);

        AfterTilesChanged();
    }

    /// <summary>Devuelve los tiles que machacó el último estampado.</summary>
    [RelayCommand(CanExecute = nameof(CanUndoStamp))]
    private void UndoStamp()
    {
        if (_overwritten is null)
            return;

        _tileSet.Stamp(_overwrittenLeft, _overwrittenTop, _overwritten);
        _overwritten = null;

        AfterTilesChanged();
    }

    /// <summary>
    /// Repinta y avisa después de cambiar tiles de golpe.
    /// </summary>
    /// <remarks>
    /// Los 256, que son cuatro mil pixeles y no se nota: el trozo puede caer donde sea y
    /// llevar la cuenta de cuáles tocó sólo serviría para equivocarse.
    /// </remarks>
    private void AfterTilesChanged()
    {
        Touch();
        OnPropertyChanged(nameof(CanUndoStamp));
        UndoStampCommand.NotifyCanExecuteChanged();

        RenderAll();
        RefreshRequested?.Invoke();
    }

    private void OnRowColorPicked(int rowIndex)
    {
        Touch();

        if (CurrentTile.ImageMini is not null)
            TileRenderer.RenderRow(CurrentTile, rowIndex, ColorPalette, BorderColor.Color, CurrentTile.ImageMini);

        RefreshRequested?.Invoke();
    }

    private void OnPaletteColorsChanged(ColorPalette palette) => RefreshPalette();

    /// <summary>Cambiar de paleta o retocar un color repinta los 256.</summary>
    private void RefreshPalette()
    {
        // Y deja el juego sin guardar: la paleta va dentro de su fichero.
        Touch();

        OnPropertyChanged(nameof(ColorPalette));
        OnPropertyChanged(nameof(BorderChoices));
        OnPropertyChanged(nameof(BorderColor));

        foreach (TileRowColorViewModel row in RowColors)
            row.Refresh();

        RenderAll();
        RefreshRequested?.Invoke();
        PaletteChanged?.Invoke();
    }

    private void RenderAll()
    {
        foreach (Tile tile in _tileSet.ListOfTiles)
        {
            if (tile.ImageMini is not null)
                TileRenderer.Render(tile, ColorPalette, BorderColor.Color, tile.ImageMini);
        }
    }

    /// <summary>
    /// El tile que se está editando, visto por el lienzo.
    /// </summary>
    /// <remarks>
    /// Lee siempre el tile actual del editor, así que navegar por el juego no obliga a
    /// rehacer nada. Aquí no hay color transparente: un pixel apagado se pinta con el
    /// color de fondo de su línea, que es un color de la paleta como cualquier otro.
    /// </remarks>
    private sealed class TilePixelSurface(TileSetEditorViewModel editor) : IPixelSurface
    {
        public int Size => Tile.Rows;

        public bool IsSet(int x, int y) => editor.CurrentTile.ArrayTileRows[y].ArrayPattern[x];

        public void Set(int x, int y, bool on)
        {
            TileRow row = editor.CurrentTile.ArrayTileRows[y];

            row.ArrayPattern[x] = on;

            editor.Touch();

            editor.CurrentTile.ImageMini?.SetPixel(
                x, y, editor.ColorPalette.Resolve(on ? row.ForeColor : row.BackColor, editor.BorderColor.Color));
        }

        public IBrush BrushAt(int x, int y) =>
            TileRenderer.BrushAt(editor.CurrentTile, editor.ColorPalette, editor.BorderColor.Brush, x, y);

        // Un tile no lo compone nadie: nada que recalcular al soltar.
        public void EndStroke()
        {
        }
    }
}
