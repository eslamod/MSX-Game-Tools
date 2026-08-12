using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Formulario para cambiar unos tiles por otros en el mapa.
/// </summary>
/// <remarks>
/// Va por rango y no de uno en uno porque el caso real es mover un grupo entero: cambiar
/// el 10, 11 y 12 por el 20, 21 y 22 se pide una vez en vez de tres. El desplazamiento es
/// el mismo para todo el rango, que es justo lo que pasa al recolocar tiles en el juego.
/// </remarks>
public partial class ReplaceTilesViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;
    private readonly MapEditorViewModel _editor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeLabel))]
    private int _fromFirst;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeLabel))]
    private int _fromLast;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeLabel))]
    private int _toFirst;

    /// <summary>En todas las capas o sólo en la activa.</summary>
    [ObservableProperty]
    private bool _allLayers;

    /// <summary>Sólo dentro de lo marcado, si hay algo marcado.</summary>
    [ObservableProperty]
    private bool _onlySelection;

    [ObservableProperty]
    private string? _resultMessage;

    public ReplaceTilesViewModel(MainWindowViewModel mainWindowVm, MapEditorViewModel editor)
    {
        _mainWindowVm = mainWindowVm;
        _editor = editor;

        Header = $"Sustituir tiles en {editor.Map.Name}";
        TagId = "replace:tiles";

        HasSelection = editor.HasSelection;
        OnlySelection = HasSelection;
    }

    /// <summary>Si había algo marcado al abrir el formulario.</summary>
    public bool HasSelection { get; }

    public int MaxTile => TileSet.TileCount - 1;

    /// <summary>Lo que va a pasar, escrito, para poder comprobarlo antes de darle.</summary>
    public string RangeLabel
    {
        get
        {
            int count = Math.Abs(FromLast - FromFirst) + 1;
            int first = Math.Min(FromFirst, FromLast);

            return count == 1
                ? $"El tile {first} pasa a ser el {ToFirst}."
                : $"Los {count} tiles del {first} al {first + count - 1} pasan a "
                  + $"ser del {ToFirst} al {ToFirst + count - 1}.";
        }
    }

    [RelayCommand]
    private void AcceptReplace()
    {
        TileMap map = _editor.Map;

        (int left, int top, int width, int height) = OnlySelection && _editor.Selection is { } region
            ? (region.Left, region.Top, region.Width, region.Height)
            : (0, 0, map.Width, map.Height);

        IEnumerable<int> layers = AllLayers
            ? Enumerable.Range(0, map.Layers.Count)
            : [_editor.ActiveLayerIndex];

        int changed = map.Replace(FromFirst, FromLast, ToFirst, left, top, width, height, layers);

        _editor.AfterReplace();

        // Se queda abierto: sustituir suele hacerse varias veces seguidas, y cerrarlo
        // obligaria a volver a abrirlo y a rellenarlo para el siguiente rango.
        ResultMessage = changed == 0
            ? "No había ninguna celda con esos tiles."
            : $"Cambiadas {changed} celdas.";
    }

    [RelayCommand]
    private void CloseReplace() => _mainWindowVm.RightPanViewModel = null;
}
