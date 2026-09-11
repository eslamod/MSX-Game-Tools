namespace MSX_GameTools.Entities;

/// <summary>Un patrón de 8x8 de los modos gráficos 2 y 3.</summary>
public class Tile
{
    public const int Rows = 8;

    public Tile()
    {
        ArrayTileRows = new TileRow[Rows];
        for (int i = 0; i < Rows; i++)
            ArrayTileRows[i] = new TileRow();
    }

    public TileRow[] ArrayTileRows { get; }

    /// <summary>Miniatura de 8x8 para el panel de la derecha.</summary>
    public ImageMini? ImageMini { get; set; }

    /// <summary>
    /// Las ocho banderas de este tile, una por bit.
    /// </summary>
    /// <remarks>
    /// Cómo se llama cada bit lo lleva el juego de tiles, no el tile: el nombre es del
    /// atributo y es el mismo para los 256. Aquí sólo está qué tiene puesto éste. Se guarda
    /// en un <c>int</c> y no en un <c>byte</c> para que subir de ocho a dieciséis algún día
    /// sea la constante y la exportación, y no tocar el tipo de los 256 tiles.
    /// </remarks>
    public int Attributes { get; set; }

    public bool Has(int bit) => Inside(bit) && (Attributes & (1 << bit)) != 0;

    public void SetAttribute(int bit, bool on)
    {
        if (!Inside(bit))
            return;

        Attributes = on ? Attributes | (1 << bit) : Attributes & ~(1 << bit);
    }

    /// <summary>
    /// Se queda con el dibujo, los colores y los atributos de otro tile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los atributos van con el dibujo a propósito: si un tile es sólido, la copia que se
    /// estampa en otro hueco también lo es. Copiar el dibujo y dejarse las banderas daría
    /// dos tiles iguales que se comportan distinto, y eso sólo se descubre jugando.
    /// </para>
    /// <para>
    /// La miniatura no se copia: es del hueco, no del dibujo. Cada uno de los 256 tiene la
    /// suya desde que se crea el juego y la vista la tiene cogida, así que cambiarla por la
    /// de otro dejaría dos huecos enseñando la misma imagen.
    /// </para>
    /// </remarks>
    public void CopyFrom(Tile other)
    {
        for (int row = 0; row < Rows; row++)
            ArrayTileRows[row].CopyFrom(other.ArrayTileRows[row]);

        Attributes = other.Attributes;
    }

    private static bool Inside(int bit) => (uint)bit < TileAttributeNames.Count;

    /// <summary>Una copia suelta del dibujo, para poder guardarlo y devolverlo.</summary>
    public Tile Copy()
    {
        var copy = new Tile();

        copy.CopyFrom(this);

        return copy;
    }

    /// <summary>Mirrors the drawing sideways, leaving every line where it is.</summary>
    /// <remarks>
    /// The colours stay put because they have no reason to move: each line keeps the two it
    /// had, and all that changes is the order of the pixels inside it.
    /// </remarks>
    public void FlipHorizontal()
    {
        foreach (TileRow row in ArrayTileRows)
            PatternMoves.Mirror(row.ArrayPattern);
    }

    /// <summary>Turns the drawing upside down, colours included.</summary>
    /// <remarks>
    /// The two colours of a line belong to that line, so they travel with it. Leaving them
    /// behind would flip the shape and not the drawing: a figure with a red top and a blue
    /// bottom would come back with the red still on top.
    /// </remarks>
    public void FlipVertical() => Reorder(line => Rows - 1 - line);

    /// <summary>
    /// Turns the drawing a quarter, one way or the other.
    /// </summary>
    /// <remarks>
    /// The pixels turn and the colours do not: they are written one pair per line, so turning
    /// them would mean ending up down a column, which is not something the tables of GRAPHIC 2
    /// can say. A tile drawn in one colour turns exactly; one with colour bands comes out
    /// turned with its bands still lying flat, which is all the VDP would paint anyway.
    /// </remarks>
    public void Turn(bool clockwise) =>
        PatternMoves.Turn([.. ArrayTileRows.Select(row => row.ArrayPattern)], clockwise);

    /// <summary>
    /// Moves the whole drawing one pixel, wrapping around the edges.
    /// </summary>
    /// <remarks>
    /// Sideways nothing leaves its line, so the colours stay where they are; up and down it is
    /// the lines themselves that move, and their colours go along, the same as flipping.
    /// </remarks>
    public void Shift(int dx, int dy)
    {
        foreach (TileRow row in ArrayTileRows)
            PatternMoves.Shift(row.ArrayPattern, dx);

        if (dy != 0)
            Reorder(line => PatternMoves.LineFrom(line, dy, Rows));
    }

    /// <summary>Rebuilds every line from the one <paramref name="source"/> points at.</summary>
    /// <remarks>
    /// Through a copy of the whole tile: the lines take from each other, so writing straight
    /// over them would have the later ones reading what the earlier ones have already changed.
    /// </remarks>
    private void Reorder(Func<int, int> source)
    {
        Tile before = Copy();

        for (int line = 0; line < Rows; line++)
            ArrayTileRows[line].CopyFrom(before.ArrayTileRows[source(line)]);
    }
}
