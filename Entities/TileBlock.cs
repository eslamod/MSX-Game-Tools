namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Un grupo de tiles colocados como van a quedar en el mapa: un árbol de 3x3, un
/// supertile de 2x2, o lo que se quiera colocar de una vez.
/// </summary>
/// <remarks>
/// <para>
/// Una celda vacía <b>no es el tile 0</b>. El 0 es un tile de verdad, así que hace falta
/// distinguir "aquí no va nada" de "aquí va el primero del juego": al estampar el bloque
/// en el mapa, las celdas vacías dejan lo que hubiera debajo. Por eso los índices son
/// <see cref="Nullable{T}"/> y no un número reservado.
/// </para>
/// <para>
/// El tamaño se guarda, no se deduce de hasta dónde llegan los tiles puestos. Crece solo
/// al pintar más allá, que es lo cómodo, pero se puede fijar: un supertile de 2x2 con la
/// esquina de abajo vacía sigue siendo de 2x2, y deducirlo lo dejaría en 2x1.
/// </para>
/// </remarks>
public class TileBlock
{
    /// <summary>
    /// Lo más grande que puede ser un bloque, de ancho y de alto.
    /// </summary>
    /// <remarks>
    /// El límite es el panel, que comparte sitio con los demás. Un bloque más grande que
    /// esto ya es un mapa.
    /// </remarks>
    public const int MaxSide = 16;

    private readonly int?[] _tiles = new int?[MaxSide * MaxSide];

    private int _width = 1;
    private int _height = 1;

    public TileBlock(string name = "") => Name = name;

    public string Name { get; set; }

    /// <summary>Ancho en tiles. Al reducirlo se olvida lo que quede fuera.</summary>
    public int Width
    {
        get => _width;
        set => Resize(value, _height);
    }

    /// <summary>Alto en tiles. Al reducirlo se olvida lo que quede fuera.</summary>
    public int Height
    {
        get => _height;
        set => Resize(_width, value);
    }

    /// <summary>Si no hay ni un tile puesto. Un bloque vacío no se puede estampar.</summary>
    public bool IsEmpty
    {
        get
        {
            for (int index = 0; index < _tiles.Length; index++)
            {
                if (_tiles[index] is not null)
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// El tile de una celda, o <c>null</c> si está vacía. Fuera del bloque es siempre
    /// <c>null</c>: preguntar por una celda que no existe no es un error, no hay nada.
    /// </summary>
    public int? this[int column, int row]
    {
        get => Contains(column, row) ? _tiles[Offset(column, row)] : null;
        set => Set(column, row, value);
    }

    /// <summary>
    /// Pone un tile, y si cae fuera estira el bloque para que quepa.
    /// </summary>
    /// <remarks>
    /// Sólo crece al poner algo. Borrar en el borde no encoge el bloque: el tamaño es una
    /// decisión y no la consecuencia de lo último que se haya hecho.
    /// </remarks>
    /// <returns><c>false</c> si la celda cae fuera del máximo y no se ha tocado nada.</returns>
    public bool Set(int column, int row, int? tile)
    {
        if (column < 0 || row < 0 || column >= MaxSide || row >= MaxSide)
            return false;

        if (tile is not null)
            Resize(Math.Max(_width, column + 1), Math.Max(_height, row + 1));
        else if (!Contains(column, row))
            return false;

        _tiles[Offset(column, row)] = tile;

        return true;
    }

    /// <summary>Estampa otra rejilla con su esquina en (column, row).</summary>
    /// <remarks>
    /// Es lo que hace falta para pegar una selección de varios tiles de una vez, y es lo
    /// mismo que hará el mapa con un bloque entero.
    /// </remarks>
    public void Stamp(int column, int row, TilePatch patch)
    {
        for (int y = 0; y < patch.Height; y++)
        {
            for (int x = 0; x < patch.Width; x++)
                Set(column + x, row + y, patch[x, y]);
        }
    }

    /// <summary>Deja el bloque sin ningún tile, conservando su tamaño.</summary>
    public void Clear() => Array.Clear(_tiles);

    /// <summary>Si esa celda cae dentro del tamaño actual.</summary>
    public bool Contains(int column, int row) =>
        column >= 0 && row >= 0 && column < _width && row < _height;

    /// <summary>
    /// Lo que ocupa de verdad lo dibujado, que puede ser menos que el tamaño.
    /// </summary>
    /// <remarks>
    /// No se usa para guardar —el tamaño manda— pero sirve para ofrecer un "ajustar al
    /// contenido" y para saber si sobra sitio.
    /// </remarks>
    public (int Width, int Height) UsedSize()
    {
        int width = 0;
        int height = 0;

        for (int row = 0; row < _height; row++)
        {
            for (int column = 0; column < _width; column++)
            {
                if (_tiles[Offset(column, row)] is null)
                    continue;

                width = Math.Max(width, column + 1);
                height = Math.Max(height, row + 1);
            }
        }

        return (width, height);
    }

    /// <summary>Copia del contenido, para estamparlo en otro sitio.</summary>
    public TilePatch ToPatch()
    {
        var patch = new TilePatch(_width, _height);

        for (int row = 0; row < _height; row++)
        {
            for (int column = 0; column < _width; column++)
                patch[column, row] = _tiles[Offset(column, row)];
        }

        return patch;
    }

    private void Resize(int width, int height)
    {
        width = Math.Clamp(width, 1, MaxSide);
        height = Math.Clamp(height, 1, MaxSide);

        if (width == _width && height == _height)
            return;

        // Lo que se queda fuera se olvida: si el bloque vuelve a crecer, la celda sale
        // vacía y no con lo que hubiera antes, que es lo que uno espera al verla vacía.
        for (int row = 0; row < MaxSide; row++)
        {
            for (int column = 0; column < MaxSide; column++)
            {
                if (column >= width || row >= height)
                    _tiles[Offset(column, row)] = null;
            }
        }

        _width = width;
        _height = height;
    }

    private static int Offset(int column, int row) => (row * MaxSide) + column;
}

/// <summary>
/// Un trozo rectangular de tiles suelto: lo que se coge del juego o de un bloque para
/// pegarlo en otro sitio.
/// </summary>
/// <remarks>
/// La misma forma que un bloque pero sin nombre ni límite, porque no se guarda: es lo
/// que hay seleccionado ahora mismo. Un solo tile es un trozo de 1x1, así que estampar
/// no tiene dos caminos.
/// </remarks>
public sealed class TilePatch
{
    private readonly int?[] _tiles;

    public TilePatch(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _tiles = new int?[Width * Height];
    }

    public int Width { get; }

    public int Height { get; }

    public int?[] Tiles => _tiles;

    public int? this[int column, int row]
    {
        get => _tiles[(row * Width) + column];
        set => _tiles[(row * Width) + column] = value;
    }

    /// <summary>Un solo tile, que es lo que se coge al pulsar en una miniatura.</summary>
    public static TilePatch Single(int tile) => new(1, 1) { [0, 0] = tile };
}
