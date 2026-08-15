using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>Qué hace el ratón sobre el mapa.</summary>
public enum MapTool
{
    /// <summary>Coloca lo que haya cogido del selector de abajo.</summary>
    Stamp,

    /// <summary>Marca un rectángulo del mapa, sobre el que actúan las demás acciones.</summary>
    Select,

    /// <summary>Arrastra el mapa sin tocarlo.</summary>
    Pan,
}

/// <summary>Un rectángulo del mapa, en celdas.</summary>
public readonly record struct MapRegion(int Left, int Top, int Width, int Height)
{
    /// <summary>El rectángulo que va de una celda a otra, en cualquier orden.</summary>
    public static MapRegion Between(int fromColumn, int fromRow, int toColumn, int toRow) => new(
        Math.Min(fromColumn, toColumn),
        Math.Min(fromRow, toRow),
        Math.Abs(toColumn - fromColumn) + 1,
        Math.Abs(toRow - fromRow) + 1);

    public bool Contains(int column, int row) =>
        column >= Left && row >= Top && column < Left + Width && row < Top + Height;
}

/// <summary>
/// Edición de un mapa: el lienzo, las capas y lo que se estampa.
/// </summary>
/// <remarks>
/// <para>
/// Hay dos modos y no cinco. Seleccionar, copiar y rellenar hacían lo mismo con el ratón
/// —arrastrar un rectángulo— y sólo se diferenciaban en lo que pasa al soltar, así que
/// obligaban a acordarse del modo antes de empezar. Aquí se marca el rectángulo una vez y
/// después se decide qué hacer con él.
/// </para>
/// <para>
/// Ninguna operación toca la rejilla directamente: todas pasan por el mapa, que es quien
/// anota el rastro para deshacer.
/// </para>
/// </remarks>
public partial class MapEditorViewModel : PanelBaseViewModel, IPaletteDocument
{
    /// <summary>
    /// Los pasos del zoom, en pixeles de pantalla por pixel de tile.
    /// </summary>
    /// <remarks>
    /// Por debajo de uno van potencias de dos: dividen exacto el tile de ocho y no
    /// descuadran la rejilla. Hacen falta para que un mapa grande quepa entero, que con
    /// el mínimo en x1 un mapa de 96x96 ocupa 768 pixeles y no entra en la pantalla.
    /// </remarks>
    public static readonly double[] ZoomSteps = [0.25, 0.5, 1, 2, 3, 4, 5, 6, 7, 8];

    public const double MinZoom = 0.25;

    public const double MaxZoom = 8;

    /// <summary>Tiles por fila del selector, los mismos que el editor y el png.</summary>
    public const int TilesPerRow = 32;

    private readonly TileSetEditorViewModel _tiles;

    /// <summary>El bloque que se cogió, para poder pasar al siguiente con la rueda.</summary>
    private int _blockIndex = -1;

