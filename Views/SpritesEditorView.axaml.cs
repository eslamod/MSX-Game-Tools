using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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

    private const int GridSize = 16;

    private static readonly double[] ZoomSizes = [256, 512, 600];

    private readonly Rectangle[] _cells = new Rectangle[GridSize * GridSize];

    private bool _cellsBuilt;
    private DrawState _state = DrawState.Idle;
    private SpritesEditorViewModel? _subscribed;

    public SpritesEditorView() => InitializeComponent();

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

    private void ApplyZoom(int index)
    {
        double size = ZoomSizes[Math.Clamp(index, 0, ZoomSizes.Length - 1)];

        EditorGrid.ColumnDefinitions[0].Width = new GridLength(size);
        EditorGrid.RowDefinitions[1].Height = new GridLength(size);
        CanvSprite.Width = size;
        CanvSprite.Height = size;

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

        for (int y = 0; y < GridSize; y++)
        {
            SpriteRow row = vm.CurrentSprite.ArraySpriteRows[y];

            for (int x = 0; x < GridSize; x++)
            {
                Rectangle rect = _cells[(y * GridSize) + x];

                rect.Width = cellWidth;
                rect.Height = cellHeight;
                rect.Fill = row.ArrayColumns[x] ? vm.ColorPalette.GetBrush(row.Color) : Brushes.Black;

                Canvas.SetLeft(rect, x * cellWidth);
                Canvas.SetTop(rect, y * cellHeight);
            }
        }
    }

    /// <summary>Pinta (o borra) el pixel bajo el puntero con el color de su fila.</summary>
    private void DrawPixel(Point position, bool paint)
    {
        if (DataContext is not SpritesEditorViewModel vm)
            return;

        double cellWidth = CanvSprite.Width / GridSize;
        double cellHeight = CanvSprite.Height / GridSize;

        int x = (int)(position.X / cellWidth);
        int y = (int)(position.Y / cellHeight);

        // La versión WPF no comprobaba límites: arrastrar fuera del lienzo
        // lanzaba IndexOutOfRangeException.
        if ((uint)x >= (uint)GridSize || (uint)y >= (uint)GridSize)
            return;

        SpriteRow row = vm.CurrentSprite.ArraySpriteRows[y];
        row.ArrayColumns[x] = paint;

        Color color = paint ? vm.ColorPalette.GetColor(row.Color) : Colors.Black;

        _cells[(y * GridSize) + x].Fill = paint ? vm.ColorPalette.GetBrush(row.Color) : Brushes.Black;
        vm.CurrentSprite.ImageMini?.SetPixel(x, y, color);
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(CanvSprite);

        if (point.Properties.IsLeftButtonPressed)
            _state = DrawState.Painting;
        else if (point.Properties.IsRightButtonPressed)
            _state = DrawState.Erasing;
        else
            return;

        // Capturar el puntero permite seguir pintando aunque el ratón salga del lienzo.
        e.Pointer.Capture(CanvSprite);
        DrawPixel(point.Position, _state == DrawState.Painting);
        e.Handled = true;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_state == DrawState.Idle)
            return;

        DrawPixel(e.GetPosition(CanvSprite), _state == DrawState.Painting);
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _state = DrawState.Idle;
        e.Pointer.Capture(null);
    }

    private void OnCanvasPointerExited(object? sender, PointerEventArgs e) => _state = DrawState.Idle;
}
