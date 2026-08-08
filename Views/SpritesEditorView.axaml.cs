using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;

namespace MSX_SpritesEditor.Views;

public partial class SpritesEditorView : UserControl
{
    private enum DrawState
    {
        Idle,
        Painting,
        Erasing,
    }

    /// <summary>Cómo responde el lienzo al ratón. Los valores coinciden con el Tag de los RadioButton.</summary>
    private enum PaintMode
    {
        /// <summary>Cada pulsación pinta un único pixel; arrastrar no hace nada.</summary>
        Click,

        /// <summary>Manteniendo pulsado se pintan todos los pixeles del recorrido.</summary>
        Drag,
    }

    private const int GridSize = 16;

    /// <summary>Pixeles de pantalla por pixel de sprite en la miniatura a X1.</summary>
    private const int ThumbnailBaseScale = 4;

    private static readonly double[] ZoomSizes = [256, 512, 600];

    /// <summary>
    /// Lado de las miniaturas del banco. Vive en la vista y no en el ViewModel porque
    /// es presentación pura; la plantilla del ListBox lo lee con $parent.
    /// </summary>
    public static readonly StyledProperty<double> ThumbnailSizeProperty =
        AvaloniaProperty.Register<SpritesEditorView, double>(
            nameof(ThumbnailSize),
            defaultValue: GridSize * ThumbnailBaseScale);

    /// <summary>
    /// Alto de una fila del lienzo. La tira de colores por línea lo lee para quedar
    /// alineada con las filas del sprite sea cual sea el zoom.
    /// </summary>
    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<SpritesEditorView, double>(
            nameof(CellSize),
            defaultValue: 256d / GridSize);

    private readonly Rectangle[] _cells = new Rectangle[GridSize * GridSize];

    private bool _cellsBuilt;
    private DrawState _state = DrawState.Idle;
    private PaintMode _paintMode = PaintMode.Drag;
    private SpritesEditorViewModel? _subscribed;

    // Última celda pintada del trazo actual, para poder interpolar. -1 = trazo no iniciado.
    private int _lastCellX = -1;
    private int _lastCellY = -1;

    public SpritesEditorView() => InitializeComponent();

