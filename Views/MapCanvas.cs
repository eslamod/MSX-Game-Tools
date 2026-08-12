using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Views;

/// <summary>
/// El lienzo del mapa: se dibuja a sí mismo y sólo lo que se ve.
/// </summary>
/// <remarks>
/// <para>
/// No es un control por celda como la rejilla de un bloque. Un mapa de 128x128 son
/// 16.384 celdas por capa, y un control con su plantilla y su medida para cada una no se
/// sostiene. Aquí se pinta el rango visible en un solo <see cref="Render"/>, así que el
/// coste depende del hueco en pantalla y no de lo grande que sea el mapa.
/// </para>
/// <para>
/// El control no sabe qué se está editando: avisa de en qué celda ha pasado algo y quien
/// escuche decidirá. Lo único suyo es el desplazamiento, que es presentación pura.
/// </para>
/// </remarks>
public class MapCanvas : Control
{
    public static readonly StyledProperty<TileMap?> MapProperty =
        AvaloniaProperty.Register<MapCanvas, TileMap?>(nameof(Map));

    /// <summary>Las miniaturas del juego de tiles, indexadas por número de tile.</summary>
    public static readonly StyledProperty<IList<ImageMini>?> TilesProperty =
        AvaloniaProperty.Register<MapCanvas, IList<ImageMini>?>(nameof(Tiles));

    public static readonly StyledProperty<int> ZoomProperty =
        AvaloniaProperty.Register<MapCanvas, int>(nameof(Zoom), defaultValue: 2);

    /// <summary>Lo que se ve donde no hay tile en ninguna capa: el borde de la máquina.</summary>
    public static readonly StyledProperty<IBrush> BackgroundProperty =
        AvaloniaProperty.Register<MapCanvas, IBrush>(nameof(Background), defaultValue: Brushes.Black);

    public static readonly StyledProperty<MapRegion?> SelectionProperty =
        AvaloniaProperty.Register<MapCanvas, MapRegion?>(nameof(Selection));

    public static readonly StyledProperty<MapTool> ToolProperty =
        AvaloniaProperty.Register<MapCanvas, MapTool>(nameof(Tool), defaultValue: MapTool.Stamp);

