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
}
