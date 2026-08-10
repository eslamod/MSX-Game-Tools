using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.Views;

/// <summary>
/// Rejilla de pixeles que se pinta con el ratón.
/// </summary>
/// <remarks>
/// Sale del editor de sprites para poder servir también a los tiles, que son de 8x8 en
/// vez de 16x16. Aquí vive todo lo que costó afinar: la captura del puntero, los dos
/// modos de pintado y la interpolación entre posiciones del ratón. Lo que se está
/// editando llega por <see cref="Surface"/>, y este control no sabe qué es.
/// </remarks>
public class PixelCanvas : UserControl
{
    /// <summary>Cómo responde el lienzo al ratón. Los valores coinciden con el Tag de los RadioButton.</summary>
    public enum PaintingMode
    {
        /// <summary>Cada pulsación pinta un único pixel; arrastrar no hace nada.</summary>
        Click,

        /// <summary>Manteniendo pulsado se pintan todos los pixeles del recorrido.</summary>
        Drag,
    }

    public static readonly StyledProperty<IPixelSurface?> SurfaceProperty =
        AvaloniaProperty.Register<PixelCanvas, IPixelSurface?>(nameof(Surface));

    /// <summary>Lado del lienzo en pixeles de pantalla.</summary>
    public static readonly StyledProperty<double> CanvasSizeProperty =
        AvaloniaProperty.Register<PixelCanvas, double>(nameof(CanvasSize), defaultValue: 256);

    public static readonly StyledProperty<PaintingMode> PaintModeProperty =
        AvaloniaProperty.Register<PixelCanvas, PaintingMode>(nameof(PaintMode), defaultValue: PaintingMode.Drag);

    /// <summary>Marcar el borde de cada pixel. Ayuda a contar, estorba al mirar.</summary>
    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<PixelCanvas, bool>(nameof(ShowGrid), defaultValue: true);

    private readonly Canvas _canvas = new()
    {
        Background = Brushes.Gray,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
    };

    private Rectangle[] _cells = [];
    private DrawState _state = DrawState.Idle;

    // Última celda pintada del trazo actual, para poder interpolar. -1 = trazo no iniciado.
    private int _lastCellX = -1;
    private int _lastCellY = -1;

    public PixelCanvas()
    {
        Content = _canvas;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

        _canvas.PointerPressed += OnCanvasPointerPressed;
        _canvas.PointerMoved += OnCanvasPointerMoved;
        _canvas.PointerReleased += OnCanvasPointerReleased;

        // Ojo: aquí NO va un handler de PointerExited. Con el puntero capturado, Avalonia
        // lanza PointerExited sobre el Canvas nada más empezar el arrastre, y cancelaba el
        // trazo en el primer movimiento. PointerReleased ya cubre el final del trazo.
    }

    private enum DrawState
    {
        Idle,
        Painting,
        Erasing,
    }

    public IPixelSurface? Surface
    {
        get => GetValue(SurfaceProperty);
        set => SetValue(SurfaceProperty, value);
    }

    public double CanvasSize
    {
        get => GetValue(CanvasSizeProperty);
        set => SetValue(CanvasSizeProperty, value);
    }

    public PaintingMode PaintMode
    {
        get => GetValue(PaintModeProperty);
        set => SetValue(PaintModeProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>Lado de una celda en pixeles de pantalla.</summary>
    public double CellSize => Surface is { Size: > 0 } surface ? CanvasSize / surface.Size : 0;

    /// <summary>Repinta el lienzo entero desde la superficie.</summary>
    public void Redraw()
    {
        if (Surface is not { Size: > 0 } surface)
            return;

        _canvas.Width = CanvasSize;
        _canvas.Height = CanvasSize;

        double cell = CanvasSize / surface.Size;
        if (double.IsNaN(cell) || cell <= 0)
            return;

        if (_cells.Length != surface.Size * surface.Size)
            BuildCells(surface.Size);

        IBrush grid = ShowGrid ? Brushes.Black : Brushes.Transparent;

        for (int y = 0; y < surface.Size; y++)
        {
            for (int x = 0; x < surface.Size; x++)
            {
                Rectangle rect = _cells[(y * surface.Size) + x];

                rect.Width = cell;
                rect.Height = cell;
                rect.Stroke = grid;
                rect.Fill = surface.BrushAt(x, y);

                Canvas.SetLeft(rect, x * cell);
                Canvas.SetTop(rect, y * cell);
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SurfaceProperty
            || change.Property == CanvasSizeProperty
            || change.Property == ShowGridProperty)
        {
            Redraw();
        }
    }

    private void BuildCells(int size)
    {
        _canvas.Children.Clear();
        _cells = new Rectangle[size * size];

        for (int i = 0; i < _cells.Length; i++)
        {
            var rect = new Rectangle { StrokeThickness = 1 };

            _cells[i] = rect;
            _canvas.Children.Add(rect);
        }
    }

    /// <summary>Convierte una posición del lienzo en coordenadas de celda.</summary>
    private bool TryGetCell(Point position, out int x, out int y)
    {
        x = -1;
        y = -1;

        if (Surface is not { Size: > 0 } surface)
            return false;

        double cell = CanvasSize / surface.Size;
        if (double.IsNaN(cell) || cell <= 0)
            return false;

        // Math.Floor y no una conversión directa: (int) trunca hacia cero, así que
        // una coordenada negativa caería dentro de la celda 0 en lugar de quedar fuera.
        x = (int)Math.Floor(position.X / cell);
        y = (int)Math.Floor(position.Y / cell);

        return (uint)x < (uint)surface.Size && (uint)y < (uint)surface.Size;
    }

    private void PaintCell(int x, int y, bool paint)
    {
        if (Surface is not { } surface || surface.IsSet(x, y) == paint)
            return;

        surface.Set(x, y, paint);

        _cells[(y * surface.Size) + x].Fill = surface.BrushAt(x, y);
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

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(_canvas);

        if (point.Properties.IsLeftButtonPressed)
            _state = DrawState.Painting;
        else if (point.Properties.IsRightButtonPressed)
            _state = DrawState.Erasing;
        else
            return;

        // Capturar el puntero permite seguir pintando aunque el ratón salga del lienzo,
        // y garantiza que PointerReleased llegue aquí aunque se suelte fuera.
        e.Pointer.Capture(_canvas);

        _lastCellX = -1;
        _lastCellY = -1;
        PaintAt(point.Position);

        e.Handled = true;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_state == DrawState.Idle || PaintMode == PaintingMode.Click)
            return;

        PaintAt(e.GetPosition(_canvas));
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _state = DrawState.Idle;
        _lastCellX = -1;
        _lastCellY = -1;
        e.Pointer.Capture(null);

        Surface?.EndStroke();
    }
}
