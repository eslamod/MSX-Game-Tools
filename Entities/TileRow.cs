namespace MSX_SpritesEditor.Entities;

public class TileRow
{
    public const int Columns = 8;

    public bool[] ArrayPattern { get; } = new bool[Columns];

    public int BackColor { get; set; }

    public int ForeColor { get; set; }
}
