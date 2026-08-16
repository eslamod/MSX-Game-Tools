namespace MSX_GameTools.Entities;

public class Sprite
{
    public const int Rows = 16;

    public Sprite()
    {
        ArraySpriteRows = new SpriteRow[Rows];
        for (int i = 0; i < Rows; i++)
            ArraySpriteRows[i] = new SpriteRow { Color = EmptyColor };
    }

    public SpriteRow[] ArraySpriteRows { get; }

    public ImageMini? ImageMini { get; set; }

    /// <summary>Imagen de referencia que se ve detras del lienzo al editar este patron.</summary>
    public BackgroundRef Background { get; set; } = BackgroundRef.None;

    /// <summary>
    /// Un patron sin tocar: sin pixeles, con el color de partida y sin calco.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Con los 64 huecos siempre puestos, esto es lo que distingue un patron que se usa de
    /// uno que solo esta ahi. Decide que se guarda en el fichero y hasta donde llega la
    /// tabla que se exporta.
    /// </para>
    /// <para>
    /// Tambien cuenta la imagen de calco, y no es un adorno: alguien puede dejar puesta la
    /// referencia sobre la que va a dibujar y guardar antes de empezar. Sin mirarla, ese
    /// patron se daba por sin tocar y la referencia se perdia al guardar.
    /// </para>
    /// </remarks>
    public bool IsEmpty => !Background.HasValue
        && ArraySpriteRows.All(row => row.Color == EmptyColor && row.ArrayColumns.All(on => !on));

    /// <summary>El color con el que nace una linea, y al que vuelve al vaciarla.</summary>
    private const int EmptyColor = 15;

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

            row.Color = EmptyColor;
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
