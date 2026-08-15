using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

/// <summary>Formulario de creación de un juego de tiles.</summary>
/// <remarks>
/// Sólo pide el nombre. A diferencia de un banco de sprites no hay tipo que elegir: el
/// formato de un tile es el mismo en GRAPHIC 2 y en GRAPHIC 3, y lo único que cambia en
/// MSX2 es que los 16 colores se pueden redefinir, que ya es cosa de la paleta activa.
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

    [ObservableProperty]
    private int _superTileWidth = 2;

    [ObservableProperty]
    private int _superTileHeight = 2;

    public EditTileSetViewModel(MainWindowViewModel mainWindowVm)
    {
        _mainWindowVm = mainWindowVm;
        _palette = mainWindowVm.Palettes.ActivePalette;

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

    [RelayCommand]
    private void AcceptTileSet()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "El nombre del juego de tiles no puede estar vacío.";
            return;
        }

        if (UseSuperTiles
            && (SuperTileWidth is < 1 || SuperTileHeight is < 1
                || SuperTileWidth > MaxSuperTileSide || SuperTileHeight > MaxSuperTileSide))
        {
            ErrorMessage = $"El supertile tiene que medir entre 1 y {MaxSuperTileSide} en cada lado.";
            return;
        }

        ErrorMessage = null;

        var tileSet = new TileSet(Name);

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
