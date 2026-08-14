using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Una fila de la lista de sustituciones: de qué tile a qué tile, con sus dibujos.
/// </summary>
/// <remarks>
/// No se edita: si te equivocas la quitas y la vuelves a poner. Una fila con dos números
/// se rehace en dos segundos, y un modo de edición sería más interfaz que la que arregla.
/// </remarks>
public sealed class TileSubstitution(int from, int to, ImageMini? fromTile, ImageMini? toTile)
{
    public int From { get; } = from;

    public int To { get; } = to;

    public ImageMini? FromTile { get; } = fromTile;

    public ImageMini? ToTile { get; } = toTile;
}

/// <summary>
/// Formulario para cambiar unos tiles por otros en el mapa.
/// </summary>
/// <remarks>
/// <para>
/// Dos modos. Por <b>rango</b>, que mueve un grupo entero con el mismo desplazamiento:
/// del 10, 11 y 12 al 20 salen 20, 21 y 22 de una pasada. Y por <b>lista</b>, que es el
/// caso de verdad cuando los tiles no están seguidos: los de un árbol pueden ser el 35,
/// 36, 37, 67, 68 y 69, y sus destinos tampoco tienen por qué ir en fila.
/// </para>
/// <para>
/// Los dos acaban en el mismo sitio: el rango construye su tabla y llama al mismo motor,
/// así que tratan igual los destinos que no existen, la cuenta de celdas y el deshacer.
/// </para>
/// </remarks>
public partial class ReplaceTilesViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;
    private readonly MapEditorViewModel _editor;

    /// <summary>Por lista en vez de por rango.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRangeMode))]
    private bool _isListMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeLabel))]
    private int _fromFirst;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeLabel))]
    private int _fromLast;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeLabel))]
    private int _toFirst;

    /// <summary>El origen de la fila que se está preparando.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewFromTile))]
    [NotifyPropertyChangedFor(nameof(AlreadyListed))]
    [NotifyCanExecuteChangedFor(nameof(AddSubstitutionCommand))]
    private int _newFrom;

    /// <summary>Y su destino.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewToTile))]
    private int _newTo;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSubstitutionCommand))]
    private TileSubstitution? _selectedSubstitution;

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

        Header = Text.Format("ReplaceHeader", editor.Map.Name);
        TagId = "replace:tiles";

        HasSelection = editor.HasSelection;
        OnlySelection = HasSelection;
    }

    private static Localizer Text => Localizer.Instance;

    /// <summary>Si había algo marcado al abrir el formulario.</summary>
    public bool HasSelection { get; }

    public int MaxTile => TileSet.TileCount - 1;

    public bool IsRangeMode => !IsListMode;

    /// <summary>Las sustituciones, en el orden en que se fueron poniendo.</summary>
    public ObservableCollection<TileSubstitution> Substitutions { get; } = [];

    public ImageMini? NewFromTile => TileAt(NewFrom);

    public ImageMini? NewToTile => TileAt(NewTo);

    /// <summary>
    /// Si el origen que hay puesto ya tiene su fila.
    /// </summary>
    /// <remarks>
    /// Dos filas con el mismo origen no tienen sentido: la segunda no llegaría a pasar
    /// nunca, porque cada celda se mira una sola vez. Más vale no dejar añadirla y decirlo
    /// que aceptar una fila muerta que parece que hace algo.
    /// </remarks>
    public bool AlreadyListed => Substitutions.Any(row => row.From == NewFrom);

    /// <summary>Lo que va a pasar, escrito, para poder comprobarlo antes de darle.</summary>
    public string RangeLabel
    {
        get
        {
            int count = Math.Abs(FromLast - FromFirst) + 1;
            int first = Math.Min(FromFirst, FromLast);

            return count == 1
                ? Text.Format("ReplaceOneLabel", first, ToFirst)
                : Text.Format("ReplaceRangeLabel", count, first, first + count - 1, ToFirst, ToFirst + count - 1);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddSubstitution))]
    private void AddSubstitution()
    {
        Substitutions.Add(new TileSubstitution(NewFrom, NewTo, NewFromTile, NewToTile));

        // Y el origen pasa al siguiente que quede libre. Si se quedara donde está, el
        // panel se planta con el aviso de repetido justo después de un añadido que ha
        // ido bien, y parece que ha fallado. Además, encadenar tiles suele ir seguido:
        // el 35, el 36, el 37.
        NewFrom = NextFreeFrom();

        OnPropertyChanged(nameof(AlreadyListed));
        AddSubstitutionCommand.NotifyCanExecuteChanged();
    }

    private bool CanAddSubstitution() => !AlreadyListed;

    /// <summary>
    /// El primer tile a partir del que hay puesto que no tenga ya su fila.
    /// </summary>
    /// <remarks>
    /// Da la vuelta al llegar al final, y si no queda ninguno libre se queda donde estaba:
    /// entonces el aviso ya dice lo que pasa.
    /// </remarks>
    private int NextFreeFrom()
    {
        for (int tile = NewFrom + 1; tile <= MaxTile; tile++)
        {
            if (!Substitutions.Any(row => row.From == tile))
                return tile;
        }

        for (int tile = 0; tile < NewFrom; tile++)
        {
            if (!Substitutions.Any(row => row.From == tile))
                return tile;
        }

        return NewFrom;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSubstitution))]
    private void RemoveSubstitution()
    {
        if (SelectedSubstitution is null)
            return;

        Substitutions.Remove(SelectedSubstitution);
        SelectedSubstitution = null;

        OnPropertyChanged(nameof(AlreadyListed));
        AddSubstitutionCommand.NotifyCanExecuteChanged();
    }

    private bool HasSelectedSubstitution() => SelectedSubstitution is not null;

    [RelayCommand]
    private void AcceptReplace()
    {
        if (IsListMode && Substitutions.Count == 0)
        {
            ResultMessage = Text["ReplaceListEmpty"];

            return;
        }

        TileMap map = _editor.Map;

        (int left, int top, int width, int height) = OnlySelection && _editor.Selection is { } region
            ? (region.Left, region.Top, region.Width, region.Height)
            : (0, 0, map.Width, map.Height);

        IEnumerable<int> layers = AllLayers
            ? Enumerable.Range(0, map.Layers.Count)
            : [_editor.ActiveLayerIndex];

        int changed = IsListMode
            ? map.Replace(Table(), left, top, width, height, layers)
            : map.Replace(FromFirst, FromLast, ToFirst, left, top, width, height, layers);

        _editor.AfterReplace();

        // Se queda abierto: sustituir suele hacerse varias veces seguidas, y cerrarlo
        // obligaria a volver a abrirlo y a rellenarlo para el siguiente rango.
        ResultMessage = changed == 0
            ? Text["ReplaceNothing"]
            : Text.Format("ReplaceDone", changed);
    }

    /// <summary>La lista como tabla, que es lo que entiende el mapa.</summary>
    private Dictionary<int, int> Table() => Substitutions.ToDictionary(row => row.From, row => row.To);

    /// <summary>El dibujo de un tile, o nada si ese número no existe en el juego.</summary>
    private ImageMini? TileAt(int index) =>
        (uint)index < (uint)_editor.Tiles.Count ? _editor.Tiles[index] : null;

    [RelayCommand]
    private void CloseReplace() => _mainWindowVm.RightPanViewModel = null;
}
