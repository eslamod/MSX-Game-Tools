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

        Apply();
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
