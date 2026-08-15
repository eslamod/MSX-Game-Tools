using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Views;

public partial class MapEditorView : UserControl
{
    /// <summary>Pixeles de pantalla por pixel de tile en el selector con el zoom a 1.</summary>
    private const int TileBaseScale = 8;

    /// <summary>Con el que se abre si no hay ajuste guardado. El mismo que marca el XAML.</summary>
    private const int DefaultTileZoom = 2;

    /// <summary>Lado de un tile en el selector de abajo.</summary>
    public static readonly StyledProperty<double> TileSizeProperty =
        AvaloniaProperty.Register<MapEditorView, double>(
            nameof(TileSize), defaultValue: TileBaseScale * 2);

    /// <summary>El tile por el que se empezó a arrastrar, para coger el rectángulo.</summary>
    private int _anchor = -1;

    private MapEditorViewModel? _subscribed;

    public MapEditorView() => InitializeComponent();

    public double TileSize
    {
        get => GetValue(TileSizeProperty);
        set => SetValue(TileSizeProperty, value);
    }

    private MapEditorViewModel? Editor => DataContext as MapEditorViewModel;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        Canvas.CellPressed += OnCellPressed;
        Canvas.CellDragged += OnCellDragged;
        Canvas.HoverChanged += OnHoverChanged;

        if (Editor is { } editor)
        {
            _subscribed = editor;
            editor.RefreshRequested += OnRefreshRequested;

            // Los bloques se editan en otro panel: al volver aqui pueden ser otros.
            editor.RefreshBlocks();
        }