    [ObservableProperty]
    private MapTool _tool = MapTool.Stamp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    private double _zoom = 2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(FillSelectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(EraseSelectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopySelectionCommand))]
    private MapRegion? _selection;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteLayerCommand))]
    private MapLayerViewModel? _activeLayer;

    /// <summary>
    /// Lo que se estampa. Nunca es nulo: al abrir ya está cogido el primer tile, para que
    /// pulsar haga algo desde el principio.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BrushLabel))]
    private TilePatch _brush = TilePatch.Single(0);

    /// <summary>Cómo se llama lo que hay cogido, para la barra: un tile o un bloque.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BrushLabel))]
    private string _brushName = "Tile 0";

    /// <summary>La celda por la que pasa el ratón, o nulo si está fuera.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HoverLabel))]
    private (int Column, int Row)? _hover;

    public MapEditorViewModel(TileMap map, TileSetEditorViewModel tiles, EditorPreferences? preferences = null)
    {
        Map = map;
        _tiles = tiles;
        Preferences = preferences ?? new EditorPreferences();

        foreach (MapLayer layer in map.Layers)
            Watch(new MapLayerViewModel(layer));

        ActiveLayer = Layers.LastOrDefault();

        for (int index = 0; index < Tiles.Count; index++)
            TileChoices.Add(new TileChoiceViewModel(index, Tiles[index]));

        RefreshBlocks();

        map.Undo.Changed += OnUndoChanged;
        _tiles.PaletteChanged += OnTilesPaletteChanged;
        _tiles.BlocksChanged += RefreshBlocks;
    }

    /// <summary>Hay que repintar el lienzo.</summary>
    public event Action? RefreshRequested;

    public override bool IsDocument => true;

    public override string DocumentName
    {
        get => Map.Name;
        set => Map.Name = value;
    }

    public override string HeaderTag => "MP";

    public override string KindKey => "Map";

    /// <inheritdoc cref="TileSetEditorViewModel.ToFileText"/>
    public override string ToFileText() => MapSerializer.Serialize(Map);

    public TileMap Map { get; }

    public EditorPreferences Preferences { get; }

    /// <summary>Las miniaturas del juego, para pintar el mapa.</summary>
    public ObservableCollection<ImageMini> Tiles => _tiles.Thumbnails;

    /// <summary>
    /// Los tiles del selector de abajo, cada uno sabiendo si está cogido.
    /// </summary>
    /// <remarks>
    /// Hace falta el envoltorio para poder marcarlos: sin señal de lo que se ha cogido,
    /// hay que acordarse, y con un rectángulo de varios no hay quien se acuerde.
    /// </remarks>
    public ObservableCollection<TileChoiceViewModel> TileChoices { get; } = [];

    /// <summary>Los bloques del juego, que son el otro origen de lo que se estampa.</summary>
    public IList<TileBlock> Blocks => _tiles.TileSet.Blocks;

    /// <summary>
    /// Si este mapa se dibuja con supertiles, que lo decide su juego de tiles.
    /// </summary>
    /// <remarks>
    /// Entonces una celda del mapa es un supertile entero y su número es el del supertile,
    /// no el de un tile. Coger tiles sueltos deja de tener sentido: no hay dónde ponerlos.
    /// </remarks>
    public bool UsesSuperTiles => _tiles.TileSet.HasSuperTiles;

    /// <summary>Tiles que ocupa una celda del mapa, de ancho y de alto.</summary>
    public int CellTilesWidth => UsesSuperTiles ? _tiles.TileSet.SuperTileWidth : 1;

    /// <inheritdoc cref="CellTilesWidth"/>
    public int CellTilesHeight => UsesSuperTiles ? _tiles.TileSet.SuperTileHeight : 1;

    /// <summary>
    /// Las imágenes con las que se pinta el mapa, indexadas por el número de la celda.
    /// </summary>
    /// <remarks>
    /// Los tiles del juego en un mapa normal y los supertiles ya compuestos en uno de
    /// supertiles. Así el lienzo dibuja una imagen por celda en los dos casos y no tiene
    /// que saber de qué van.
    /// </remarks>
    public ObservableCollection<ImageMini> CellImages => UsesSuperTiles ? SuperTiles : Tiles;

    /// <summary>Cada supertile compuesto en una sola imagen.</summary>
    public ObservableCollection<ImageMini> SuperTiles { get; } = [];

    /// <summary>
    /// Rehace las imágenes de los supertiles.
    /// </summary>
    /// <remarks>
    /// Al tocar un bloque, un tile o la paleta: el dibujo de un supertile sale de todo eso.
    /// Se reutiliza la imagen anterior de cada uno cuando mide lo mismo, que es lo normal,
    /// para no soltar el bitmap que la vista tiene cogido.
    /// </remarks>
    private void RefreshSuperTiles()
    {
        if (!UsesSuperTiles)
        {
            SuperTiles.Clear();

            return;
        }

        for (int index = 0; index < Blocks.Count; index++)
        {
            ImageMini image = SuperTileRenderer.Render(
                Blocks[index],
                _tiles.TileSet,
                ColorPalette,
                ColorPalette.Resolve(BackgroundColorIndexOrBorder, ColorPalette[1].Color),
                index < SuperTiles.Count ? SuperTiles[index] : null);

            if (index < SuperTiles.Count)
                SuperTiles[index] = image;
            else
                SuperTiles.Add(image);
        }

        while (SuperTiles.Count > Blocks.Count)
            SuperTiles.RemoveAt(SuperTiles.Count - 1);
    }

    /// <summary>
    /// Con qué color se ve el 0 dentro de un supertile.
    /// </summary>
    /// <remarks>
    /// El del fondo del mapa, que es lo que se verá en la máquina: el 0 es transparente y
    /// deja pasar el borde, y en el mapa ese borde es su color de fondo.
    /// </remarks>
    private int BackgroundColorIndexOrBorder => Map.BackgroundColorIndex;

    /// <summary>
    /// Los bloques con su dibujo, para enseñarlos todos a la vez.
    /// </summary>
    /// <remarks>
    /// Todos visibles y no una lista de nombres con vista previa: un bloque se reconoce
    /// por su dibujo, no por llamarse «Bloque 7», y con una lista habría que ir uno a uno
    /// hasta dar con el que se busca.
    /// </remarks>
    public ObservableCollection<BlockChoiceViewModel> BlockChoices { get; } = [];

    /// <summary>Vuelve a leer los bloques del juego, que se editan en otro panel.</summary>
    public void RefreshBlocks()
    {
        BlockChoices.Clear();

        foreach (TileBlock block in Blocks)
            BlockChoices.Add(new BlockChoiceViewModel(block, _tiles.Thumbnails));

        // Un bloque es un supertile aqui, asi que su dibujo tambien cambia.
        RefreshSuperTiles();

        // Y el juego puede haber pasado a ser de supertiles mientras el mapa estaba
        // abierto: entonces cambia de que van sus celdas y cuanto miden.
        OnPropertyChanged(nameof(UsesSuperTiles));
        OnPropertyChanged(nameof(CellTilesWidth));
        OnPropertyChanged(nameof(CellTilesHeight));
        OnPropertyChanged(nameof(CellImages));

        RefreshRequested?.Invoke();
    }

    /// <summary>
    /// En el mismo orden que el mapa: la primera es la de abajo y la última la que tapa.
    /// La vista las enseña del revés, que es como se leen las capas.
    /// </summary>
    public ObservableCollection<MapLayerViewModel> Layers { get; } = [];

    public bool HasSelection => Selection is not null;

    /// <summary>
    /// La paleta con la que se ve el mapa, que es la de su juego de tiles.
    /// </summary>
    /// <remarks>
    /// Un mapa son números de tile: no tiene colores propios ni los guarda. Se expone para
    /// que la barra de paletas siga sirviendo con un mapa delante, pero lo que se cambia es
    /// la paleta del juego, y es a él a quien le queda algo sin guardar.
    /// </remarks>
    public ColorPalette ColorPalette
    {
        get => _tiles.ColorPalette;
        set => _tiles.ColorPalette = value;
    }

    /// <summary>Los colores que puede tomar el fondo. El 0 no, que es el transparente.</summary>
    public IReadOnlyList<PaletteColor> BackgroundChoices => ColorPalette.BackgroundChoices;

    public PaletteColor BackgroundColor => ColorPalette[Map.BackgroundColorIndex];

    /// <summary>
    /// Con qué tile salen las celdas vacías al exportar a binario.
    /// </summary>
    /// <remarks>
    /// Va por el ViewModel y no directo al mapa para poder enseñar cuál es: un número
    /// suelto no dice nada, y con la miniatura al lado se ve lo que va a salir.
    /// </remarks>
    public int EmptyTile
    {
        get => Map.EmptyTile;
        set
        {
            int tile = Math.Clamp(value, 0, Tiles.Count - 1);

            if (Map.EmptyTile == tile)
                return;

            Map.EmptyTile = tile;

            Touch();

            OnPropertyChanged();
            OnPropertyChanged(nameof(EmptyTileImage));
        }
    }

    /// <summary>La miniatura del tile de relleno, para verlo y no sólo leer su número.</summary>
    public ImageMini? EmptyTileImage =>
        (uint)Map.EmptyTile < (uint)Tiles.Count ? Tiles[Map.EmptyTile] : null;

    /// <summary>
    /// Con qué se pinta donde no hay tile en ninguna capa.
    /// </summary>
    /// <remarks>
    /// Es el mismo R#7 que el borde del editor de tiles: en la máquina, una celda sin nada
    /// enseña el color del borde.
    /// </remarks>
    public IBrush BackgroundBrush => ColorPalette.GetBrush(Map.BackgroundColorIndex);

    /// <summary>
    /// El juego ha cambiado de paleta o de colores: sus miniaturas ya están rehechas, pero
    /// el fondo del mapa es un índice de esa paleta y hay que volver a leerlo.
    /// </summary>
    private void OnTilesPaletteChanged()
    {
        OnPropertyChanged(nameof(ColorPalette));
        OnPropertyChanged(nameof(BackgroundChoices));
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BackgroundBrush));

        // El dibujo de un supertile sale de los tiles y de la paleta, asi que hay que
        // rehacerlo: sus miniaturas son nuestras, no las del juego.
        RefreshSuperTiles();

        RefreshRequested?.Invoke();
    }

    /// <summary>El zoom para leerlo: los pasos de menos de uno se escriben en quebrado.</summary>
    public string ZoomLabel => Zoom switch
    {
        0.25 => "x¼",
        0.5 => "x½",
        _ => $"x{Zoom:0}",
    };

    public string BrushLabel => BrushName;

    public string HoverLabel => Hover is { } cell ? $"{cell.Column}, {cell.Row}" : string.Empty;

    /// <summary>Índice de la capa activa dentro del mapa, o -1 si no hay ninguna.</summary>
    public int ActiveLayerIndex => ActiveLayer is null ? -1 : Map.Layers.IndexOf(ActiveLayer.Layer);

    /// <summary>Si se puede editar: hace falta capa activa y que no esté bloqueada.</summary>
    public bool CanEdit => ActiveLayer is { IsLocked: false };

    // ------------------------------------------------------------------ herramientas

    [RelayCommand]
    private void UseStamp() => Tool = MapTool.Stamp;

    [RelayCommand]
    private void UseSelect() => Tool = MapTool.Select;

    [RelayCommand]
    private void UsePan() => Tool = MapTool.Pan;

    [RelayCommand]
    private void ZoomIn() => Zoom = ZoomSteps.FirstOrDefault(step => step > Zoom, MaxZoom);

    [RelayCommand]
    private void ZoomOut() => Zoom = ZoomSteps.LastOrDefault(step => step < Zoom, MinZoom);

    /// <summary>El zoom más grande con el que el mapa entero cabe en ese hueco.</summary>
    public void FitZoom(double availableWidth, double availableHeight)
    {
        if (Map.Width <= 0 || Map.Height <= 0 || availableWidth <= 0 || availableHeight <= 0)
            return;

        double byWidth = availableWidth / (Map.Width * TileRow.Columns);
        double byHeight = availableHeight / (Map.Height * Tile.Rows);
        double fits = Math.Min(byWidth, byHeight);

        // El paso más grande con el que todavía cabe. Si no cabe ni con el más pequeño,
        // ése: es lo más lejos que se puede mirar.
        Zoom = ZoomSteps.LastOrDefault(step => step <= fits, MinZoom);
    }

    // ------------------------------------------------------------------ pintar

    /// <summary>
    /// Coloca lo que haya cogido con su esquina en esa celda.
    /// </summary>
    /// <remarks>
    /// Se llama una vez por celda mientras se arrastra, así que estampar un bloque de 3x3
    /// arrastrando deja una fila de bloques solapados. Es lo que hace Tiled y es lo que se
    /// quiere para pintar hierba con un patrón.
    /// </remarks>
    public void Paint(int column, int row)
    {
        if (ActiveLayerIndex < 0)
            return;

        if (Map.Stamp(ActiveLayerIndex, column, row, Brush))
            RefreshRequested?.Invoke();
    }

    /// <summary>
    /// El mapa ha cambiado de tamaño: se repinta y se olvida la selección.
    /// </summary>
    /// <remarks>
    /// La selección se tira porque señalaba celdas que puede que ya no existan, y dejarla
    /// apuntando a cualquier sitio es peor que no tenerla.
    /// </remarks>
    public void AfterResize()
    {
        Selection = null;

        OnPropertyChanged(nameof(Map));
        RefreshRequested?.Invoke();
    }

    /// <summary>Han cambiado tiles por debajo: hay que repintar.</summary>
    public void AfterReplace() => RefreshRequested?.Invoke();

    /// <summary>Marca el rectángulo que va de una celda a otra.</summary>
    public void Select(int fromColumn, int fromRow, int toColumn, int toRow)
    {
        Selection = MapRegion.Between(
            Math.Clamp(fromColumn, 0, Map.Width - 1),
            Math.Clamp(fromRow, 0, Map.Height - 1),
            Math.Clamp(toColumn, 0, Map.Width - 1),
            Math.Clamp(toRow, 0, Map.Height - 1));
    }

    [RelayCommand]
    private void ClearSelection() => Selection = null;

    /// <summary>
    /// Cambia el color que se ve donde no hay tile en ninguna capa.
    /// </summary>
    /// <remarks>
    /// Es el mismo R#7 del borde en el editor de tiles, y aquí también hay que poder
    /// tocarlo: enseñarlo sin dejar cambiarlo no sirve de nada.
    /// </remarks>
    [RelayCommand]
    private void PickBackgroundColor(PaletteColor? color)
    {
        if (color is null || Map.BackgroundColorIndex == color.Index)
            return;

        Map.BackgroundColorIndex = color.Index;

        Touch();

        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BackgroundBrush));

        RefreshRequested?.Invoke();
    }

    /// <summary>Llena la selección repitiendo lo que haya cogido.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void FillSelection()
    {
        if (Selection is not { } region || ActiveLayerIndex < 0)
            return;

        if (Map.Fill(ActiveLayerIndex, region.Left, region.Top, region.Width, region.Height, Brush))
            RefreshRequested?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void EraseSelection()
    {
        if (Selection is not { } region || ActiveLayerIndex < 0)
            return;

        if (Map.Erase(ActiveLayerIndex, region.Left, region.Top, region.Width, region.Height))
            RefreshRequested?.Invoke();
    }

    /// <summary>
    /// Coge lo que hay en la selección para poder estamparlo en otro sitio.
    /// </summary>
    /// <remarks>
    /// Copia de la capa activa y no de lo que se ve: pegar tiene que devolver lo mismo que
    /// se cogió, y lo que se ve puede venir de tres capas distintas.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopySelection()
    {
        if (Selection is not { } region || ActiveLayer is null)
            return;

        Brush = ActiveLayer.Layer.Grid.ToPatch(region.Left, region.Top, region.Width, region.Height);
        BrushName = $"Copia {region.Width}x{region.Height}";
    }

    /// <summary>Coge un tile del selector de abajo.</summary>
    public void PickTile(TilePatch patch, string name)
    {
        Brush = patch;
        BrushName = name;
        _blockIndex = -1;

        Unmark();
    }

    /// <summary>
    /// Coge el rectángulo de tiles que va de uno a otro.
    /// </summary>
    /// <remarks>
    /// El rectángulo se calcula sobre las 32 columnas del selector, que son las mismas del
    /// editor y las del png: es la única disposición en la que coger un rectángulo
    /// significa algo, porque un árbol dibujado en tres filas sólo es un rectángulo si las
    /// filas miden lo que medían al dibujarlo.
    /// </remarks>
    public void PickTiles(int from, int to)
    {
        if ((uint)from >= (uint)TileChoices.Count || (uint)to >= (uint)TileChoices.Count)
            return;

        int left = Math.Min(from % TilesPerRow, to % TilesPerRow);
        int right = Math.Max(from % TilesPerRow, to % TilesPerRow);
        int top = Math.Min(from / TilesPerRow, to / TilesPerRow);
        int bottom = Math.Max(from / TilesPerRow, to / TilesPerRow);

        var patch = new TilePatch(right - left + 1, bottom - top + 1);

        Unmark();

        for (int row = 0; row < patch.Height; row++)
        {
            for (int column = 0; column < patch.Width; column++)
            {
                int index = ((top + row) * TilesPerRow) + left + column;

                patch[column, row] = index;
                TileChoices[index].IsSelected = true;
            }
        }

        Brush = patch;
        BrushName = patch.Width * patch.Height == 1 ? $"Tile {from}" : $"Tiles {patch.Width}x{patch.Height}";
        _blockIndex = -1;
    }

    /// <summary>Quita las marcas de los dos selectores: sólo se coge de uno a la vez.</summary>
    private void Unmark()
    {
        foreach (TileChoiceViewModel tile in TileChoices)
            tile.IsSelected = false;

        foreach (BlockChoiceViewModel block in BlockChoices)
            block.IsSelected = false;
    }

    /// <summary>
    /// Coge un bloque, que se estampa entero.
    /// </summary>
    /// <remarks>
    /// En un mapa de supertiles lo que se estampa es <b>una celda con el número del
    /// supertile</b>, no sus tiles sueltos: ahí la celda del mapa es el supertile entero,
    /// y guardar sus tiles la desharía en pedazos que el mapa no sabe colocar.
    /// </remarks>
    public void PickBlock(TileBlock block)
    {
        _blockIndex = Blocks.IndexOf(block);

        Brush = UsesSuperTiles && _blockIndex >= 0
            ? TilePatch.Single(_blockIndex)
            : block.ToPatch();

        BrushName = $"{block.Name} ({block.Width}x{block.Height})";

        Unmark();

        foreach (BlockChoiceViewModel choice in BlockChoices)
            choice.IsSelected = ReferenceEquals(choice.Block, block);
    }

    /// <summary>Pasa al bloque siguiente o al anterior, para la rueda con shift.</summary>
    public void StepBlock(int direction)
    {
        if (Blocks.Count == 0)
            return;

        int next = Math.Clamp(_blockIndex < 0 ? 0 : _blockIndex + direction, 0, Blocks.Count - 1);

        PickBlock(Blocks[next]);
    }

    // ------------------------------------------------------------------ deshacer

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        Map.Undo.Undo(Map);
        RefreshRequested?.Invoke();
    }

    private bool CanUndo() => Map.Undo.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        Map.Undo.Redo(Map);
        RefreshRequested?.Invoke();
    }

    private bool CanRedo() => Map.Undo.CanRedo;

    /// <summary>
    /// Todo lo que toca la rejilla pasa por la pila de deshacer, así que aquí se entera el
    /// mapa entero de que hay algo sin guardar: estampar, rellenar, redimensionar,
    /// sustituir tiles, y también deshacer y rehacer.
    /// </summary>
    private void OnUndoChanged()
    {
        Touch();

        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // ------------------------------------------------------------------ capas

    [RelayCommand]
    private void AddLayer()
    {
        MapLayerViewModel panel = Watch(new MapLayerViewModel(Map.AddLayer()));

        ActiveLayer = panel;

        Touch();
        RefreshRequested?.Invoke();
    }

    /// <summary>Un mapa sin capas no se puede editar, así que la última no se borra.</summary>
    private bool CanDeleteLayer() => ActiveLayer is not null && Layers.Count > 1;

    [RelayCommand(CanExecute = nameof(CanDeleteLayer))]
    private void DeleteLayer()
    {
        if (ActiveLayer is not { } layer)
            return;

        int index = Layers.IndexOf(layer);

        Map.Layers.Remove(layer.Layer);

        // Antes de quitarla de la coleccion: si no, el ListBox escribe null de vuelta.
        ActiveLayer = Layers[index == 0 ? 1 : index - 1];

        Layers.RemoveAt(index);

        Touch();
        RefreshRequested?.Invoke();
    }

    /// <summary>
    /// Apagar o bloquear una capa cambia lo que se ve, así que el lienzo tiene que
    /// enterarse: la vista sólo mueve el interruptor.
    /// </summary>
    private MapLayerViewModel Watch(MapLayerViewModel layer)
    {
        layer.PropertyChanged += (_, e) =>
        {
            // El nombre, si se ve y si esta bloqueada van al fichero del mapa.
            if (e.PropertyName is nameof(MapLayerViewModel.Name)
                or nameof(MapLayerViewModel.IsVisible)
                or nameof(MapLayerViewModel.IsLocked))
            {
                Touch();
            }

            if (e.PropertyName == nameof(MapLayerViewModel.IsVisible))
                RefreshRequested?.Invoke();

            if (e.PropertyName == nameof(MapLayerViewModel.IsLocked))
                OnPropertyChanged(nameof(CanEdit));
        };

        Layers.Add(layer);

        return layer;
    }

    partial void OnActiveLayerChanged(MapLayerViewModel? value)
    {
        OnPropertyChanged(nameof(CanEdit));

        foreach (MapLayerViewModel layer in Layers)
            layer.IsActive = ReferenceEquals(layer, value);
    }

    partial void OnZoomChanged(double value) => RefreshRequested?.Invoke();
}

