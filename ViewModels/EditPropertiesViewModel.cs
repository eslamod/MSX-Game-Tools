using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Los atributos de un documento: de momento sólo el nombre.
/// </summary>
/// <remarks>
/// <para>
/// Un panel para los tres tipos y no uno por tipo: lo que tienen en común es lo que hay
/// hoy, y el día que un mapa quiera enseñar aquí su tamaño y un juego su color de borde,
/// se añaden como secciones que aparecen según lo que sea el documento.
/// </para>
/// <para>
/// Con aceptar y cancelar, y no escribiendo según se teclea: renombrar toca la pestaña, el
/// árbol y los mapas que usan el juego, y ver todo eso bailar letra a letra mientras se
/// escribe es desconcertante.
/// </para>
/// </remarks>
public partial class EditPropertiesViewModel : PanelBaseViewModel
{
    private readonly MainWindowViewModel _mainWindowVm;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>
    /// Si los mapas de este juego van por supertiles. Sólo para juegos de tiles.
    /// </summary>
    /// <remarks>
    /// Aquí y no sólo al crear el juego: un juego que ya existe, con sus tiles dibujados,
    /// es justo el que uno quiere pasar a supertiles, y volver a empezar de cero para eso
    /// no tiene ningún sentido.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SuperTileWarning))]
    [NotifyPropertyChangedFor(nameof(HasSuperTileWarning))]
    private bool _useSuperTiles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SuperTileWarning))]
    [NotifyPropertyChangedFor(nameof(HasSuperTileWarning))]
    private int _superTileWidth = 2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SuperTileWarning))]
    [NotifyPropertyChangedFor(nameof(HasSuperTileWarning))]
    private int _superTileHeight = 2;

    public EditPropertiesViewModel(MainWindowViewModel mainWindowVm, PanelBaseViewModel document)
    {
        _mainWindowVm = mainWindowVm;

        Document = document;
        _name = document.DocumentName;

        if (document is TileSetEditorViewModel { TileSet: { HasSuperTiles: true } tiles })
        {
            _useSuperTiles = true;
            _superTileWidth = tiles.SuperTileWidth;
            _superTileHeight = tiles.SuperTileHeight;
        }

        Header = $"{Localizer.Instance["TreeProperties"]}: {document.DocumentName}";
        TagId = "properties";
    }

    /// <summary>El documento cuyos atributos se están tocando.</summary>
    public PanelBaseViewModel Document { get; }

    /// <summary>Qué es, para que se vea de qué se están viendo las propiedades.</summary>
    public string Kind => Document.DocumentKind;

    /// <summary>Dónde está guardado, o que todavía no lo está.</summary>
    public string Where => Document.FilePath ?? "Sin guardar en ningún fichero todavía.";

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Aviso de lo que arrastra el cambio, cuando arrastra algo.
    /// </summary>
    /// <remarks>
    /// Renombrar un juego de tiles ya no rompe nada —los mapas lo señalan por identidad—
    /// pero sí les cambia el fichero, y quedarse con tres mapas sin guardar sin saber por
    /// qué es peor que un renglón de aviso.
    /// </remarks>
    public string Affected
    {
        get
        {
            if (Document is not TileSetEditorViewModel tiles)
                return string.Empty;

            int maps = _mainWindowVm.MapsOf(tiles).Count;

            return maps switch
            {
                0 => string.Empty,
                1 => "Hay un mapa que se dibuja con él y también quedará sin guardar.",
                _ => $"Hay {maps} mapas que se dibujan con él y también quedarán sin guardar.",
            };
        }
    }

    public bool HasAffected => Affected.Length > 0;

    /// <summary>Los supertiles sólo salen si lo que se está mirando es un juego de tiles.</summary>
    public bool IsTileSet => Document is TileSetEditorViewModel;

    public static int MaxSuperTileSide => TileSet.MaxSuperTileSide;

    /// <summary>
    /// Lo que va a pasarle a lo que ya hay, escrito antes de aceptar.
    /// </summary>
    /// <remarks>
    /// Los dos efectos son de los que no se ven venir. Los bloques se ajustan al tamaño
    /// nuevo porque en un juego de supertiles cada uno es una celda del mapa. Y las celdas
    /// de los mapas ya dibujados no cambian de número pero sí de significado: donde decía
    /// «tile 7» pasa a decir «supertile 7», así que el mapa se ve distinto sin que nadie
    /// lo haya tocado.
    /// </remarks>
    public string SuperTileWarning
    {
        get
        {
            if (Document is not TileSetEditorViewModel tiles)
                return string.Empty;

            TileSet tileSet = tiles.TileSet;
            bool changes = UseSuperTiles != tileSet.HasSuperTiles
                || (UseSuperTiles
                    && (SuperTileWidth != tileSet.SuperTileWidth || SuperTileHeight != tileSet.SuperTileHeight));

            if (!changes)
                return string.Empty;

            var lines = new List<string>();

            if (UseSuperTiles && tileSet.Blocks.Count > 0)
            {
                lines.Add(tileSet.Blocks.Count == 1
                    ? $"Su bloque pasará a medir {SuperTileWidth}x{SuperTileHeight}; lo que sobre se recorta."
                    : $"Sus {tileSet.Blocks.Count} bloques pasarán a medir {SuperTileWidth}x{SuperTileHeight}; lo que sobre se recorta.");
            }

            int maps = _mainWindowVm.MapsOf(tiles).Count;

            if (maps > 0)
            {
                lines.Add(UseSuperTiles
                    ? $"Las celdas de {Written(maps)} pasan a leerse como números de supertile, así que se verán distintos."
                    : $"Las celdas de {Written(maps)} vuelven a leerse como números de tile, así que se verán distintos.");
            }

            return string.Join(" ", lines);
        }
    }

    public bool HasSuperTileWarning => SuperTileWarning.Length > 0;

    private static string Written(int maps) =>
        maps == 1 ? "el mapa que se dibuja con él" : $"los {maps} mapas que se dibujan con él";

    [RelayCommand]
    private void AcceptProperties()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = $"El nombre del {Kind} no puede estar vacío.";

            return;
        }

        if (UseSuperTiles && IsTileSet
            && (SuperTileWidth is < 1 || SuperTileHeight is < 1
                || SuperTileWidth > MaxSuperTileSide || SuperTileHeight > MaxSuperTileSide))
        {
            ErrorMessage = $"El supertile tiene que medir entre 1 y {MaxSuperTileSide} en cada lado.";

            return;
        }

        ErrorMessage = null;

        ApplySuperTiles();

        _mainWindowVm.Rename(Document, Name);
        _mainWindowVm.RightPanViewModel = null;
    }

    /// <summary>
    /// Lleva al juego lo que se haya elegido de supertiles.
    /// </summary>
    /// <remarks>
    /// Sólo si de verdad cambia: pasar por aquí para renombrar no tiene por qué ajustar
    /// bloques ni dejar el juego sin guardar por nada.
    /// </remarks>
    private void ApplySuperTiles()
    {
        if (Document is not TileSetEditorViewModel tiles)
            return;

        TileSet tileSet = tiles.TileSet;

        bool changes = UseSuperTiles != tileSet.HasSuperTiles
            || (UseSuperTiles
                && (SuperTileWidth != tileSet.SuperTileWidth || SuperTileHeight != tileSet.SuperTileHeight));

        if (!changes)
            return;

        if (UseSuperTiles)
            tileSet.UseSuperTiles(SuperTileWidth, SuperTileHeight);
        else
            tileSet.DropSuperTiles();

        tiles.Touch();

        // Los mapas que se dibujan con el tienen que enterarse: sus celdas cambian de
        // significado y de tamaño, y sus supertiles hay que componerlos.
        tiles.NotifyBlocksChanged();
    }

    [RelayCommand]
    private void CancelProperties() => _mainWindowVm.RightPanViewModel = null;
}
