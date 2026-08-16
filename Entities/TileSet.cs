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

    /// <summary>
    /// Lo más grande que puede ser un supertile por cada lado.
    /// </summary>
    /// <remarks>
    /// Ocho tiles son 64 pixeles, un cuarto de pantalla de ancho. Más grande que eso el
    /// mapa tendría tan pocas celdas que colocarlas deja de ser dibujar.
    /// </remarks>
    public const int MaxSuperTileSide = 8;

    private int _superTileWidth;

    private int _superTileHeight;

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
    /// Cambia de índice los colores de todos los tiles, para que sigan viéndose igual
    /// después de haber movido los colores de sitio en la paleta.
    /// </summary>
    /// <param name="table">Del índice de antes al de ahora, tal cual lo da <see cref="PaletteSwaps.Table"/>.</param>
    /// <remarks>
    /// Los bloques y los supertiles son números de tile, no de color: no se tocan.
    /// </remarks>
    public void RemapColors(IReadOnlyList<int> table)
    {
        foreach (Tile tile in ListOfTiles)
        {
            foreach (TileRow row in tile.ArrayTileRows)
            {
                row.ForeColor = table[row.ForeColor];
                row.BackColor = table[row.BackColor];
            }
        }
    }

    /// <summary>
    /// Los bloques definidos con estos tiles.
    /// </summary>
    /// <remarks>
    /// Cuelgan del juego y no de un sitio aparte porque un bloque son números de tile, y
    /// esos números no significan nada sin saber de qué juego son. Teniendo varios
    /// abiertos a la vez, un bloque suelto se vería como un churro con el juego que no es.
    /// </remarks>
    public IList<TileBlock> Blocks { get; } = new List<TileBlock>();

    /// <summary>
    /// Cómo se llama cada uno de los ocho atributos, si es que se usan.
    /// </summary>
    /// <remarks>
    /// Del juego y no del tile: el nombre del bit 2 es el mismo para los 256, y lo que
    /// cambia de un tile a otro es si lo tiene puesto. Empieza vacío, y mientras siga vacío
    /// no se enseña en ninguna parte: quien no los quiera no se entera de que existen.
    /// </remarks>
    public TileAttributeNames AttributeNames { get; } = new();

    /// <summary>
    /// Ancho del supertile de este juego en tiles, o 0 si no va de supertiles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vive aquí y no en el mapa a propósito. Un supertile es un bloque con el tamaño
    /// clavado, y los bloques cuelgan del juego porque son números de tile. Poniendo el
    /// tamaño en el mapa, dos mapas del mismo juego podrían pedir tamaños distintos y la
    /// regla «los bloques de este juego miden 2x2» no se podría cumplir para los dos.
    /// </para>
    /// <para>
    /// Así además la tabla de supertiles la comparten todos los mapas del juego, que es
    /// como se escribe en la máquina: una tabla y varios niveles que la indexan.
    /// </para>
    /// </remarks>
    public int SuperTileWidth
    {
        get => _superTileWidth;
        set => _superTileWidth = ClampSide(value);
    }

    /// <inheritdoc cref="SuperTileWidth"/>
    public int SuperTileHeight
    {
        get => _superTileHeight;
        set => _superTileHeight = ClampSide(value);
    }

    /// <summary>Si este juego se dibuja con supertiles en vez de con tiles sueltos.</summary>
    public bool HasSuperTiles => SuperTileWidth > 0 && SuperTileHeight > 0;

    /// <summary>Tiles que ocupa un supertile, que es lo que mide una celda del mapa.</summary>
    public int SuperTileArea => SuperTileWidth * SuperTileHeight;

    /// <summary>
    /// Pone el tamaño del supertile y deja los bloques que ya hubiera midiendo eso.
    /// </summary>
    /// <remarks>
    /// Los bloques se ajustan porque en un juego de supertiles cada uno es una celda del
    /// mapa: dejar uno de 3x1 entre supertiles de 2x2 sería dejar algo que no se puede
    /// colocar en ninguna parte. Lo que sobre se recorta, que es lo que hace la rejilla al
    /// encogerse, y lo que falte queda vacío.
    /// </remarks>
    public void UseSuperTiles(int width, int height)
    {
        SuperTileWidth = width;
        SuperTileHeight = height;

        if (!HasSuperTiles)
            return;

        foreach (TileBlock block in Blocks)
        {
            block.Width = SuperTileWidth;
            block.Height = SuperTileHeight;
        }
    }

    /// <summary>Deja de ir por supertiles. Los bloques se quedan como estén.</summary>
    public void DropSuperTiles()
    {
        SuperTileWidth = 0;
        SuperTileHeight = 0;
    }

    private static int ClampSide(int value) =>
        value <= 0 ? 0 : Math.Min(value, MaxSuperTileSide);

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
