using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Views;

public partial class SpritesEditorView : UserControl
{
    /// <summary>
    /// Lado del patrón. Sigue aquí porque de él salen los tamaños de miniatura y la
    /// altura de fila de la tira de colores; el lienzo ya no lo necesita, lo saca de la
    /// superficie que le pasan.
    /// </summary>
    private const int GridSize = Sprite.Rows;

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
    /// Lado de la miniatura de un grupo. Se escala con el mismo número de pixeles de
    /// pantalla por pixel de sprite que las miniaturas de patrones, así que sale
    /// proporcionalmente mayor: el lienzo del grupo es de 46 y el del patrón de 16.
    /// </summary>
    public static readonly StyledProperty<double> GroupThumbnailSizeProperty =
        AvaloniaProperty.Register<SpritesEditorView, double>(
            nameof(GroupThumbnailSize),
            defaultValue: SpriteGroupRenderer.PreviewSize * ThumbnailBaseScale);

    /// <summary>
    /// Lo que mide en pantalla un pixel del lienzo del grupo. La imagen de referencia se
    /// escala con esto para quedar 1:1 con la composición sea cual sea el zoom. Es una
    /// propiedad de Avalonia y no un simple getter porque los enlaces tienen que
    /// enterarse al cambiar el zoom.
    /// </summary>
    public static readonly StyledProperty<double> GroupPixelSizeProperty =
        AvaloniaProperty.Register<SpritesEditorView, double>(
            nameof(GroupPixelSize),
            defaultValue: ThumbnailBaseScale);

    /// <summary>
    /// Alto de una fila del lienzo. La tira de colores por línea lo lee para quedar
    /// alineada con las filas del sprite sea cual sea el zoom.
    /// </summary>
    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<SpritesEditorView, double>(
            nameof(CellSize),
            defaultValue: 256d / GridSize);

    private SpritesEditorViewModel? _subscribed;

    /// <summary>
    /// Donde vive el zoom entre pestañas. El TabControl reconstruye la vista cada vez que
    /// se cambia, asi que la vista no puede recordarlo por su cuenta.
    /// </summary>
    private EditorPreferences? Preferences => (DataContext as SpritesEditorViewModel)?.Preferences;

    public SpritesEditorView() => InitializeComponent();

    public double ThumbnailSize
    {
        get => GetValue(ThumbnailSizeProperty);
        set => SetValue(ThumbnailSizeProperty, value);
    }

    public double GroupThumbnailSize
    {
        get => GetValue(GroupThumbnailSizeProperty);
        set => SetValue(GroupThumbnailSizeProperty, value);
    }

    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public double GroupPixelSize
    {
        get => GetValue(GroupPixelSizeProperty);
        set => SetValue(GroupPixelSizeProperty, value);
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
        RestoreZoom();
    }

    /// <summary>Deja marcados los botones del zoom que se estaba usando y lo aplica.</summary>
    private void RestoreZoom()
    {
        int canvas = Preferences?.SpriteCanvasZoom ?? 0;

        Check("EditZoom", canvas);
        Check("PreviewZoom", Preferences?.SpriteThumbnailZoom ?? 1);

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

    private void OnRefreshRequested(Sprite sprite) => CanvSprite.Redraw();

    /// <summary>
    /// El botón derecho también elige la miniatura.
    /// </summary>
    /// <remarks>
    /// Sin esto el ListBox sólo selecciona con el izquierdo, y las órdenes del menú
    /// contextual —copiar, pegar aquí— actuarían sobre el patrón que estuviera elegido de
    /// antes y no sobre el que se acaba de pulsar. «Pegar aquí» tiene que pegar aquí.
    /// </remarks>
    private void OnThumbnailPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(ThumbnailList).Properties.IsRightButtonPressed)
            return;

        if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>() is { } item)
            item.IsSelected = true;
    }

    private void OnZoomChanged(object? sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender is RadioButton { IsChecked: true, Tag: string tag } && int.TryParse(tag, out int index))
        {
            if (Preferences is { } preferences)
                preferences.SpriteCanvasZoom = index;

            ApplyZoom(index);
        }
    }

    private void OnThumbnailZoomChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true, Tag: string tag } || !int.TryParse(tag, out int factor))
            return;

        int scale = ThumbnailBaseScale * factor;

        if (Preferences is { } preferences)
            preferences.SpriteThumbnailZoom = factor;

        ThumbnailSize = GridSize * scale;
        GroupThumbnailSize = SpriteGroupRenderer.PreviewSize * scale;
        GroupPixelSize = scale;
    }

    private void OnThumbnailModeChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SpritesEditorViewModel vm)
            return;

        if (sender is RadioButton { IsChecked: true, Tag: string tag } && Enum.TryParse(tag, out ThumbnailMode mode))
            vm.ThumbnailMode = mode;
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
        if (sender is RadioButton { IsChecked: true, Tag: string tag }
            && Enum.TryParse(tag, out PixelCanvas.PaintingMode mode))
        {
            CanvSprite.PaintMode = mode;
        }
    }

    private void ApplyZoom(int index)
    {
        double size = ZoomSizes[Math.Clamp(index, 0, ZoomSizes.Length - 1)];

        EditorGrid.ColumnDefinitions[0].Width = new GridLength(size);
        CanvSprite.CanvasSize = size;
        CellSize = size / GridSize;
    }

}
