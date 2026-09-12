using System.Collections.ObjectModel;
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

        // Los ocho huecos siempre, definidos o no: la lista es de tamaño fijo porque un
        // atributo es un bit, y los ocho bits están ahí se usen o no. Dejar un nombre en
        // blanco es la forma de decir que ese bit no se usa.
        if (document is TileSetEditorViewModel editor)
        {
            for (int bit = 0; bit < TileAttributeNames.Count; bit++)
            {
                Attributes.Add(new TileAttributeRowViewModel(
                    bit, editor.TileSet.AttributeNames[bit], Watch));
            }
        }

        if (document is MapEditorViewModel map)
        {
            Bands = new MapBandsViewModel(mainWindowVm.TileSets, map.Map.Height)
            {
                TileSet = mainWindowVm.TileSetOf(map.Map.TileSets[0]),
            };

            // After the first one: setting the map's own set drags the other two along, which
            // is what is wanted when picking and not when opening the form.
            if (Bands.ShowsMiddle)
                Bands.Middle = BandOf(mainWindowVm, map, 1) ?? Bands.TileSet;

            if (Bands.ShowsBottom)
                Bands.Bottom = BandOf(mainWindowVm, map, 2) ?? Bands.TileSet;
        }

        Header = $"{Localizer.Instance["TreeProperties"]}: {document.DocumentName}";
        TagId = "properties";
    }

    /// <summary>El documento cuyos atributos se están tocando.</summary>
    public PanelBaseViewModel Document { get; }

    /// <summary>Los ocho atributos del juego de tiles, para ponerles nombre.</summary>
    public ObservableCollection<TileAttributeRowViewModel> Attributes { get; } = [];

    /// <summary>Qué es, para que se vea de qué se están viendo las propiedades.</summary>
    public string Kind => Document.DocumentKind;

    /// <summary>Dónde está guardado, o que todavía no lo está.</summary>
    public string Where => Document.FilePath ?? Localizer.Instance["PropsNotSaved"];

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
                1 => Localizer.Instance["PropsAffectedOne"],
                _ => Localizer.Instance.Format("PropsAffectedMany", maps),
            };
        }
    }

    public bool HasAffected => Affected.Length > 0;

    /// <summary>
    /// The tile set of each band, when what is being looked at is a map.
    /// </summary>
    /// <remarks>
    /// Changing it was not possible before, not even with a single tile set, and it is worth
    /// more than it looks: a map drawn with the wrong set, or one that outgrew the set it was
    /// started with, had no way back other than making it again.
    /// </remarks>
    public MapBandsViewModel? Bands { get; }

    /// <summary>The bands only show up when what is being looked at is a map.</summary>
    public bool IsMap => Document is MapEditorViewModel;

    /// <summary>The open panel of the tile set of a band of the map.</summary>
    private static TileSetEditorViewModel? BandOf(
        MainWindowViewModel main, MapEditorViewModel map, int band) =>
        main.TileSetOf(map.Map.TileSetFor(band * TileMap.RowsPerThird));

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
                    ? Localizer.Instance.Format("PropsBlocksOne", SuperTileWidth, SuperTileHeight)
                    : Localizer.Instance.Format(
                        "PropsBlocksMany", tileSet.Blocks.Count, SuperTileWidth, SuperTileHeight));
            }

            int maps = _mainWindowVm.MapsOf(tiles).Count;

            if (maps > 0)
            {
                lines.Add(Localizer.Instance.Format(
                    (UseSuperTiles ? "PropsSuperOn" : "PropsSuperOff") + (maps == 1 ? "One" : "Many"),
                    maps));
            }

            return string.Join(" ", lines);
        }
    }

    public bool HasSuperTileWarning => SuperTileWarning.Length > 0;

    [RelayCommand]
    private void AcceptProperties()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = Localizer.Instance.Format("PropsNoName", Kind);

            return;
        }

        if (UseSuperTiles && IsTileSet
            && (SuperTileWidth is < 1 || SuperTileHeight is < 1
                || SuperTileWidth > MaxSuperTileSide || SuperTileHeight > MaxSuperTileSide))
        {
            ErrorMessage = Localizer.Instance.Format("NewTileSetSuperRange", MaxSuperTileSide);

            return;
        }

        if (Bands?.Clash() is { } mixed)
        {
            ErrorMessage = Localizer.Instance.Format("MapBandPalette", mixed.TileSet.Name);

            return;
        }

        ErrorMessage = null;

        ApplyBands();
        ApplySuperTiles();
        ApplyAttributes();
        StopWatching();

        _mainWindowVm.Rename(Document, Name);
        _mainWindowVm.RightPanViewModel = null;
    }

    /// <summary>
    /// Takes the chosen tile sets to the map.
    /// </summary>
    /// <remarks>
    /// Only when they really change: coming through here to rename must not repaint the map nor
    /// leave it unsaved for nothing.
    /// </remarks>
    private void ApplyBands()
    {
        if (Document is not MapEditorViewModel map || Bands is null)
            return;

        IReadOnlyList<TileSetEditorViewModel> chosen = [.. Bands.Chosen()];

        if (chosen.Count == 0 || map.Map.TileSets.SequenceEqual(Bands.Refs()))
            return;

        map.UseTileSets(chosen);
    }

    /// <summary>
    /// Lleva al juego los nombres de los atributos.
    /// </summary>
    /// <remarks>
    /// Borrar un nombre deja ese bit sin definir y no toca lo marcado en los tiles: el bit
    /// se queda puesto donde estuviera, sólo deja de enseñarse. Volviendo a nombrarlo
    /// reaparece con lo que había. Borrar un rótulo no puede borrar el trabajo de marcar
    /// doscientos tiles.
    /// </remarks>
    private void ApplyAttributes()
    {
        if (Document is not TileSetEditorViewModel tiles)
            return;

        bool changed = false;

        foreach (TileAttributeRowViewModel row in Attributes)
        {
            if (tiles.TileSet.AttributeNames[row.Bit] == (row.Name?.Trim() ?? string.Empty))
                continue;

            tiles.TileSet.AttributeNames.Define(row.Bit, row.Name);
            changed = true;
        }

        if (!changed)
            return;

        tiles.Touch();
        tiles.RefreshAttributes();
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
    private void CancelProperties()
    {
        StopWatching();

        _mainWindowVm.RightPanViewModel = null;
    }

    /// <summary>
    /// Enciende el ojo de un atributo y apaga el que estuviera.
    /// </summary>
    /// <remarks>
    /// De uno en uno: con dos teñidos a la vez habría que distinguirlos por color, y
    /// entonces hay que aprenderse los colores. Lo que se quiere saber es «cuáles tienen
    /// éste».
    /// </remarks>
    private void Watch(int bit, bool on)
    {
        if (Document is not TileSetEditorViewModel tiles)
            return;

        if (on)
        {
            foreach (TileAttributeRowViewModel other in Attributes)
            {
                if (other.Bit != bit)
                    other.IsWatched = false;
            }
        }

        // Apagar el que ya no está encendido no puede apagar el que acaba de encenderse:
        // al cambiar de ojo llegan dos avisos y el de apagar puede llegar el último.
        if (on)
            tiles.HighlightedAttribute = bit;
        else if (tiles.HighlightedAttribute == bit)
            tiles.HighlightedAttribute = null;
    }

    /// <summary>
    /// Cerrar el panel se lleva el tinte.
    /// </summary>
    /// <remarks>
    /// Por los tres caminos, que son distintos: el aspa de la pestaña pasa por
    /// <see cref="OnClosed"/>, y Aceptar y Cancelar sólo dejan de enseñar el panel sin
    /// cerrarlo. Sin cubrir los tres, el tinte se quedaría puesto sin nadie a quien
    /// pedirle que se vaya.
    /// </remarks>
    public override void OnClosed() => StopWatching();

    private void StopWatching()
    {
        if (Document is TileSetEditorViewModel tiles)
            tiles.HighlightedAttribute = null;
    }
}
