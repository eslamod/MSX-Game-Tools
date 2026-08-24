using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

/// <summary>Dónde queda lo que ya había al cambiar el tamaño del mapa.</summary>
public enum MapAnchor
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Centre,
    Right,
    BottomLeft,
    Bottom,
    BottomRight,
}

/// <summary>
/// Formulario de redimensionado de un mapa.
/// </summary>
/// <remarks>
/// Es un formulario y no unas casillas en el panel porque la operación descoloca el mapa
/// entero: no puede dispararse al teclear un dígito. Y lleva ancla porque sin ella sólo
/// se podría crecer por abajo y por la derecha, y alargar un nivel por el principio
/// obligaría a repintarlo.
/// </remarks>
public partial class ResizeMapViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;
    private readonly MapEditorViewModel _editor;

    [ObservableProperty]
    private int _columns;

    [ObservableProperty]
    private int _rows;

    [ObservableProperty]
    private MapAnchor _anchor = MapAnchor.TopLeft;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public ResizeMapViewModel(MainWindowViewModel mainWindowVm, MapEditorViewModel editor)
    {
        _mainWindowVm = mainWindowVm;
        _editor = editor;

        Header = $"Redimensionar {editor.Map.Name}";
        TagId = "resize:map";

        Columns = editor.Map.Width;
        Rows = editor.Map.Height;
    }

    public IReadOnlyList<MapAnchor> Anchors { get; } = Enum.GetValues<MapAnchor>();

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public int MaxSide => TileMap.MaxSide;

    /// <summary>Lo que mide ahora, para tenerlo delante al decidir.</summary>
    public string CurrentLabel => $"Ahora: {_editor.Map.Width} x {_editor.Map.Height}";

    [RelayCommand]
    private void AcceptResize()
    {
        if (Columns is < 1 || Rows is < 1 || Columns > MaxSide || Rows > MaxSide)
        {
            ErrorMessage = Localizer.Instance.Format("NewMapSizeRange", MaxSide);
            return;
        }

        ErrorMessage = null;

        TileMap map = _editor.Map;

        (int column, int row) = OffsetFor(Anchor, map.Width, map.Height, Columns, Rows);

        map.Resize(Columns, Rows, column, row);

        _editor.AfterResize();
        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelResize() => _mainWindowVm.RightPanViewModel = null;

    /// <summary>
    /// Cuánto hay que correr lo que había para que quede donde dice el ancla.
    /// </summary>
    /// <remarks>
    /// Al encoger sale negativo, que es lo correcto: anclando a la derecha se pierde lo de
    /// la izquierda y no al revés.
    /// </remarks>
    public static (int Column, int Row) OffsetFor(
        MapAnchor anchor, int oldWidth, int oldHeight, int newWidth, int newHeight)
    {
        int growX = newWidth - oldWidth;
        int growY = newHeight - oldHeight;

        int column = anchor switch
        {
            MapAnchor.Top or MapAnchor.Centre or MapAnchor.Bottom => growX / 2,
            MapAnchor.TopRight or MapAnchor.Right or MapAnchor.BottomRight => growX,
            _ => 0,
        };

        int row = anchor switch
        {
            MapAnchor.Left or MapAnchor.Centre or MapAnchor.Right => growY / 2,
            MapAnchor.BottomLeft or MapAnchor.Bottom or MapAnchor.BottomRight => growY,
            _ => 0,
        };

        return (column, row);
    }
}
