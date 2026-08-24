using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

/// <summary>Formulario de creación de un juego de tiles.</summary>
/// <remarks>
/// El modo se elige aquí y no se cambia después, como el tipo de un banco de sprites: de él
/// cuelgan el fichero, la exportación y media pantalla del editor. GRAPHIC 2 y 3 son el mismo
/// modo a estos efectos —lo único que cambia en MSX2 es que los 16 colores se pueden
/// redefinir, y eso ya es cosa de la paleta—; GRAPHIC 1 no, que allí el color no es del tile.
/// </remarks>
public partial class EditTileSetViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>Con qué colores se dibuja, que el juego guarda dentro de su fichero.</summary>
    [ObservableProperty]
    private ColorPalette _palette;

    /// <summary>
    /// Si los mapas de este juego se dibujan con supertiles en vez de tiles sueltos.
    /// </summary>
    /// <remarks>
    /// Se pregunta aquí y no al crear el mapa porque el tamaño del supertile es del juego:
    /// un supertile es un bloque con el tamaño clavado, y los bloques cuelgan del juego.
    /// Preguntándolo por mapa, dos mapas del mismo juego podrían pedir tamaños distintos.
    /// </remarks>
    [ObservableProperty]
    private bool _useSuperTiles;

    /// <summary>
    /// En qué modo gráfico se va a usar el juego.
    /// </summary>
    /// <remarks>
    /// Se guarda la opción entera y no el enum suelto porque el desplegable enseña el nombre
    /// traducido. Y se ignora el null: al cambiar de idioma la lista se rehace y el
    /// desplegable escribe un hueco de vuelta antes de reelegir, que dejaría el formulario
    /// sin modo justo cuando el usuario no ha tocado nada.
    /// </remarks>
    private TileModeChoice _selectedMode = null!;

    [ObservableProperty]
    private int _superTileWidth = 2;

    [ObservableProperty]
    private int _superTileHeight = 2;

    public EditTileSetViewModel(MainWindowViewModel mainWindowVm)
    {
        _mainWindowVm = mainWindowVm;
        _palette = mainWindowVm.Palettes.ActivePalette;
        _selectedMode = Modes[0];

        // Con la cabecera vacia la pestaña parecia rota.
        Header = Localizer.Instance["NewTileSetTitle"];
        TagId = "new:tileset";
    }

    /// <summary>
    /// Las paletas del proyecto, para elegir con cuál nace el juego.
    /// </summary>
    /// <remarks>
    /// Se pregunta aquí en vez de darle la que enseñe la barra y ya está: la paleta va
    /// dentro del fichero del juego, así que es una decisión suya, y hacerla a escondidas
    /// dejaba al usuario sin saber de dónde le había salido.
    /// </remarks>
    public IReadOnlyList<ColorPalette> Palettes => _mainWindowVm.Palettes.Palettes;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Lo más grande que puede medir un supertile por cada lado.</summary>
    public static int MaxSuperTileSide => TileSet.MaxSuperTileSide;

    /// <summary>Los modos entre los que se elige, con el nombre de pantalla de cada uno.</summary>
    public IReadOnlyList<TileModeChoice> Modes { get; } =
    [
        new(TileSet.GraphicMode.Graphic2, "NewTileSetModeGraphic2"),
        new(TileSet.GraphicMode.Graphic1, "NewTileSetModeGraphic1"),
    ];

    /// <inheritdoc cref="_selectedMode"/>
    public TileModeChoice SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (value is null || ReferenceEquals(value, _selectedMode))
                return;

            _selectedMode = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(IsGraphic1));
        }
    }

    /// <summary>El modo elegido, que es lo que se le pasa al juego al crearlo.</summary>
    public TileSet.GraphicMode Mode => SelectedMode.Mode;

    /// <summary>Para poder explicar en el formulario en qué se va a notar screen 1.</summary>
    public bool IsGraphic1 => Mode == TileSet.GraphicMode.Graphic1;

    [RelayCommand]
    private void AcceptTileSet()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = Localizer.Instance["NewTileSetNoName"];
            return;
        }

        if (UseSuperTiles
            && (SuperTileWidth is < 1 || SuperTileHeight is < 1
                || SuperTileWidth > MaxSuperTileSide || SuperTileHeight > MaxSuperTileSide))
        {
            ErrorMessage = Localizer.Instance.Format("NewTileSetSuperRange", MaxSuperTileSide);
            return;
        }

        ErrorMessage = null;

        var tileSet = new TileSet(Name, Mode);

        if (UseSuperTiles)
        {
            tileSet.SuperTileWidth = SuperTileWidth;
            tileSet.SuperTileHeight = SuperTileHeight;
        }

        // Recien creado y vacio: no hay nada que perder todavia, asi que sale sin marcar.
        _mainWindowVm.OpenTileSet(tileSet, Palette).MarkClean();
        _mainWindowVm.RightPanViewModel = null;
    }

    [RelayCommand]
    private void CancelTileSet() => _mainWindowVm.RightPanViewModel = null;
}

/// <summary>Un modo en el desplegable, con su nombre traducido.</summary>
/// <remarks>
/// El nombre no sale del enum: en el desplegable tiene que leerse «Screen 2 y 4», que es como
/// se llama esto de cara a quien lo usa, y además cambia de idioma.
/// </remarks>
public sealed record TileModeChoice(TileSet.GraphicMode Mode, string Key)
{
    public string Name => Localizer.Instance[Key];
}
