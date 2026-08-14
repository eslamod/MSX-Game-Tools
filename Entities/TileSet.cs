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

    /// <summary>
    /// Tiles por fila en la rejilla con la que se enseña el juego.
    /// </summary>
    /// <remarks>
    /// Aquí y no sólo en la vista porque copiar y estampar van por rectángulos de esa
    /// rejilla: sin saber cuántos hay por fila, un rectángulo no significa nada. Son 32
    /// también en el editor de mapas y en el png que se importa y se exporta, así que el
    /// dibujo del juego es el mismo se mire por donde se mire.
    /// </remarks>
    public const int Columns = 32;

    /// <summary>Filas que salen de los 256 tiles repartidos de 32 en 32.</summary>
    public const int GridRows = TileCount / Columns;

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

    /// <summary>Copia los dibujos de un rectángulo de la rejilla.</summary>
    public TileSetPatch Copy(int left, int top, int width, int height)
    {
        (left, top, width, height) = Clip(left, top, width, height);

        var tiles = new List<Tile>(width * height);

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
                tiles.Add(ListOfTiles[((top + row) * Columns) + left + column].Copy());
        }

        return new TileSetPatch(width, height, tiles);
    }

    /// <summary>
    /// Estampa un trozo con su esquina en esa celda y devuelve lo que había.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sobrescribe entero: aquí no hay huecos que respetar como en el mapa, porque los 256
    /// tiles existen siempre y todos dibujan algo.
    /// </para>
    /// <para>
    /// Lo que devuelve es el mismo rectángulo tal como estaba, que es lo que hace falta
    /// para poder deshacerlo. Estampar machaca dibujos que pueden ser de hace horas, así
    /// que quien llama tiene que quedárselo.
    /// </para>
    /// </remarks>
    /// <returns>El contenido anterior, para devolverlo con otro <see cref="Stamp"/>.</returns>
    public TileSetPatch Stamp(int left, int top, TileSetPatch patch)
    {
        (left, top, int width, int height) = Clip(left, top, patch.Width, patch.Height);

        TileSetPatch before = Copy(left, top, width, height);

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
                ListOfTiles[((top + row) * Columns) + left + column].CopyFrom(patch[column, row]);
        }

        return before;
    }

    /// <summary>
    /// Recorta un rectángulo a lo que cabe en la rejilla.
    /// </summary>
    /// <remarks>
    /// Se recorta y no se da la vuelta a la fila siguiente: soltar un trozo pegado al borde
    /// derecho tiene que dejar lo que quepa, no aparecer por la izquierda de la fila de
    /// abajo, que es donde no se está mirando.
    /// </remarks>
    private static (int Left, int Top, int Width, int Height) Clip(
        int left, int top, int width, int height)
    {
        left = Math.Clamp(left, 0, Columns - 1);
        top = Math.Clamp(top, 0, GridRows - 1);

        return (left, top, Math.Clamp(width, 1, Columns - left), Math.Clamp(height, 1, GridRows - top));
    }
}
