using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;

namespace MSX_SpritesEditor.Views;

public partial class TileSetEditorView : UserControl
{
    /// <summary>Pixeles de pantalla por pixel de tile en la rejilla a X1.</summary>
    private const int ThumbnailBaseScale = 2;

    private static readonly double[] ZoomSizes = [256, 512, 600];

    /// <summary>
    /// Lado de una miniatura. Un tile son 8 pixeles, así que a X1 se ve de 16: menos no
    /// se distingue, y 32 columnas de 16 caben de sobra a lo ancho.
    /// </summary>
    public static readonly StyledProperty<double> ThumbnailSizeProperty =
        AvaloniaProperty.Register<TileSetEditorView, double>(
            nameof(ThumbnailSize),
            defaultValue: TileRow.Columns * ThumbnailBaseScale);

    /// <summary>
    /// Alto de una fila del lienzo. La tira de colores lo lee para quedar alineada con
    /// las líneas del tile sea cual sea el zoom.
    /// </summary>
    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<TileSetEditorView, double>(
            nameof(CellSize),
            defaultValue: 256d / Tile.Rows);

    /// <summary>
    /// Marcar el borde de cada tile en la rejilla de miniaturas. No afecta al lienzo de
    /// edición: ahí la rejilla de pixeles se ve siempre, que es para lo que sirve.
    /// Vive aquí y no en el ViewModel porque es presentación pura, como el zoom.
    /// </summary>
    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<TileSetEditorView, bool>(nameof(ShowGrid), defaultValue: true);

    /// <summary>
    /// Con qué se pinta el borde de cada miniatura. Los bordes de los tiles pegados son
    /// la rejilla, como en GIMP: una línea de un pixel sea cual sea el zoom.
    /// </summary>
    public static readonly StyledProperty<IBrush> GridBrushProperty =
        AvaloniaProperty.Register<TileSetEditorView, IBrush>(nameof(GridBrush), defaultValue: Brushes.Black);

    /// <summary>
    /// Grosor de la línea de rejilla de una miniatura, sólo arriba y a la izquierda.
    /// </summary>
    /// <remarks>
    /// Sólo dos lados a propósito: si cada tile llevara borde por los cuatro, entre dos
    /// vecinos habría dos líneas y los tiles quedarían separados. Así la comparten y la
    /// rejilla sale continua y de un pixel, como la de GIMP. Y con la rejilla apagada el
    /// grosor pasa a cero, que si no quedaría una separación transparente igual de fea.
    /// </remarks>
    public static readonly StyledProperty<Thickness> GridThicknessProperty =
        AvaloniaProperty.Register<TileSetEditorView, Thickness>(
            nameof(GridThickness), defaultValue: new Thickness(1, 1, 0, 0));

    private TileSetEditorViewModel? _subscribed;

    public TileSetEditorView() => InitializeComponent();

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

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public IBrush GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public Thickness GridThickness
    {
        get => GetValue(GridThicknessProperty);
        set => SetValue(GridThicknessProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ShowGridProperty)
            return;

        GridBrush = ShowGrid ? Brushes.Black : Brushes.Transparent;
        GridThickness = ShowGrid ? new Thickness(1, 1, 0, 0) : default;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is TileSetEditorViewModel vm)
        {
            _subscribed = vm;
            vm.RefreshRequested += OnRefreshRequested;
        }

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

    private void OnRefreshRequested() => CanvTile.Redraw();

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
            ThumbnailSize = TileRow.Columns * ThumbnailBaseScale * factor;
    }

    private void OnPaintModeChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Tag: string tag }
            && Enum.TryParse(tag, out PixelCanvas.PaintingMode mode))
        {
            CanvTile.PaintMode = mode;
        }
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
        // sólo después lee su Command. Al cerrar el popup se sueltan sus enlaces y
        // Command ya valdría null. Se cierra cuando la pulsación haya terminado.
        Dispatcher.UIThread.Post(() => popup.IsOpen = false, DispatcherPriority.Background);
    }

    private void ApplyZoom(int index)
    {
        double size = ZoomSizes[Math.Clamp(index, 0, ZoomSizes.Length - 1)];

        EditorGrid.ColumnDefinitions[0].Width = new GridLength(size);
        CanvTile.CanvasSize = size;
        CellSize = size / Tile.Rows;
    }
}
