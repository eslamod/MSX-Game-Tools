using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Views;

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

    /// <summary>
    /// Cierre de la rejilla por la derecha y por abajo, en el contenedor.
    /// </summary>
    /// <remarks>
    /// Como cada tile solo lleva linea arriba y a la izquierda, la ultima fila se queda
    /// sin borde inferior y la ultima columna sin el derecho. Ponerlos aqui cuesta dos
    /// lineas en total; darselos a cada tile serian dos por cada uno de los 256, y
    /// ademas volverian a separarlos.
    /// </remarks>
    public static readonly StyledProperty<Thickness> GridEdgeThicknessProperty =
        AvaloniaProperty.Register<TileSetEditorView, Thickness>(
            nameof(GridEdgeThickness), defaultValue: new Thickness(0, 0, 1, 1));

    private TileSetEditorViewModel? _subscribed;

    /// <summary>
    /// Donde vive el zoom entre pestañas. El TabControl reconstruye la vista cada vez que
    /// se cambia, asi que la vista no puede recordarlo por su cuenta.
    /// </summary>
    private EditorPreferences? Preferences => (DataContext as TileSetEditorViewModel)?.Preferences;

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

    public Thickness GridEdgeThickness
    {
        get => GetValue(GridEdgeThicknessProperty);
        set => SetValue(GridEdgeThicknessProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ShowGridProperty)
            return;

        GridBrush = ShowGrid ? Brushes.Black : Brushes.Transparent;
        GridThickness = ShowGrid ? new Thickness(1, 1, 0, 0) : default;
        GridEdgeThickness = ShowGrid ? new Thickness(0, 0, 1, 1) : default;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is TileSetEditorViewModel vm)
        {
            _subscribed = vm;
            vm.RefreshRequested += OnRefreshRequested;
        }

        RestoreZoom();
    }

    /// <summary>Deja marcados los botones del zoom que se estaba usando y lo aplica.</summary>
    private void RestoreZoom()
    {
        int canvas = Preferences?.TileCanvasZoom ?? 0;

        Check("TileZoom", canvas);
        Check("TilePreviewZoom", Preferences?.TileThumbnailZoom ?? 1);

        // Por si el zoom guardado ya era el que marca el XAML: entonces no ha saltado
        // ningun IsCheckedChanged y hay que aplicarlo a mano.
        ApplyZoom(canvas);
    }

    private void Check(string group, int tag)
    {
        RadioButton? button = this.GetVisualDescendants()
            .OfType<RadioButton>()
            .FirstOrDefault(r => r.GroupName == group && (string?)r.Tag == tag.ToString());

        if (button is not null)
            button.IsChecked = true;
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
        {
            if (Preferences is { } preferences)
                preferences.TileCanvasZoom = index;

            ApplyZoom(index);
        }
    }

    private void OnThumbnailZoomChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true, Tag: string tag } || !int.TryParse(tag, out int factor))
            return;

        if (Preferences is { } preferences)
            preferences.TileThumbnailZoom = factor;

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
    /// <summary>
    /// El botón de bloques abre su panel en el lateral.
    /// </summary>
    /// <remarks>
    /// Lo pide la ventana principal y no este panel, igual que Redimensionar en el editor
    /// de mapas: los paneles del lateral los reparte ella, que es quien sabe cuáles hay.
    /// </remarks>
    private void OnShowBlocks(object? sender, RoutedEventArgs e)
    {
        if (this.FindAncestorOfType<MainWindow>()?.DataContext is MainWindowViewModel main)
            main.ShowBlocksCommand.Execute(null);
    }

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
