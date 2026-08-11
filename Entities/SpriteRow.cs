namespace MSX_GameTools.Entities;

public class SpriteRow
{
    public const int Columns = 16;

    public bool[] ArrayColumns { get; } = new bool[Columns];

    /// <summary>Índice en la <see cref="ColorPalette"/> del color de la fila.</summary>
    public int Color { get; set; }
}
