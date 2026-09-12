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
    [NotifyPropertyChangedFor(nameof(ShowsMiddleThird))]
    [NotifyPropertyChangedFor(nameof(ShowsBottomThird))]
    private int _rows = 24;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesSuperTiles))]
    [NotifyPropertyChangedFor(nameof(KindLabel))]
    [NotifyPropertyChangedFor(nameof(ShowsMiddleThird))]
    [NotifyPropertyChangedFor(nameof(ShowsBottomThird))]
    private TileSetEditorViewModel? _tileSet;

    /// <summary>The tile set of the middle band of the screen.</summary>
    [ObservableProperty]
    private TileSetEditorViewModel? _middleTileSet;

    /// <summary>And of the bottom one.</summary>
    [ObservableProperty]
    private TileSetEditorViewModel? _bottomTileSet;

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

    /// <summary>
    /// How many tile sets the map being asked for can take, from one to three.
    /// </summary>
    /// <remarks>
    /// It comes out of the rows and of the tile set: a map taller than the screen does not
    /// split, and neither does one of GRAPHIC 1 or one of super tiles.
    /// </remarks>
    private int Slots => TileSet is { } tiles ? TileMap.SlotsOf(Rows, tiles.TileSet) : 1;

    /// <summary>Whether the middle band of the screen gets a tile set of its own.</summary>
    public bool ShowsMiddleThird => Slots >= 2;

    /// <summary>And the bottom one.</summary>
    public bool ShowsBottomThird => Slots >= 3;

    /// <summary>
    /// The other bands follow the tile set of the map while nobody touches them.
    /// </summary>
    /// <remarks>
    /// Three bands of the same set is the ordinary map, so that is what the form offers to
    /// begin with; and changing the map's set has to take them along, or the other two would be
    /// left pointing at the one that is no longer there. It also means that what is read is
    /// what comes out, with no empty box meaning "the same as the one above".
    /// </remarks>
    partial void OnTileSetChanged(TileSetEditorViewModel? value)
    {
        MiddleTileSet = value;
        BottomTileSet = value;
    }

    /// <summary>The tile set of each band, the one of the map first.</summary>
    private IEnumerable<TileSetEditorViewModel> Chosen(TileSetEditorViewModel tiles)
    {
        yield return tiles;

        if (ShowsMiddleThird)
            yield return MiddleTileSet ?? tiles;

        if (ShowsBottomThird)
            yield return BottomTileSet ?? tiles;
    }

    /// <summary>
    /// The first band that does not share the palette, if there is one.
    /// </summary>
    /// <remarks>
    /// There is one palette on the screen, so three tile sets with three palettes is something
    /// the machine cannot paint -it would take changing it mid screen with a line interrupt,
    /// which is not what this is for-. The same palette and not the same colours: two that
    /// merely look alike today come apart the day one of them is edited.
    /// </remarks>
    private TileSetEditorViewModel? Clash(TileSetEditorViewModel tiles) =>
        Chosen(tiles).FirstOrDefault(band => !ReferenceEquals(band.ColorPalette, tiles.ColorPalette));

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

        if (Clash(tiles) is { } mixed)
        {
            ErrorMessage = Localizer.Instance.Format("NewMapThirdPalette", mixed.TileSet.Name);
            return;
        }

        ErrorMessage = null;

        var map = new TileMap(Name, Columns, Rows)
        {
            // El fondo arranca en el color más oscuro de la paleta y no en un índice fijo:
            // dar por negro el 1 ya nos costó un fallo con las paletas generadas.
            BackgroundColorIndex = tiles.ColorPalette.DefaultBackgroundIndex,
        };

        map.UseTileSets([.. Chosen(tiles).Select(band => new TileSetRef(band.TileSet.Id, band.TileSet.Name))]);

        // Recien creado y vacio: no hay nada que perder todavia, asi que sale sin marcar.
        _mainWindowVm.OpenMap(map, tiles).MarkClean();
        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelMap() => _mainWindowVm.RightPanViewModel = null;
}
