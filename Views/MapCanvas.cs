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

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<MapCanvas, double>(nameof(Zoom), defaultValue: 2);

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

    /// <inheritdoc cref="CellTilesWidth"/>
    public static readonly StyledProperty<int> CellTilesWidthProperty =
        AvaloniaProperty.Register<MapCanvas, int>(nameof(CellTilesWidth), defaultValue: 1);

    /// <inheritdoc cref="CellTilesWidth"/>
    public static readonly StyledProperty<int> CellTilesHeightProperty =
        AvaloniaProperty.Register<MapCanvas, int>(nameof(CellTilesHeight), defaultValue: 1);

    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)));
    private static readonly IPen SelectionPen = new Pen(Brushes.Red, 2);
    private static readonly IPen EdgePen = new Pen(Brushes.DimGray);

    private Point _offset;

    /// <summary>La celda bajo el ratón, para pintar ahí lo que se va a estampar.</summary>
    private (int Column, int Row)? _hover;

    /// <summary>
    /// Dónde empezó el arrastre para desplazar.
    /// </summary>
    /// <remarks>
    /// Sólo el del desplazamiento. Por dónde se empezó a marcar lo recuerda la vista, que
    /// es quien sabe si se está seleccionando o estampando; aquí sólo se dice por qué
    /// celda se va pasando.
    /// </remarks>
    private Point _panFrom;
    private bool _panning;
    private bool _painting;

    /// <summary>
    /// El fantasma y la selección, encima del mapa y con su propio repintado.
    /// </summary>
    /// <remarks>
    /// Medido: un repintado del mapa lleno son unos 35 ms —2982 celdas visibles a 11 us
    /// cada una, casi todo el <c>DrawImage</c> de la celda—, y eso se disparaba cada vez que
    /// el ratón cruzaba a otra celda, sólo para mover el fantasma de sitio. Con la capa
    /// aparte, pasear el ratón cuesta lo que cuesta el fantasma, que son unas pocas celdas.
    /// </remarks>
    private readonly Overlay _overlay;

    static MapCanvas()
    {
        // Selection y Brush ya no estan aqui: sólo cambian lo que se pinta encima, y
        // meterlas aqui obligaba al mapa entero a repintarse por mover un recuadro.
        AffectsRender<MapCanvas>(
            MapProperty,
            ZoomProperty,
            BackgroundProperty,
            ShowGridProperty,
            TilesProperty,
            CellTilesWidthProperty,
            CellTilesHeightProperty);
    }

    public MapCanvas()
    {
        ClipToBounds = true;
        Focusable = true;

        ApplyInterpolation();

        // Hijo visual y no lógico, y sin recibir el ratón: es un adorno que va encima, no
        // un control con el que se pueda hacer nada.
        _overlay = new Overlay(this) { IsHitTestVisible = false };

        VisualChildren.Add(_overlay);
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

    public double Zoom
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

    /// <summary>
    /// Tiles que ocupa una celda del mapa: uno en un mapa normal, el supertile si no.
    /// </summary>
    /// <remarks>
    /// El lienzo sigue dibujando una imagen por celda; lo único que cambia es lo que mide
    /// la celda. Las imágenes que le pasen ya serán las de los supertiles, así que aquí no
    /// hay que saber de qué van.
    /// </remarks>
    public int CellTilesWidth
    {
        get => GetValue(CellTilesWidthProperty);
        set => SetValue(CellTilesWidthProperty, value);
    }

    /// <inheritdoc cref="CellTilesWidth"/>
    public int CellTilesHeight
    {
        get => GetValue(CellTilesHeightProperty);
        set => SetValue(CellTilesHeightProperty, value);
    }

    /// <summary>Lo que mide un tile en pantalla con el zoom actual.</summary>
    public double TileSize => TileRow.Columns * Zoom;

    /// <summary>Lo que mide una celda del mapa de ancho, que puede ser varios tiles.</summary>
    public double CellWidth => TileSize * Math.Max(1, CellTilesWidth);

    /// <inheritdoc cref="CellWidth"/>
    public double CellHeight => TileSize * Math.Max(1, CellTilesHeight);

    /// <summary>
    /// Cómo se escalan los tiles al dibujarlos.
    /// </summary>
    /// <remarks>
    /// Ampliando, sin interpolar: los tiles son de 8x8 y se quieren ver los pixeles
    /// nítidos. Alejando por debajo de uno hay que tirar información —cada pixel del MSX
    /// ocupa menos de uno de pantalla— y sin suavizar sale un moteado ilegible.
    /// </remarks>
    private void ApplyInterpolation() => RenderOptions.SetBitmapInterpolationMode(
        this,
        Zoom < 1 ? BitmapInterpolationMode.MediumQuality : BitmapInterpolationMode.None);

    /// <summary>Deja el mapa pegado a la esquina, que es donde se empieza a mirar.</summary>
    public void ResetOffset()
    {
        _offset = default;

        InvalidateAll();
    }

    /// <summary>
    /// Repinta las dos capas.
    /// </summary>
    /// <remarks>
    /// Para cuando cambia dónde cae cada celda —desplazar, cambiar de zoom—: entonces el
    /// fantasma y la selección tienen que moverse con el mapa, no quedarse donde estaban.
    /// </remarks>
    private void InvalidateAll()
    {
        InvalidateVisual();

        _overlay.InvalidateVisual();
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

        double cellWidth = CellWidth;
        double cellHeight = CellHeight;

        int firstColumn = Math.Max(0, (int)(_offset.X / cellWidth));
        int firstRow = Math.Max(0, (int)(_offset.Y / cellHeight));
        int lastColumn = Math.Min(map.Width - 1, (int)((_offset.X + Bounds.Width) / cellWidth));
        int lastRow = Math.Min(map.Height - 1, (int)((_offset.Y + Bounds.Height) / cellHeight));

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

        double cellWidth = CellWidth;
        double cellHeight = CellHeight;
        MapRegion visible = VisibleRange();

        for (int row = visible.Top; row < visible.Top + visible.Height; row++)
        {
            for (int column = visible.Left; column < visible.Left + visible.Width; column++)
            {
                var rect = new Rect(
                    (column * cellWidth) - _offset.X,
                    (row * cellHeight) - _offset.Y,
                    cellWidth,
                    cellHeight);

                if (map.TileAt(column, row, onlyVisible: true) is int tile && (uint)tile < (uint)tiles.Count)
                    context.DrawImage(tiles[tile].SpritePreview, rect);

                if (ShowGrid)
                    context.DrawRectangle(null, GridPen, rect);
            }
        }

        DrawEdge(context, map, cellWidth, cellHeight);
    }

    /// <summary>Lo que va encima del mapa y cambia sin que el mapa cambie.</summary>
    private void RenderOverlay(DrawingContext context)
    {
        double cellWidth = CellWidth;
        double cellHeight = CellHeight;

        if (Map is { } map && Tiles is { Count: > 0 } tiles)
            DrawGhost(context, map, tiles, cellWidth, cellHeight);

        DrawSelection(context, cellWidth, cellHeight);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Ocupa el hueco que le den: el mapa se recorre desplazando, no creciendo.
        var size = new Size(
            double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);

        _overlay.Measure(size);

        return size;
    }

    /// <summary>La capa de encima ocupa lo mismo: dibuja en las mismas coordenadas.</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _overlay.Arrange(new Rect(finalSize));

        return base.ArrangeOverride(finalSize);
    }

    /// <summary>
    /// Lo que se va a estampar, dibujado bajo el ratón antes de pulsar.
    /// </summary>
    /// <remarks>
    /// Se ve translúcido para distinguirlo de lo que ya está puesto. Sin esto hay que
    /// acordarse de lo que se cogió abajo, y con un bloque además de por dónde cae.
    /// </remarks>
    private void DrawGhost(
        DrawingContext context, TileMap map, IList<ImageMini> tiles, double cellWidth, double cellHeight)
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
                        (atColumn * cellWidth) - _offset.X,
                        (atRow * cellHeight) - _offset.Y,
                        cellWidth,
                        cellHeight));
                }
            }
        }
    }

    /// <summary>El borde del mapa, para saber dónde se acaba cuando sobra hueco.</summary>
    private void DrawEdge(DrawingContext context, TileMap map, double cellWidth, double cellHeight)
    {
        context.DrawRectangle(null, EdgePen, new Rect(
            -_offset.X, -_offset.Y, map.Width * cellWidth, map.Height * cellHeight));
    }

    private void DrawSelection(DrawingContext context, double cellWidth, double cellHeight)
    {
        if (Selection is not { } region)
            return;

        context.DrawRectangle(null, SelectionPen, new Rect(
            (region.Left * cellWidth) - _offset.X,
            (region.Top * cellHeight) - _offset.Y,
            region.Width * cellWidth,
            region.Height * cellHeight));
    }

    /// <summary>La celda que hay bajo ese punto, aunque caiga fuera del mapa.</summary>
    private (int Column, int Row) CellAt(Point point) => (
        (int)Math.Floor((point.X + _offset.X) / CellWidth),
        (int)Math.Floor((point.Y + _offset.Y) / CellHeight));

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

            // Sólo la capa de encima. Esto es lo que se disparaba con cada cruce de celda
            // y arrastraba consigo el repintado del mapa entero.
            _overlay.InvalidateVisual();
        }

        HoverChanged?.Invoke(cell);

        if (_painting)
            CellDragged?.Invoke(column, row);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        EndDrag();

        e.Pointer.Capture(null);
    }

    /// <summary>
    /// Perder la captura cuenta como soltar.
    /// </summary>
    /// <remarks>
    /// Si el sistema se lleva el ratón a media —cambiar de ventana, un diálogo que salta—
    /// no llega el soltar, y quien esté contando ese arrastre se queda esperando para
    /// siempre. Desde que el trazo decide qué es un paso de deshacer, eso significaría que
    /// deshacer deja de anotar y nadie se entera hasta que lo necesita.
    /// </remarks>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        EndDrag();
    }

    private void EndDrag()
    {
        if (_painting)
            DragEnded?.Invoke();

        _panning = false;
        _painting = false;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        _hover = null;
        _overlay.InvalidateVisual();

        HoverChanged?.Invoke(null);
    }

    /// <summary>Mueve el mapa dentro del hueco.</summary>
    private void Pan(Point position)
    {
        Point delta = position - _panFrom;
        _panFrom = position;

        _offset = new Point(_offset.X - delta.X, _offset.Y - delta.Y);

        ClampOffset();
        InvalidateAll();
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

        double maxX = Math.Max(0, (map.Width * CellWidth) - (Bounds.Width / 2));
        double maxY = Math.Max(0, (map.Height * CellHeight) - (Bounds.Height / 2));

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
            double before = change.OldValue is double old && old > 0 ? old : Zoom;

            if (before != Zoom)
                _offset = new Point(_offset.X * Zoom / before, _offset.Y * Zoom / before);

            ClampOffset();
            ApplyInterpolation();
        }
        else if (change.Property == MapProperty)
        {
            // Otro mapa empieza por su esquina, que es donde se empieza a mirar.
            _offset = default;
        }

        // Selection y Brush sólo tocan la capa de encima; las de geometría la tocan también,
        // porque mueven las celdas de sitio y el fantasma se quedaría descolocado.
        if (change.Property == SelectionProperty
            || change.Property == BrushProperty
            || change.Property == ZoomProperty
            || change.Property == MapProperty
            || change.Property == CellTilesWidthProperty
            || change.Property == CellTilesHeightProperty)
        {
            _overlay.InvalidateVisual();
        }
    }

    private bool Inside(int column, int row) =>
        Map is { } map && column >= 0 && row >= 0 && column < map.Width && row < map.Height;

    /// <summary>
    /// La capa de encima. Sólo dibuja; todo lo que necesita saber lo tiene el lienzo.
    /// </summary>
    private sealed class Overlay(MapCanvas canvas) : Control
    {
        public override void Render(DrawingContext context) => canvas.RenderOverlay(context);
    }
}
