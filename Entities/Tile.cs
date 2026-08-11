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
}
