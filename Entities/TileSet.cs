namespace MSX_GameTools.Entities;

/// <summary>
/// Un juego de patrones de 8x8 para los modos gráficos 2 y 3.
/// </summary>
/// <remarks>
/// <para>
/// Los 256 tiles existen desde el principio, vacíos. No se añaden ni se quitan: en el
/// VDP la tabla es de tamaño fijo y un tile se referencia por su número, así que un mapa
/// puede guardar un byte y ya está. Hacerlos aparecer y desaparecer sólo serviría para
/// que los números bailaran.
/// </para>
/// <para>
/// En pantalla el VDP parte la imagen en tres tercios, cada uno con su tabla de patrones
/// y de colores, o sea 768 huecos. Aquí se define un solo juego de 256 porque la
/// exportación lo replica en los tres: así un tile se ve igual lo pongas donde lo pongas,
/// que es lo que hace falta para poder dibujar mapas sin llevar cuentas.
/// </para>
/// </remarks>
public class TileSet
{
    /// <summary>Tiles de un juego. Es el tamaño de la tabla en el VDP.</summary>
    public const int TileCount = 256;

    public TileSet(string name = "")
    {
        Name = name;

        var tiles = new Tile[TileCount];

        for (int index = 0; index < TileCount; index++)
            tiles[index] = new Tile { ImageMini = new ImageMini(TileRow.Columns, Tile.Rows) };

        ListOfTiles = tiles;
    }

    /// <summary>
    /// Quién es este juego, para que otros lo señalen.
    /// </summary>
    /// <remarks>
    /// Un mapa tiene que decir con qué juego se dibuja, y decirlo por el nombre ataba las
    /// dos cosas: renombrar un juego dejaba a sus mapas sin encontrarlo, y dos juegos
    /// llamados igual hacían la búsqueda ambigua. Con esto el nombre vuelve a ser sólo una
    /// etiqueta que se puede cambiar cuando se quiera.
    /// </remarks>
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; }

    public IReadOnlyList<Tile> ListOfTiles { get; }

    /// <summary>
    /// Los bloques definidos con estos tiles.
    /// </summary>
    /// <remarks>
    /// Cuelgan del juego y no de un sitio aparte porque un bloque son números de tile, y
    /// esos números no significan nada sin saber de qué juego son. Teniendo varios
    /// abiertos a la vez, un bloque suelto se vería como un churro con el juego que no es.
    /// </remarks>
    public IList<TileBlock> Blocks { get; } = new List<TileBlock>();
}