    /// <summary>Lo que se va a estampar, para enseñarlo bajo el ratón antes de soltar.</summary>
    public static readonly StyledProperty<TilePatch?> BrushProperty =
        AvaloniaProperty.Register<MapCanvas, TilePatch?>(nameof(Brush));

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowGrid), defaultValue: true);

    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)));
    private static readonly IPen SelectionPen = new Pen(Brushes.Red, 2);
    private static readonly IPen EdgePen = new Pen(Brushes.DimGray);

    private Point _offset;

    /// <summary>La celda bajo el ratón, para pintar ahí lo que se va a estampar.</summary>
    private (int Column, int Row)? _hover;

    /// <summary>Dónde empezó el arrastre, para desplazar o para marcar.</summary>
    private Point _panFrom;
    private (int Column, int Row)? _dragFrom;
    private bool _panning;
    private bool _painting;

    static MapCanvas()
    {
        AffectsRender<MapCanvas>(
            MapProperty, ZoomProperty, BackgroundProperty, SelectionProperty, ShowGridProperty, BrushProperty);
    }

    public MapCanvas()
    {
        ClipToBounds = true;
        Focusable = true;

        // Los tiles son de 8x8 estirados: sin esto salen borrosos al ampliar.
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    /// <summary>Se ha pulsado sobre una celda.</summary>
    public event Action<int, int>? CellPressed;

    /// <summary>Se está arrastrando y se ha entrado en otra celda.</summary>
    public event Action<int, int>? CellDragged;

    /// <summary>Se ha soltado el botón.</summary>
    public event Action? DragEnded;

    /// <summary>El ratón está sobre esa celda, o fuera del mapa si es nulo.</summary>
    public event Action<(int Column, int Row)?>? HoverChanged;

    public TileMap? Map
    {
        get => GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    public IList<ImageMini>? Tiles
    {
        get => GetValue(TilesProperty);
        set => SetValue(TilesProperty, value);
    }

    public int Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public IBrush Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public MapRegion? Selection
    {
        get => GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    public MapTool Tool
    {
        get => GetValue(ToolProperty);
        set => SetValue(ToolProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public TilePatch? Brush
    {
        get => GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    /// <summary>Lo que mide un tile en pantalla con el zoom actual.</summary>
    public double TileSize => TileRow.Columns * Zoom;

    /// <summary>Deja el mapa pegado a la esquina, que es donde se empieza a mirar.</summary>
    public void ResetOffset()
    {
        _offset = default;
        InvalidateVisual();
    }

    /// <summary>
    /// Las celdas del mapa que caben ahora mismo en el hueco.
    /// </summary>
    /// <remarks>
    /// Es de donde sale que el coste de pintar dependa del hueco y no de lo grande que sea
    /// el mapa. Público para poder comprobarlo: es aritmética, y en aritmética los fallos
    /// no se ven leyendo el código.
    /// </remarks>
    public MapRegion VisibleRange()
    {
        if (Map is not { } map)
            return default;

        double size = TileSize;

        int firstColumn = Math.Max(0, (int)(_offset.X / size));
        int firstRow = Math.Max(0, (int)(_offset.Y / size));
        int lastColumn = Math.Min(map.Width - 1, (int)((_offset.X + Bounds.Width) / size));
        int lastRow = Math.Min(map.Height - 1, (int)((_offset.Y + Bounds.Height) / size));

        return new MapRegion(
            firstColumn,
            firstRow,
            Math.Max(0, lastColumn - firstColumn + 1),
            Math.Max(0, lastRow - firstRow + 1));
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Background, new Rect(Bounds.Size));

        if (Map is not { } map || Tiles is not { Count: > 0 } tiles)
            return;

        double size = TileSize;
        MapRegion visible = VisibleRange();

        for (int row = visible.Top; row < visible.Top + visible.Height; row++)
        {
            for (int column = visible.Left; column < visible.Left + visible.Width; column++)
            {
                var rect = new Rect(
                    (column * size) - _offset.X,
                    (row * size) - _offset.Y,
                    size,
                    size);

                if (map.TileAt(column, row, onlyVisible: true) is int tile && (uint)tile < (uint)tiles.Count)
                    context.DrawImage(tiles[tile].SpritePreview, rect);

                if (ShowGrid)
                    context.DrawRectangle(null, GridPen, rect);
            }
        }

        DrawEdge(context, map, size);
        DrawGhost(context, map, tiles, size);
        DrawSelection(context, size);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Ocupa el hueco que le den: el mapa se recorre desplazando, no creciendo.
        return new Size(
            double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
    }

    /// <summary>
    /// Lo que se va a estampar, dibujado bajo el ratón antes de pulsar.
    /// </summary>
    /// <remarks>
    /// Se ve translúcido para distinguirlo de lo que ya está puesto. Sin esto hay que
    /// acordarse de lo que se cogió abajo, y con un bloque además de por dónde cae.
    /// </remarks>
    private void DrawGhost(DrawingContext context, TileMap map, IList<ImageMini> tiles, double size)
    {
        if (_hover is not { } hover || Brush is not { } brush || Tool != MapTool.Stamp)
            return;

        using (context.PushOpacity(0.65))
        {
            for (int row = 0; row < brush.Height; row++)
            {
                for (int column = 0; column < brush.Width; column++)
                {
                    if (brush[column, row] is not int tile || (uint)tile >= (uint)tiles.Count)
                        continue;

                    int atColumn = hover.Column + column;
                    int atRow = hover.Row + row;

                    if (atColumn >= map.Width || atRow >= map.Height)
                        continue;

                    context.DrawImage(tiles[tile].SpritePreview, new Rect(
                        (atColumn * size) - _offset.X,
                        (atRow * size) - _offset.Y,
                        size,
                        size));
                }
            }
        }
    }

    /// <summary>El borde del mapa, para saber dónde se acaba cuando sobra hueco.</summary>
    private void DrawEdge(DrawingContext context, TileMap map, double size)
    {
        context.DrawRectangle(null, EdgePen, new Rect(
            -_offset.X, -_offset.Y, map.Width * size, map.Height * size));
    }

    private void DrawSelection(DrawingContext context, double size)
    {
        if (Selection is not { } region)
            return;

        context.DrawRectangle(null, SelectionPen, new Rect(
            (region.Left * size) - _offset.X,
            (region.Top * size) - _offset.Y,
            region.Width * size,
            region.Height * size));
    }

    /// <summary>La celda que hay bajo ese punto, aunque caiga fuera del mapa.</summary>
    private (int Column, int Row) CellAt(Point point) => (
        (int)Math.Floor((point.X + _offset.X) / TileSize),
        (int)Math.Floor((point.Y + _offset.Y) / TileSize));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;

        // La rueda pulsada desplaza siempre, sin tener que ir a la herramienta: es lo
        // comodo mientras se pinta, que es cuando mas falta hace moverse.
        if (properties.IsMiddleButtonPressed || Tool == MapTool.Pan)
        {
            _panning = true;
            _panFrom = e.GetPosition(this);
            e.Pointer.Capture(this);

            return;
        }

        (int column, int row) = CellAt(e.GetPosition(this));

        _painting = true;
        _dragFrom = (column, row);

        e.Pointer.Capture(this);

        CellPressed?.Invoke(column, row);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        Point position = e.GetPosition(this);

        if (_panning)
        {
            Pan(position);
            return;
        }

        (int column, int row) = CellAt(position);
        (int Column, int Row)? cell = Inside(column, row) ? (column, row) : null;

        if (cell != _hover)
        {
            _hover = cell;
            InvalidateVisual();
        }

        HoverChanged?.Invoke(cell);

        if (_painting)
            CellDragged?.Invoke(column, row);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_painting)
            DragEnded?.Invoke();

        _panning = false;
        _painting = false;
        _dragFrom = null;

        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        _hover = null;
        InvalidateVisual();

        HoverChanged?.Invoke(null);
    }

    /// <summary>Mueve el mapa dentro del hueco.</summary>
    private void Pan(Point position)
    {
        Point delta = position - _panFrom;
        _panFrom = position;

        _offset = new Point(_offset.X - delta.X, _offset.Y - delta.Y);

        ClampOffset();
        InvalidateVisual();
    }

    /// <summary>
    /// Devuelve el desplazamiento a donde hay mapa.
    /// </summary>
    /// <remarks>
    /// Se deja pasar un poco del borde a propósito, para poder trabajar cómodo en la
    /// última fila; lo que no se deja es perder el mapa de vista del todo.
    /// </remarks>
    private void ClampOffset()
    {
        if (Map is not { } map)
            return;

        double maxX = Math.Max(0, (map.Width * TileSize) - (Bounds.Width / 2));
        double maxY = Math.Max(0, (map.Height * TileSize) - (Bounds.Height / 2));

        _offset = new Point(
            Math.Clamp(_offset.X, 0, maxX),
            Math.Clamp(_offset.Y, 0, maxY));
    }

    /// <summary>
    /// El desplazamiento va en pixeles de pantalla, así que al cambiar el zoom hay que
    /// reescalarlo.
    /// </summary>
    /// <remarks>
    /// Sin esto, alejarse desde un mapa grande dejaba la vista fuera del mapa: el
    /// desplazamiento seguía valiendo miles de pixeles cuando el mapa entero ya medía
    /// unos cientos, y se veía el fondo pelado.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ZoomProperty)
        {
            int before = change.OldValue is int old && old > 0 ? old : Zoom;

            if (before != Zoom)
                _offset = new Point(_offset.X * Zoom / before, _offset.Y * Zoom / before);

            ClampOffset();
        }
        else if (change.Property == MapProperty)
        {
            // Otro mapa empieza por su esquina, que es donde se empieza a mirar.
            _offset = default;
        }
    }

    private bool Inside(int column, int row) =>
        Map is { } map && column >= 0 && row >= 0 && column < map.Width && row < map.Height;
}
