using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Formulario de creación de un mapa.
/// </summary>
/// <remarks>
/// Además del nombre pide el tamaño y el juego de tiles. El juego no es un adorno: un
/// mapa son números de tile y esos números no significan nada sin saber de cuál son, así
/// que sin ningún juego en el proyecto no hay mapa que crear.
/// </remarks>
public partial class EditMapViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _columns = 32;

    [ObservableProperty]
    private int _rows = 24;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesSuperTiles))]
    [NotifyPropertyChangedFor(nameof(KindLabel))]
    private TileSetEditorViewModel? _tileSet;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public EditMapViewModel(MainWindowViewModel mainWindowVm)
    {
        _mainWindowVm = mainWindowVm;

        // Con la cabecera vacia la pestaña parecia rota.
        // La cabecera se fija al abrir el formulario, así que un cambio de idioma con él
        // ya abierto no la mueve. Se abren y se cierran en un momento; no compensa más.
        Header = Localizer.Instance["NewMapTitle"];
        TagId = "new:map";

        TileSets = mainWindowVm.TileSets;
        TileSet = TileSets.FirstOrDefault();

        if (TileSets.Count == 0)
            ErrorMessage = Localizer.Instance["NewMapNeedsTileSet"];
    }

    /// <summary>Los juegos entre los que elegir, tal como estaban al abrir el formulario.</summary>
    public IReadOnlyList<TileSetEditorViewModel> TileSets { get; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Lo más grande que se puede pedir, que es lo que aguanta el mapa.</summary>
    public int MaxSide => TileMap.MaxSide;

    /// <summary>
    /// Si el mapa va a ser de supertiles, que lo decide el juego con el que se dibuja.
    /// </summary>
    /// <remarks>
    /// No se elige aquí: el tamaño del supertile es del juego de tiles, así que preguntarlo
    /// por mapa dejaría a dos mapas del mismo juego pidiendo tamaños distintos. Aquí sólo
    /// se dice lo que va a salir, para no enterarse después.
    /// </remarks>
    public bool UsesSuperTiles => TileSet?.TileSet.HasSuperTiles ?? false;

    /// <summary>De qué va el mapa que va a salir, y en qué unidades se está pidiendo.</summary>
    public string KindLabel
    {
        get
        {
            if (TileSet?.TileSet is not { HasSuperTiles: true } tiles)
                return Localizer.Instance["NewMapKindTiles"];

            return Localizer.Instance.Format(
                "NewMapKindSuperTiles", tiles.SuperTileWidth, tiles.SuperTileHeight);
        }
    }

    [RelayCommand]
    private void AcceptMap()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = Localizer.Instance["NewMapNoName"];
            return;
        }

        if (TileSet is not { } tiles)
        {
            ErrorMessage = Localizer.Instance["NewMapNoTileSet"];
            return;
        }

        if (Columns is < 1 || Rows is < 1 || Columns > MaxSide || Rows > MaxSide)
        {
            ErrorMessage = Localizer.Instance.Format("NewMapSizeRange", MaxSide);
            return;
        }

        ErrorMessage = null;

        var map = new TileMap(Name, Columns, Rows)
        {
            // El fondo arranca en el color más oscuro de la paleta y no en un índice fijo:
            // dar por negro el 1 ya nos costó un fallo con las paletas generadas.
            BackgroundColorIndex = tiles.ColorPalette.DefaultBackgroundIndex,
        };

        // Recien creado y vacio: no hay nada que perder todavia, asi que sale sin marcar.
        _mainWindowVm.OpenMap(map, tiles).MarkClean();
        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelMap() => _mainWindowVm.RightPanViewModel = null;
}
