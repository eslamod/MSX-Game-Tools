namespace MSX_GameTools.Entities;

public class SpriteRow
{
    public const int Columns = 16;

    public bool[] ArrayColumns { get; } = new bool[Columns];

    /// <summary>Índice en la <see cref="ColorPalette"/> del color de la fila.</summary>
    public int Color { get; set; }

    /// <summary>Takes the pixels and the colour of another line.</summary>
    public void CopyFrom(SpriteRow other)
    {
        for (int column = 0; column < Columns; column++)
            ArrayColumns[column] = other.ArrayColumns[column];

        Color = other.Color;
    }
}
