namespace MSX_SpritesEditor.Entities;

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
}
