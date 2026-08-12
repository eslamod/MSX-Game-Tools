using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;

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
public partial class MapEditorViewModel : PanelBaseViewModel
{
    /// <summary>Pixeles de pantalla por pixel de tile con el zoom a 1.</summary>
    public const int MinZoom = 1;

    public const int MaxZoom = 8;

    private readonly TileSetEditorViewModel _tiles;

    /// <summary>El bloque que se cogió, para poder pasar al siguiente con la rueda.</summary>
    private int _blockIndex = -1;

    [ObservableProperty]
    private MapTool _tool = MapTool.Stamp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomLabel))]
    private int _zoom = 2;

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

        map.Undo.Changed += OnUndoChanged;
    }

    /// <summary>Hay que repintar el lienzo.</summary>
    public event Action? RefreshRequested;

    public TileMap Map { get; }

    public EditorPreferences Preferences { get; }

    /// <summary>Las miniaturas del juego, para pintar el mapa y para elegir.</summary>
    public ObservableCollection<ImageMini> Tiles => _tiles.Thumbnails;

    /// <summary>Los bloques del juego, que son el otro origen de lo que se estampa.</summary>
    public IList<TileBlock> Blocks => _tiles.TileSet.Blocks;

    /// <summary>
    /// En el mismo orden que el mapa: la primera es la de abajo y la última la que tapa.
    /// La vista las enseña del revés, que es como se leen las capas.
    /// </summary>
    public ObservableCollection<MapLayerViewModel> Layers { get; } = [];

    public bool HasSelection => Selection is not null;

    public string ZoomLabel => $"x{Zoom}";

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
    private void ZoomIn() => Zoom = Math.Min(MaxZoom, Zoom + 1);

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(MinZoom, Zoom - 1);

    /// <summary>El zoom más grande con el que el mapa entero cabe en ese hueco.</summary>
    public void FitZoom(double availableWidth, double availableHeight)
    {
        if (Map.Width <= 0 || Map.Height <= 0 || availableWidth <= 0 || availableHeight <= 0)
            return;

        double byWidth = availableWidth / (Map.Width * TileRow.Columns);
        double byHeight = availableHeight / (Map.Height * Tile.Rows);

        Zoom = Math.Clamp((int)Math.Floor(Math.Min(byWidth, byHeight)), MinZoom, MaxZoom);
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
    }

    /// <summary>Coge un bloque, que se estampa entero.</summary>
    public void PickBlock(TileBlock block)
    {
        Brush = block.ToPatch();
        BrushName = $"{block.Name} ({block.Width}x{block.Height})";
        _blockIndex = Blocks.IndexOf(block);
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

    private void OnUndoChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // ------------------------------------------------------------------ capas

    [RelayCommand]
    private void AddLayer()
    {
        MapLayerViewModel panel = Watch(new MapLayerViewModel(Map.AddLayer()));

        ActiveLayer = panel;

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

    partial void OnZoomChanged(int value) => RefreshRequested?.Invoke();
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