/// <summary>Una capa en el panel del mapa.</summary>
public partial class MapLayerViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    public MapLayerViewModel(MapLayer layer) => Layer = layer;

    public MapLayer Layer { get; }

    public string Name
    {
        get => Layer.Name;
        set
        {
            if (Layer.Name == value)
                return;

            Layer.Name = value;
            OnPropertyChanged();
        }
    }

    public bool IsVisible
    {
        get => Layer.IsVisible;
        set
        {
            if (Layer.IsVisible == value)
                return;

            Layer.IsVisible = value;
            OnPropertyChanged();
        }
    }

    public bool IsLocked
    {
        get => Layer.IsLocked;
        set
        {
            if (Layer.IsLocked == value)
                return;

            Layer.IsLocked = value;
            OnPropertyChanged();
        }
    }

    public override string ToString() => Name;
}

/// <summary>Un bloque en el selector de abajo, con su dibujo.</summary>
public partial class BlockChoiceViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public BlockChoiceViewModel(TileBlock block, IList<ImageMini> tiles)
    {
        Block = block;

        for (int row = 0; row < block.Height; row++)
        {
            for (int column = 0; column < block.Width; column++)
            {
                int? tile = block[column, row];

                Cells.Add(tile is int index && (uint)index < (uint)tiles.Count ? tiles[index] : null);
            }
        }
    }

    public TileBlock Block { get; }

    /// <summary>Las celdas por filas, que la vista reparte en <see cref="Columns"/>.</summary>
    public ObservableCollection<ImageMini?> Cells { get; } = [];

    public int Columns => Block.Width;

    public string Label => $"{Block.Name} ({Block.Width}x{Block.Height})";
}
