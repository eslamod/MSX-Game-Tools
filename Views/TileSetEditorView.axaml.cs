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

        // El rectangulo marcado esta dibujado en pixeles sobre la rejilla, asi que al
        // cambiar el tamaño de las celdas hay que volver a colocarlo o se queda con la
        // geometria del zoom anterior. Despues del layout: se mide preguntandole a la
        // primera casilla, y hasta que no se recoloque sigue diciendo lo de antes.
        Dispatcher.UIThread.Post(RedrawSelection, DispatcherPriority.Loaded);
    }

    /// <summary>Vuelve a colocar el rectángulo marcado, si es que hay y se está viendo.</summary>
    private void RedrawSelection()
    {
        if (Editor is { Tool: not TileTool.Edit, Selection: not null })
            ShowSelection();
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

    /// <summary>
    /// Las propiedades del juego que se está dibujando.
    /// </summary>
    /// <remarks>
    /// Aquí además de en el menú del nodo del árbol: es donde se definen los atributos, y
    /// por el árbol no las encontraba nadie.
    /// </remarks>
    private void OnShowProperties(object? sender, RoutedEventArgs e)
    {
        if (this.FindAncestorOfType<MainWindow>()?.DataContext is MainWindowViewModel main
            && DataContext is PanelBaseViewModel document)
        {
            main.ShowPropertiesOf(document);
        }
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

    // ------------------------------------------------------------------ copiar y estampar

    /// <summary>Celda por la que se empezó a arrastrar, para coger el rectángulo.</summary>
    private (int Column, int Row)? _anchor;

    private TileSetEditorViewModel? Editor => DataContext as TileSetEditorViewModel;

    /// <remarks>
    /// Por IsCheckedChanged y no por Click, igual que el zoom de al lado: con Click, poner
    /// el modo desde código no haría nada, y el estado visual y el del ViewModel se irían
    /// cada uno por su lado.
    /// </remarks>
    private void OnToolChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Tag: string tag } && Enum.TryParse(tag, out TileTool tool))
            SetTool(tool);
    }

    private void SetTool(TileTool tool)
    {
        if (Editor is not { } editor)
            return;

        editor.Tool = tool;
        _anchor = null;

        StampGhost.IsVisible = false;
        // El recuadro rojo sí es de lo marcado aquí: señala de dónde se copia, y un trozo
        // traído de otro juego no tiene dónde señalarse en esta rejilla.
        SelectionMark.IsVisible = editor.HasSelection && tool != TileTool.Edit;

        if (SelectionMark.IsVisible)
            ShowSelection();
    }

    private void OnToolLayerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (Editor is not { } editor || !TryCellAt(e.GetPosition(ToolLayer), out (int Column, int Row) cell))
            return;

        if (editor.Tool == TileTool.Select)
        {
            _anchor = cell;
            editor.SelectRegion(cell.Column, cell.Row, 1, 1);
            ShowSelection();

            e.Pointer.Capture(ToolLayer);
        }
        else if (editor.Tool == TileTool.Stamp)
        {
            editor.StampAt(cell.Column, cell.Row);
        }
    }

    private void OnToolLayerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (Editor is not { } editor || !TryCellAt(e.GetPosition(ToolLayer), out (int Column, int Row) cell))
            return;

        if (editor.Tool == TileTool.Select && _anchor is { } anchor)
        {
            editor.SelectRegion(
                Math.Min(anchor.Column, cell.Column),
                Math.Min(anchor.Row, cell.Row),
                Math.Abs(cell.Column - anchor.Column) + 1,
                Math.Abs(cell.Row - anchor.Row) + 1);

            ShowSelection();
        }
        else if (editor.Tool == TileTool.Stamp)
        {
            ShowGhost(cell);
        }
    }

    private void OnToolLayerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        _anchor = null;
        e.Pointer.Capture(null);
    }

    /// <summary>Al salirse, el fantasma se va: si no, se queda pegado en el borde.</summary>
    private void OnToolLayerExited(object? sender, Avalonia.Input.PointerEventArgs e) =>
        StampGhost.IsVisible = false;

    /// <summary>Dibuja el rectángulo marcado sobre la rejilla.</summary>
    private void ShowSelection()
    {
        if (Editor?.Selection is not { } region || !TryGeometry(out Point origin, out double cell))
            return;

        Canvas.SetLeft(SelectionMark, origin.X + (region.Left * cell));
        Canvas.SetTop(SelectionMark, origin.Y + (region.Top * cell));

        SelectionMark.Width = region.Width * cell;
        SelectionMark.Height = region.Height * cell;
        SelectionMark.IsVisible = true;
    }

    /// <summary>
    /// Enseña bajo el ratón lo que se va a soltar.
    /// </summary>
    /// <remarks>
    /// Con las miniaturas de verdad, que son las que el editor ya mantiene pintadas. Se
    /// ven donde van a caer antes de tocar nada, que es lo que evita estampar en la celda
    /// de al lado y tener que deshacerlo.
    /// </remarks>
    private void ShowGhost((int Column, int Row) cell)
    {
        // Lo que se va a estampar y no lo que está marcado: dentro de un juego son lo mismo,
        // pero con un trozo traído de otro no hay nada marcado aquí y hay algo que enseñar.
        if (Editor is not { StampWidth: > 0 } editor
            || !TryGeometry(out Point origin, out double size))
        {
            return;
        }

        if (GhostGrid is { } grid)
            grid.Columns = editor.StampWidth;

        StampGhost.ItemsSource = editor.StampPreview;

        Canvas.SetLeft(StampGhost, origin.X + (cell.Column * size));
        Canvas.SetTop(StampGhost, origin.Y + (cell.Row * size));

        StampGhost.IsVisible = true;
    }

    /// <summary>
    /// En qué celda de la rejilla cae un punto de la capa.
    /// </summary>
    private bool TryCellAt(Point point, out (int Column, int Row) cell)
    {
        cell = default;

        if (!TryGeometry(out Point origin, out double size))
            return false;

        cell = (
            Math.Clamp((int)((point.X - origin.X) / size), 0, TileSet.Columns - 1),
            Math.Clamp((int)((point.Y - origin.Y) / size), 0, TileSet.GridRows - 1));

        return true;
    }

    /// <summary>
    /// Dónde empieza la rejilla dentro de la capa y cuánto mide una celda.
    /// </summary>
    /// <remarks>
    /// Preguntándoselo a la primera casilla ya colocada, en vez de sumar el tamaño de la
    /// miniatura con los bordes: el zoom, la rejilla y el borde del ListBox cambian esa
    /// cuenta, y una celda mal medida descuadra la selección más según se baja.
    /// </remarks>
    private bool TryGeometry(out Point origin, out double size)
    {
        origin = default;
        size = 0;

        if (TileGrid.ContainerFromIndex(0) is not Control first
            || first.TranslatePoint(new Point(0, 0), ToolLayer) is not { } corner)
        {
            return false;
        }

        origin = corner;
        size = first.Bounds.Width;

        return size > 0;
    }

    private void ApplyZoom(int index)
    {
        double size = ZoomSizes[Math.Clamp(index, 0, ZoomSizes.Length - 1)];

        EditorGrid.ColumnDefinitions[0].Width = new GridLength(size);
        CanvTile.CanvasSize = size;
        CellSize = size / Tile.Rows;
    }
}
