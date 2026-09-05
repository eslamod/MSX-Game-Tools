using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.Entities;

/// <summary>
/// El par de colores que comparten ocho tiles seguidos en GRAPHIC 1.
/// </summary>
/// <remarks>
/// <para>
/// En screen 1 la tabla de colores tiene 32 bytes para los 256 patrones: un byte por cada
/// ocho, con el color de frente en el nibble alto y el de fondo en el bajo. O sea que el
/// color no es del tile, es del grupo en el que le haya tocado caer.
/// </para>
/// <para>
/// Eso convierte el hueco en el que pones un tile en una decisión de dibujo y no sólo de
/// numeración: mover un tile de sitio le cambia el color. Es la limitación que define el
/// modo, y el editor la enseña en vez de esconderla.
/// </para>
/// </remarks>
public partial class TileColorGroup : ObservableObject
{
    /// <summary>Los ocho tiles que pinta este par, para poder mantenerlos al día.</summary>
    private readonly IReadOnlyList<Tile> _tiles;

    /// <summary>Mientras se cambian los dos colores a la vez, no se baja nada a los tiles.</summary>
    private bool _quiet;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorByte))]
    private int _foreColor = 15;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorByte))]
    private int _backColor;

    public TileColorGroup(int index, IReadOnlyList<Tile> tiles)
    {
        Index = index;
        _tiles = tiles;
    }

    /// <summary>Se avisa cuando el par cambia, porque cambian ocho tiles de golpe.</summary>
    public event Action<TileColorGroup>? Changed;

    /// <summary>Cuál de los 32 grupos es.</summary>
    public int Index { get; }

    /// <summary>Los ocho tiles que pinta.</summary>
    public IReadOnlyList<Tile> Tiles => _tiles;

    /// <summary>El par con el que nace un grupo, el mismo con el que nace una línea.</summary>
    private const int DefaultFore = 15;

    private const int DefaultBack = 0;

    /// <summary>
    /// Si a este grupo no lo ha tocado nadie: ni dibujo en sus ocho tiles, ni par elegido.
    /// </summary>
    /// <remarks>
    /// Las dos cosas y no sólo el dibujo. Mirar sólo el dibujo daba por sin estrenar un grupo
    /// al que le acababas de elegir el color a mano y todavía no habías dibujado, que es
    /// justo el orden natural de trabajo: primero eliges los dos colores de la franja y
    /// después traes los dibujos. Con eso, lo que llegaba pisaba una decisión deliberada.
    /// </remarks>
    public bool IsUntouched =>
        ForeColor == DefaultFore
        && BackColor == DefaultBack
        && _tiles.All(tile => tile.ArrayTileRows.All(row => row.PatternByte == 0));

    /// <summary>
    /// Se queda con el par que más se repite entre unas líneas que vienen de fuera.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Para cuando cae aquí un trozo de un juego de GRAPHIC 2, donde cada línea trae su par.
    /// El que más se repite y no el de la primera línea: un dibujo suele tener un par
    /// dominante y alguna línea suelta con otro, y quedarse con el de la primera línea daría
    /// el color de una esquina.
    /// </para>
    /// <para>
    /// Sólo votan las líneas que encienden algún pixel. Una línea a cero no enseña su color de
    /// frente —ese color no se ve y puede ser cualquiera—, y dejarla votar hacía que el cielo
    /// vacío de encima de un dibujo, que suele ir fondo sobre fondo, le ganara por mayoría a
    /// las pocas líneas que pintan de verdad. El trozo llegaba con el frente igual que el
    /// fondo y se veía en blanco, aunque los bits estuvieran todos puestos.
    /// </para>
    /// </remarks>
    public void AdoptFrom(IEnumerable<TileRow> rows)
    {
        TileRow[] all = [.. rows];
        TileRow[] drawn = [.. all.Where(row => row.PatternByte != 0)];

        // Si no dibuja nada ninguna, votan todas: lo que llega es fondo liso y el par que
        // salga da igual mientras el fondo sea el bueno.
        Vote(drawn.Length > 0 ? drawn : all);
    }

    private void Vote(IReadOnlyList<TileRow> rows)
    {
        var seen = new List<(int Fore, int Back, int Times)>();

        foreach (TileRow row in rows)
        {
            int at = seen.FindIndex(pair => pair.Fore == row.ForeColor && pair.Back == row.BackColor);

            if (at < 0)
                seen.Add((row.ForeColor, row.BackColor, 1));
            else
                seen[at] = seen[at] with { Times = seen[at].Times + 1 };
        }

        if (seen.Count == 0)
            return;

        // Al empatar gana el que se vio antes, que es el de más arriba y más a la izquierda.
        (int fore, int back, _) = seen.OrderByDescending(pair => pair.Times).First();

        Set(fore, back);
    }

    public int FirstTile => Index * TileSet.ColorGroupSize;

    public int LastTile => FirstTile + TileSet.ColorGroupSize - 1;

    /// <summary>A qué tiles pinta, que es lo que se lee al lado del par: «0-7», «8-15»…</summary>
    public string Range => $"{FirstTile}-{LastTile}";

    /// <summary>El byte de la tabla de colores: el de frente arriba y el de fondo abajo.</summary>
    public byte ColorByte => (byte)(((ForeColor & 0x0F) << 4) | (BackColor & 0x0F));

    /// <summary>
    /// Pone los dos colores de una vez.
    /// </summary>
    /// <remarks>
    /// Cambiarlos por separado bajaría el par a los ocho tiles dos veces, con el estado a
    /// medias en la primera: un momento con el frente nuevo y el fondo viejo, que es un par
    /// que nadie ha pedido y que la vista llega a pintar.
    /// </remarks>
    public void Set(int foreColor, int backColor)
    {
        if (ForeColor == foreColor && BackColor == backColor)
            return;

        Remap(foreColor, backColor);

        Apply();
    }

    /// <summary>
    /// Mueve el par a otros índices sin bajarlo a los tiles.
    /// </summary>
    /// <remarks>
    /// Para cuando los colores se mueven de sitio en la paleta y este grupo no manda: en
    /// GRAPHIC 2 el color lo lleva cada línea y se reajusta línea a línea, así que bajar el
    /// par aquí borraría los ocho tiles enteros y los dejaría de un color liso.
    /// </remarks>
    public void Remap(int foreColor, int backColor)
    {
        _quiet = true;

        try
        {
            ForeColor = foreColor;
            BackColor = backColor;
        }
        finally
        {
            _quiet = false;
        }
    }

    /// <summary>
    /// Vuelve a bajar el par a sus tiles sin haberlo cambiado.
    /// </summary>
    /// <remarks>
    /// Hace falta cuando algo escribe en las líneas por detrás —estampar un trozo traído de
    /// otro juego, por ejemplo—: el dibujo es lo que se lleva, pero el color de aquí manda.
    /// </remarks>
    public void Repaint() => Apply();

    partial void OnForeColorChanged(int value) => Apply();

    partial void OnBackColorChanged(int value) => Apply();

    /// <summary>
    /// Baja el par a las líneas de sus ocho tiles.
    /// </summary>
    /// <remarks>
    /// Los colores por línea siguen ahí y siguen siendo lo que se pinta y lo que se exporta;
    /// en este modo lo que cambia es quién los escribe. Manteniéndolos al día, todo lo que ya
    /// sabía dibujar un tile —el lienzo, las miniaturas, el mapa, los bloques, el png— sigue
    /// funcionando sin enterarse de que existe el screen 1.
    /// </remarks>
    private void Apply()
    {
        if (_quiet)
            return;

        foreach (Tile tile in _tiles)
        {
            foreach (TileRow row in tile.ArrayTileRows)
            {
                row.ForeColor = ForeColor;
                row.BackColor = BackColor;
            }
        }

        Changed?.Invoke(this);
    }
}
