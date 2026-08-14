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
    /// Se queda con el dibujo y los colores de otro tile.
    /// </summary>
    /// <remarks>
    /// La miniatura no se copia: es del hueco, no del dibujo. Cada uno de los 256 tiene la
    /// suya desde que se crea el juego y la vista la tiene cogida, así que cambiarla por la
    /// de otro dejaría dos huecos enseñando la misma imagen.
    /// </remarks>
    public void CopyFrom(Tile other)
    {
        for (int row = 0; row < Rows; row++)
            ArrayTileRows[row].CopyFrom(other.ArrayTileRows[row]);
    }

    /// <summary>Una copia suelta del dibujo, para poder guardarlo y devolverlo.</summary>
    public Tile Copy()
    {
        var copy = new Tile();

        copy.CopyFrom(this);

        return copy;
    }
}
