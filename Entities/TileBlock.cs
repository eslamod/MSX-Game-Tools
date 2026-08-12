namespace MSX_GameTools.Entities;

/// <summary>
/// Un grupo de tiles colocados como van a quedar en el mapa: un árbol de 3x3, un
/// supertile de 2x2, o lo que se quiera colocar de una vez.
/// </summary>
/// <remarks>
/// <para>
/// La rejilla y la celda vacía son las de <see cref="TileGrid"/>, las mismas que usa una
/// capa de mapa. Aquí sólo viven las reglas propias del bloque: que crece al pintar más
/// allá y que tiene un tope de tamaño.
/// </para>
/// <para>
/// El tamaño se guarda, no se deduce de hasta dónde llega el último tile puesto:
/// deducirlo convertiría un supertile de 2x2 con la esquina de abajo vacía en 2x1.
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

    public TileBlock(string name = "") => Name = name;

    public string Name { get; set; }

    /// <summary>La rejilla con los tiles, que es la misma clase que usa una capa.</summary>
    public TileGrid Grid { get; } = new();

    /// <summary>Ancho en tiles. Al reducirlo se olvida lo que quede fuera.</summary>
    public int Width
    {
        get => Grid.Width;
        set => Grid.Resize(Math.Min(value, MaxSide), Grid.Height);
    }

    /// <summary>Alto en tiles. Al reducirlo se olvida lo que quede fuera.</summary>
    public int Height
    {
        get => Grid.Height;
        set => Grid.Resize(Grid.Width, Math.Min(value, MaxSide));
    }

    /// <summary>Si no hay ni un tile puesto. Un bloque vacío no se puede estampar.</summary>
    public bool IsEmpty => Grid.IsEmpty;

    /// <inheritdoc cref="TileGrid.this"/>
    public int? this[int column, int row]
    {
        get => Grid[column, row];
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
        {
            Grid.Resize(
                Math.Max(Grid.Width, column + 1),
                Math.Max(Grid.Height, row + 1));
        }
        else if (!Grid.Contains(column, row))
        {
            return false;
        }

        Grid[column, row] = tile;

        return true;
    }

    /// <inheritdoc cref="TileGrid.Stamp"/>
    public void Stamp(int column, int row, TilePatch patch)
    {
        for (int y = 0; y < patch.Height; y++)
        {
            for (int x = 0; x < patch.Width; x++)
                Set(column + x, row + y, patch[x, y]);
        }
    }

    public void Clear() => Grid.Clear();

    /// <inheritdoc cref="TileGrid.Contains"/>
    public bool Contains(int column, int row) => Grid.Contains(column, row);

    /// <inheritdoc cref="TileGrid.UsedSize"/>
    public (int Width, int Height) UsedSize() => Grid.UsedSize();

    /// <summary>Copia del contenido, para estamparlo en otro sitio.</summary>
    public TilePatch ToPatch() => Grid.ToPatch();
}

/// <summary>
/// Un trozo rectangular de tiles suelto: lo que se coge del juego, de un bloque o del
/// mapa para pegarlo en otro sitio.
/// </summary>
/// <remarks>
/// La misma forma que una rejilla pero sin nombre ni tope, porque no se guarda: es lo que
/// hay seleccionado ahora mismo. Un solo tile es un trozo de 1x1, así que estampar no
/// tiene dos caminos.
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