    public double ThumbnailSize
    {
        get => GetValue(ThumbnailSizeProperty);
        set => SetValue(ThumbnailSizeProperty, value);
    }

    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is SpritesEditorViewModel vm)
        {
            _subscribed = vm;
            vm.RefreshRequested += OnRefreshRequested;
        }

        // En WPF el lienzo arrancaba con Width = NaN porque el handler del RadioButton
        // se disparaba antes de que el constructor rellenase los arrays de tamaños,
        // así que hasta que no pulsabas un zoom no se pintaba nada.
        ApplyZoom(0);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (_subscribed is not null)
        {
            _subscribed.RefreshRequested -= OnRefreshRequested;
            _subscribed = null;
        }

        base.OnUnloaded(e);
    }

    private void OnRefreshRequested(Sprite sprite) => Draw();

    private void OnZoomChanged(object? sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender is RadioButton { IsChecked: true, Tag: string tag } && int.TryParse(tag, out int index))
            ApplyZoom(index);
    }

    private void OnThumbnailZoomChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Tag: string tag } && int.TryParse(tag, out int factor))
            ThumbnailSize = GridSize * ThumbnailBaseScale * factor;
    }

    /// <summary>
    /// Avalonia no cierra el flyout al pulsar algo de su interior, así que el
    /// desplegable de la paleta se quedaría abierto tras elegir un color.
    /// </summary>
    private void OnPaletteColorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.FindLogicalAncestorOfType<Popup>() is not { } popup)
            return;

        // Cerrarlo aquí mismo no vale: Button.OnClick lanza el evento Click primero y
        // sólo después lee su Command. Al cerrar el popup se desmonta el presentador
        // del flyout y se sueltan sus enlaces, así que Command ya valdría null y el
        // color no llegaría a aplicarse. Se cierra cuando la pulsación haya terminado.
        Dispatcher.UIThread.Post(() => popup.IsOpen = false, DispatcherPriority.Background);
    }

    private void OnPaintModeChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Tag: string tag } && Enum.TryParse(tag, out PaintMode mode))
            _paintMode = mode;
    }

    private void ApplyZoom(int index)
    {
        double size = ZoomSizes[Math.Clamp(index, 0, ZoomSizes.Length - 1)];

        EditorGrid.ColumnDefinitions[0].Width = new GridLength(size);
        EditorGrid.RowDefinitions[1].Height = new GridLength(size);
        CanvSprite.Width = size;
        CanvSprite.Height = size;
        CellSize = size / GridSize;

        Draw();
    }

    private void BuildCells()
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            var rect = new Rectangle
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
            };

            _cells[i] = rect;
            CanvSprite.Children.Add(rect);
        }

        _cellsBuilt = true;
    }

    private void Draw()
    {
        if (DataContext is not SpritesEditorViewModel vm)
            return;

        double cellWidth = CanvSprite.Width / GridSize;
        double cellHeight = CanvSprite.Height / GridSize;
        if (double.IsNaN(cellWidth) || double.IsNaN(cellHeight))
            return;

        if (!_cellsBuilt)
            BuildCells();

        IBrush background = vm.BackgroundColor.Brush;

        for (int y = 0; y < GridSize; y++)
        {
            SpriteRow row = vm.CurrentSprite.ArraySpriteRows[y];
            IBrush on = SpriteRenderer.ResolveRowBrush(vm.ColorPalette, row.Color, background);

            for (int x = 0; x < GridSize; x++)
            {
                Rectangle rect = _cells[(y * GridSize) + x];

                rect.Width = cellWidth;
                rect.Height = cellHeight;
                rect.Fill = row.ArrayColumns[x] ? on : background;

                Canvas.SetLeft(rect, x * cellWidth);
                Canvas.SetTop(rect, y * cellHeight);
            }
        }
    }

    // ---------------------------------------------------------------- pintado

    /// <summary>Convierte una posición del lienzo en coordenadas de celda del sprite.</summary>
    private bool TryGetCell(Point position, out int x, out int y)
    {
        x = -1;
        y = -1;

        double cellWidth = CanvSprite.Width / GridSize;
        double cellHeight = CanvSprite.Height / GridSize;
        if (double.IsNaN(cellWidth) || double.IsNaN(cellHeight))
            return false;

        // Math.Floor y no una conversión directa: (int) trunca hacia cero, así que
        // una coordenada negativa caería dentro de la celda 0 en lugar de quedar fuera.
        x = (int)Math.Floor(position.X / cellWidth);
        y = (int)Math.Floor(position.Y / cellHeight);

        return (uint)x < (uint)GridSize && (uint)y < (uint)GridSize;
    }

    /// <summary>Pinta o borra una celda concreta con el color de su fila.</summary>
    private void PaintCell(int x, int y, bool paint)
    {
        if (DataContext is not SpritesEditorViewModel vm)
            return;

        SpriteRow row = vm.CurrentSprite.ArraySpriteRows[y];
        if (row.ArrayColumns[x] == paint)
            return;

        row.ArrayColumns[x] = paint;

        PaletteColor background = vm.BackgroundColor;

        _cells[(y * GridSize) + x].Fill = paint
            ? SpriteRenderer.ResolveRowBrush(vm.ColorPalette, row.Color, background.Brush)
            : background.Brush;

        vm.CurrentSprite.ImageMini?.SetPixel(x, y, paint
            ? SpriteRenderer.ResolveRowColor(vm.ColorPalette, row.Color, background.Color)
            : background.Color);
    }

    /// <summary>
    /// Bresenham entre dos celdas. Sin esto un movimiento rápido del ratón deja huecos:
    /// PointerMoved sólo llega unas pocas veces por recorrido, no una por pixel.
    /// </summary>
    private void PaintLine(int x0, int y0, int x1, int y1, bool paint)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            PaintCell(x0, y0, paint);

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void PaintAt(Point position)
    {
        if (!TryGetCell(position, out int x, out int y))
        {
            // Fuera del lienzo: no pintamos y cortamos el trazo, para no unir con una
            // recta el punto por el que se salió con aquel por el que se vuelve a entrar.
            _lastCellX = -1;
            _lastCellY = -1;
            return;
        }

        bool paint = _state == DrawState.Painting;

        if (_lastCellX >= 0)
            PaintLine(_lastCellX, _lastCellY, x, y, paint);
        else
            PaintCell(x, y, paint);

        _lastCellX = x;
        _lastCellY = y;
    }

    // ---------------------------------------------------------------- puntero

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(CanvSprite);

        if (point.Properties.IsLeftButtonPressed)
            _state = DrawState.Painting;
        else if (point.Properties.IsRightButtonPressed)
            _state = DrawState.Erasing;
        else
            return;

        // Capturar el puntero permite seguir pintando aunque el ratón salga del lienzo,
        // y garantiza que PointerReleased llegue aquí aunque se suelte fuera.
        e.Pointer.Capture(CanvSprite);

        _lastCellX = -1;
        _lastCellY = -1;
        PaintAt(point.Position);

        e.Handled = true;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_state == DrawState.Idle || _paintMode == PaintMode.Click)
            return;

        PaintAt(e.GetPosition(CanvSprite));
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _state = DrawState.Idle;
        _lastCellX = -1;
        _lastCellY = -1;
        e.Pointer.Capture(null);
    }

    // Ojo: aquí NO va un handler de PointerExited. Con el puntero capturado, Avalonia
    // lanza PointerExited sobre el Canvas nada más empezar el arrastre, y cancelaba el
    // trazo en el primer movimiento. PointerReleased ya cubre el final del trazo.
}