        RestoreTileZoom();
        SelectSourceTab();
    }

    /// <summary>
    /// En un mapa de supertiles, el selector de abajo tiene que estar en los bloques.
    /// </summary>
    /// <remarks>
    /// Ocultar la pestaña de tiles no mueve la selección: la cabecera desaparece pero su
    /// contenido se queda delante, bajo la cabecera de Bloques. Se cogían tiles sueltos, y
    /// la etiqueta decía «Tile 4» en un mapa donde un tile suelto no se puede colocar en
    /// ninguna parte.
    /// </remarks>
    private void SelectSourceTab()
    {
        if (Editor is { UsesSuperTiles: true })
            SourceTabs.SelectedIndex = 1;
    }

    /// <summary>
    /// Deja marcado el zoom del selector que se estaba usando y lo aplica.
    /// </summary>
    /// <remarks>
    /// El TabControl reconstruye la vista al cambiar de pestaña, así que el zoom vive en
    /// los ajustes del espacio de trabajo y no aquí.
    /// </remarks>
    private void RestoreTileZoom()
    {
        int saved = Editor?.Preferences.MapTileZoom ?? DefaultTileZoom;

        // El paso mas cercano al guardado, no el que coincida: los pasos cambian -el X1 de
        // ocho pixeles se quito- y un ajuste escrito por una version anterior no encontraba
        // ninguno, con lo que la tira se quedaba al minimo y sin ningun boton marcado.
        RadioButton? button = ZoomButtons()
            .OrderBy(zoom => Math.Abs(FactorOf(zoom) - saved))
            .FirstOrDefault();

        if (button is null)
            return;

        int factor = FactorOf(button);

        button.IsChecked = true;

        // Devolverlo a los ajustes: si venia uno que ya no existe, no hay que seguir
        // arrastrandolo de una sesion a otra.
        if (Editor is { } editor)
            editor.Preferences.MapTileZoom = factor;

        // Por si el guardado ya era el que marca el XAML: entonces no ha saltado ningun
        // IsCheckedChanged y hay que aplicarlo a mano.
        TileSize = TileBaseScale * factor;
    }

    /// <summary>Los botones del zoom del selector, que son quienes llevan los pasos.</summary>
    private IEnumerable<RadioButton> ZoomButtons() =>
        this.GetVisualDescendants().OfType<RadioButton>().Where(zoom => zoom.GroupName == "MapTileZoom");

    private static int FactorOf(RadioButton button) =>
        int.TryParse((string?)button.Tag, out int factor) ? factor : 0;

    private void OnTileZoomChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true, Tag: string tag } || !int.TryParse(tag, out int factor))
            return;

        if (Editor is { } editor)
            editor.Preferences.MapTileZoom = factor;

        TileSize = TileBaseScale * factor;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        Canvas.CellPressed -= OnCellPressed;
        Canvas.CellDragged -= OnCellDragged;
        Canvas.HoverChanged -= OnHoverChanged;

        if (_subscribed is not null)
        {
            _subscribed.RefreshRequested -= OnRefreshRequested;
            _subscribed = null;
        }

        base.OnUnloaded(e);
    }

    private void OnRefreshRequested()
    {
        // Tambien aqui: el juego puede pasar a ser de supertiles con el mapa abierto, y
        // entonces la pestaña de tiles desaparece estando seleccionada.
        SelectSourceTab();

        Canvas.InvalidateVisual();
    }

    // ------------------------------------------------------------------ el mapa

    /// <summary>Dónde empezó a marcarse la selección.</summary>
    private (int Column, int Row)? _selectingFrom;

    private void OnCellPressed(int column, int row)
    {
        if (Editor is not { } editor)
            return;

        if (editor.Tool == MapTool.Select)
        {
            _selectingFrom = (column, row);
            editor.Select(column, row, column, row);

            return;
        }

        editor.Paint(column, row);
    }

    private void OnCellDragged(int column, int row)
    {
        if (Editor is not { } editor)
            return;

        if (editor.Tool == MapTool.Select)
        {
            if (_selectingFrom is { } from)
                editor.Select(from.Column, from.Row, column, row);

            return;
        }

        editor.Paint(column, row);
    }

    private void OnHoverChanged((int Column, int Row)? cell)
    {
        if (Editor is { } editor)
            editor.Hover = cell;
    }

    private void OnToolChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Tag: string tag }
            && Enum.TryParse(tag, out MapTool tool)
            && Editor is { } editor)
        {
            editor.Tool = tool;
        }
    }

    /// <summary>
    /// Redimensionar abre su formulario en el lateral.
    /// </summary>
    /// <remarks>
    /// Lo pide la ventana principal y no el panel: los formularios del lateral los reparte
    /// ella, que es quien sabe cuáles hay abiertos.
    /// </remarks>
    private void OnResize(object? sender, RoutedEventArgs e)
    {
        if (this.FindAncestorOfType<MainWindow>()?.DataContext is MainWindowViewModel main)
            main.ResizeMapCommand.Execute(null);
    }

    /// <inheritdoc cref="OnResize"/>
    private void OnReplaceTiles(object? sender, RoutedEventArgs e)
    {
        if (this.FindAncestorOfType<MainWindow>()?.DataContext is MainWindowViewModel main)
            main.ReplaceTilesCommand.Execute(null);
    }

    /// <inheritdoc cref="TileSetEditorView.OnPaletteColorClick"/>
    private void OnPaletteColorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.FindLogicalAncestorOfType<Popup>() is { } popup)
            Dispatcher.UIThread.Post(() => popup.Close());
    }

    private void OnFitZoom(object? sender, RoutedEventArgs e) =>
        Editor?.FitZoom(Canvas.Bounds.Width, Canvas.Bounds.Height);

    /// <summary>
    /// Control y rueda para el zoom, mayúsculas y rueda para pasar de bloque.
    /// </summary>
    /// <remarks>
    /// Va en la vista entera y no en el lienzo: al elegir bloques abajo también se quiere
    /// pasar al siguiente sin subir el ratón al mapa.
    /// </remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (Editor is not { } editor)
            return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // Por los pasos del zoom y no sumando uno: por debajo de x1 van en mitades.
            if (e.Delta.Y > 0)
                editor.ZoomInCommand.Execute(null);
            else
                editor.ZoomOutCommand.Execute(null);

            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            editor.StepBlock(-Math.Sign(e.Delta.Y));
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------------ el selector

    private void OnTilesPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Under<TileChoiceViewModel>(TileChoices, e.GetPosition(TileChoices)) is not { } choice)
            return;

        _anchor = choice.Index;

        Editor?.PickTiles(_anchor, _anchor);
    }

    /// <summary>Arrastrar coge el rectángulo, igual que en el panel de bloques.</summary>
    private void OnTilesMoved(object? sender, PointerEventArgs e)
    {
        if (_anchor < 0)
            return;

        if (Under<TileChoiceViewModel>(TileChoices, e.GetPosition(TileChoices)) is { } choice)
            Editor?.PickTiles(_anchor, choice.Index);
    }

    private void OnBlockPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: BlockChoiceViewModel choice })
            Editor?.PickBlock(choice.Block);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _anchor = -1;
        _selectingFrom = null;
    }

    /// <inheritdoc cref="TileBlocksView.Under{T}"/>
    private static T? Under<T>(Control list, Point position)
        where T : class =>
        list.InputHitTest(position) is Visual hit
            ? hit.GetSelfAndVisualAncestors()
                .OfType<Control>()
                .Select(control => control.DataContext)
                .OfType<T>()
                .FirstOrDefault()
            : null;
}
