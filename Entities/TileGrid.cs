namespace MSX_GameTools.Entities;

/// <summary>
/// Una rejilla rectangular de números de tile: lo que hay debajo tanto de un bloque como
/// de una capa de mapa.
/// </summary>
/// <remarks>
/// <para>
/// La celda vacía es <c>null</c> y no el tile 0, que es un tile de verdad. Al estampar,
/// una celda vacía deja ver lo que hubiera debajo; en un bloque, el mapa, y en una capa,
/// la capa de abajo o el color de fondo.
/// </para>
/// <para>
/// Es la misma estructura para las dos cosas a propósito: un bloque es una tabla de
/// nombres pequeña y un mapa la misma tabla más grande, así que estampar un bloque en el
/// mapa no necesita ninguna traducción por el medio. Lo que cambia entre uno y otro son
/// las reglas de arriba —el bloque crece al pintar fuera y tiene tope, el mapa no—, y por
/// eso viven en <see cref="TileBlock"/> y no aquí.
/// </para>
/// </remarks>
public class TileGrid
{
    private int?[] _tiles;

    public TileGrid(int width = 1, int height = 1)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _tiles = new int?[Width * Height];
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>Si no hay ni un tile puesto.</summary>
    public bool IsEmpty => _tiles.All(tile => tile is null);

    /// <summary>
    /// El tile de una celda, o <c>null</c> si está vacía. Fuera de la rejilla es siempre
    /// <c>null</c>: preguntar por una celda que no existe no es un error, no hay nada.
    /// </summary>
    public int? this[int column, int row]
    {
        get => Contains(column, row) ? _tiles[(row * Width) + column] : null;
        set
        {
            if (Contains(column, row))
                _tiles[(row * Width) + column] = value;
        }
    }

    public bool Contains(int column, int row) =>
        column >= 0 && row >= 0 && column < Width && row < Height;

    /// <summary>
    /// Cambia el tamaño conservando lo que siga cayendo dentro.
    /// </summary>
    /// <remarks>
    /// Lo que queda fuera se olvida, no se guarda por si acaso: si la rejilla vuelve a
    /// crecer, esa celda sale vacía, que es lo que uno espera al verla vacía.
    /// </remarks>
    public void Resize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        if (width == Width && height == Height)
            return;

        int?[] grown = new int?[width * height];

        for (int row = 0; row < Math.Min(height, Height); row++)
        {
            for (int column = 0; column < Math.Min(width, Width); column++)
                grown[(row * width) + column] = _tiles[(row * Width) + column];
        }

        _tiles = grown;
        Width = width;
        Height = height;
    }

    /// <summary>Estampa otra rejilla con su esquina en (column, row).</summary>
    public void Stamp(int column, int row, TilePatch patch)
    {
        for (int y = 0; y < patch.Height; y++)
        {
            for (int x = 0; x < patch.Width; x++)
                this[column + x, row + y] = patch[x, y];
        }
    }

    /// <summary>Deja la rejilla sin ningún tile, conservando su tamaño.</summary>
    public void Clear() => Array.Clear(_tiles);

    /// <summary>Lo que ocupa de verdad lo dibujado, que puede ser menos que el tamaño.</summary>
    public (int Width, int Height) UsedSize()
    {
        int width = 0;
        int height = 0;

        for (int row = 0; row < Height; row++)
        {
            for (int column = 0; column < Width; column++)
            {
                if (this[column, row] is null)
                    continue;

                width = Math.Max(width, column + 1);
                height = Math.Max(height, row + 1);
            }
        }

        return (width, height);
    }

    /// <summary>Copia de un rectángulo, para estamparlo en otro sitio.</summary>
    public TilePatch ToPatch(int left = 0, int top = 0, int width = 0, int height = 0)
    {
        var patch = new TilePatch(width > 0 ? width : Width, height > 0 ? height : Height);

        for (int row = 0; row < patch.Height; row++)
        {
            for (int column = 0; column < patch.Width; column++)
                patch[column, row] = this[left + column, top + row];
        }

        return patch;
    }
}
