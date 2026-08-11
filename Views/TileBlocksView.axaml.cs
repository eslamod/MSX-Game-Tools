using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;

namespace MSX_SpritesEditor.Views;

public partial class TileBlocksView : UserControl
{
    /// <summary>Pixeles de pantalla por pixel de tile a X1.</summary>
    private const int BaseScale = 2;

    /// <summary>Lado de una celda de la rejilla donde se compone el bloque.</summary>
    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<TileBlocksView, double>(
            nameof(CellSize),
            defaultValue: TileRow.Columns * BaseScale * 2);

    /// <summary>Lado de un tile en el selector.</summary>
    public static readonly StyledProperty<double> TileSizeProperty =
        AvaloniaProperty.Register<TileBlocksView, double>(
            nameof(TileSize),
            defaultValue: TileRow.Columns * BaseScale);

    /// <summary>El tile por el que se empezó a arrastrar, para coger el rectángulo.</summary>
    private int _anchor = -1;

    /// <summary>Si el arrastre por la rejilla está borrando en vez de estampando.</summary>
    private bool _erasing;

    private bool _painting;

    public TileBlocksView() => InitializeComponent();

    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public double TileSize
    {
        get => GetValue(TileSizeProperty);
        set => SetValue(TileSizeProperty, value);
    }

    private TileBlocksViewModel? Panel => DataContext as TileBlocksViewModel;

    private EditorPreferences? Preferences => Panel?.Preferences;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        RestoreZoom();
    }

    /// <summary>
    /// Deja marcados los botones del zoom que se estaba usando y lo aplica.
    /// </summary>
    /// <remarks>
    /// El TabControl reconstruye la vista al cambiar de pestaña, así que el zoom vive en
    /// los ajustes del espacio de trabajo y no aquí.
    /// </remarks>
    private void RestoreZoom()
    {
        Check("BlockGridZoom", Preferences?.BlockGridZoom ?? 2);
        Check("BlockTileZoom", Preferences?.BlockTileZoom ?? 1);

        // Por si el guardado ya era el que marca el XAML: entonces no ha saltado ningun
        // IsCheckedChanged y hay que aplicarlo a mano.
        CellSize = TileRow.Columns * BaseScale * (Preferences?.BlockGridZoom ?? 2);
        TileSize = TileRow.Columns * BaseScale * (Preferences?.BlockTileZoom ?? 1);
    }

    private void Check(string group, int tag)
    {
        RadioButton? button = this.GetVisualDescendants()
            .OfType<RadioButton>()
            .FirstOrDefault(r => r.GroupName == group && (string?)r.Tag == tag.ToString());

        if (button is not null)
            button.IsChecked = true;
    }

    private void OnGridZoomChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true, Tag: string tag } || !int.TryParse(tag, out int factor))
            return;

        if (Preferences is { } preferences)
            preferences.BlockGridZoom = factor;

        CellSize = TileRow.Columns * BaseScale * factor;
    }

    private void OnTileZoomChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true, Tag: string tag } || !int.TryParse(tag, out int factor))
            return;

        if (Preferences is { } preferences)
            preferences.BlockTileZoom = factor;

        TileSize = TileRow.Columns * BaseScale * factor;
    }

    // ------------------------------------------------------------------ componer

    /// <summary>
    /// Estampa lo cogido, o lo borra con el botón derecho.
    /// </summary>
    /// <remarks>
    /// El botón derecho borra como en Tiled, y vaciar una celda no es ponerle el tile 0:
    /// al estampar el bloque en el mapa, la vacía deja lo que hubiera debajo.
    /// </remarks>
    private void OnGridPressed(object? sender, PointerPressedEventArgs e)
    {
        _erasing = e.GetCurrentPoint(BlockGrid).Properties.IsRightButtonPressed;
        _painting = true;

        PaintAt(e.GetPosition(BlockGrid));
    }

    /// <summary>Arrastrar sigue pintando, que es lo cómodo para rellenar una fila.</summary>
    private void OnGridMoved(object? sender, PointerEventArgs e)
    {
        if (_painting)
            PaintAt(e.GetPosition(BlockGrid));
    }

    private void PaintAt(Point position)
    {
        if (Under<BlockCellViewModel>(BlockGrid, position) is not { } cell)
            return;

        if (_erasing)
            Panel?.Erase(cell.Column, cell.Row);
        else
            Panel?.Paint(cell.Column, cell.Row);
    }

    // ------------------------------------------------------------------ elegir tiles

    private void OnTilesPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Under<TileChoiceViewModel>(TileChoices, e.GetPosition(TileChoices)) is not { } tile)
            return;

        _anchor = tile.Index;

        Panel?.SelectTile(tile.Index);
    }

    /// <summary>Arrastrar coge el rectángulo que va del tile de partida a éste.</summary>
    private void OnTilesMoved(object? sender, PointerEventArgs e)
    {
        if (_anchor < 0)
            return;

        if (Under<TileChoiceViewModel>(TileChoices, e.GetPosition(TileChoices)) is { } tile)
            Panel?.SelectRange(_anchor, tile.Index);
    }

    /// <summary>
    /// Lo que hay bajo el puntero dentro de esa lista.
    /// </summary>
    /// <remarks>
    /// Los gestos se atienden en el contenedor y no en cada celda: al pulsar, Avalonia
    /// captura el puntero para el control pulsado, así que las vecinas no llegan a
    /// enterarse de que el ratón ha pasado por encima y arrastrar no pintaría más que la
    /// primera. Buscando por posición da igual quién tenga la captura.
    /// </remarks>
    private static T? Under<T>(Control list, Point position)
        where T : class =>
        list.InputHitTest(position) is Visual hit
            ? hit.GetSelfAndVisualAncestors()
                .OfType<Control>()
                .Select(control => control.DataContext)
                .OfType<T>()
                .FirstOrDefault()
            : null;

    /// <summary>
    /// Soltar termina los dos arrastres.
    /// </summary>
    /// <remarks>
    /// Va en el control entero y no en cada celda: al soltar fuera de donde se empezó,
    /// el evento no llega a la celda y el arrastre se quedaría enganchado.
    /// </remarks>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _painting = false;
        _anchor = -1;
    }
}
