namespace MSX_GameTools.Entities;

public class Sprite
{
    public const int Rows = 16;

    public Sprite()
    {
        ArraySpriteRows = new SpriteRow[Rows];
        for (int i = 0; i < Rows; i++)
            ArraySpriteRows[i] = new SpriteRow { Color = 15 };
    }

    public SpriteRow[] ArraySpriteRows { get; }

    public ImageMini? ImageMini { get; set; }

    /// <summary>Imagen de referencia que se ve detras del lienzo al editar este patron.</summary>
    public BackgroundRef Background { get; set; } = BackgroundRef.None;

    /// <summary>
    /// Deja el patron como recien creado, sin borrarlo del banco.
    /// </summary>
    /// <remarks>
    /// Es lo que sustituye a eliminar en medio del banco. Los patrones se referencian por
    /// indice -los grupos, y sobre todo el codigo del juego-, asi que quitar uno de en
    /// medio corre todos los de atras y descoloca lo que ya estuviera hecho. Vaciandolo, el
    /// hueco sigue ahi y ningun numero se mueve.
    /// </remarks>
    public void Clear()
    {
        foreach (SpriteRow row in ArraySpriteRows)
        {
            Array.Clear(row.ArrayColumns);

            row.Color = 15;
        }

        Background = BackgroundRef.None;
    }

    /// <summary>
    /// Se queda con el dibujo, los colores de linea y el fondo de calco de otro patron.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El fondo va con la copia a proposito: se duplica un patron para hacerle una
    /// variacion, y la variacion se calca sobre la misma referencia que el original.
    /// </para>
    /// <para>
    /// La miniatura no, igual que en los tiles: es del hueco y no del dibujo. Cada patron
    /// tiene la suya desde que se crea y la vista la tiene cogida, asi que cambiarla por la
    /// de otro dejaria dos huecos enseñando la misma imagen.
    /// </para>
    /// </remarks>
    public void CopyFrom(Sprite other)
    {
        for (int row = 0; row < Rows; row++)
        {
            SpriteRow from = other.ArraySpriteRows[row];
            SpriteRow into = ArraySpriteRows[row];

            for (int column = 0; column < SpriteRow.Columns; column++)
                into.ArrayColumns[column] = from.ArrayColumns[column];

            into.Color = from.Color;
        }

        Background = other.Background;
    }
}
