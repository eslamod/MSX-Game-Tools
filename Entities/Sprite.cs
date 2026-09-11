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
    /// <summary>Una copia suelta del patron, para poder llevarselo.</summary>
    /// <remarks>
    /// Suelta del banco: no ocupa hueco ni tiene miniatura. Es lo que se guarda al copiar,
    /// para que retocar el original despues no cambie lo que se va a pegar.
    /// </remarks>
    public Sprite Copy()
    {
        var copy = new Sprite();

        copy.CopyFrom(this);

        return copy;
    }

    /// <summary>Si las lineas no van todas del mismo color, que es lo que MSX1 no puede.</summary>
    public bool HasSeveralColors =>
        ArraySpriteRows.Any(row => row.Color != ArraySpriteRows[0].Color);

    /// <summary>
    /// Deja las 16 lineas del color de la primera.
    /// </summary>
    /// <remarks>
    /// Es lo que hace un banco MSX1 con cualquier patron que le llegue: alli el color va en
    /// el byte de atributo del sprite, uno para todo el patron, y el que se escribe es el de
    /// la primera linea. Aplanarlo al entrar hace que el lienzo enseñe lo que la maquina va
    /// a pintar; sin aplanarlo se veria un patron de 16 colores dentro de un banco que solo
    /// sabe pintar uno.
    /// </remarks>
    public void FlattenColor()
    {
        int color = ArraySpriteRows[0].Color;

        foreach (SpriteRow row in ArraySpriteRows)
            row.Color = color;
    }

    public void CopyFrom(Sprite other)
    {
        for (int row = 0; row < Rows; row++)
            ArraySpriteRows[row].CopyFrom(other.ArraySpriteRows[row]);

        Background = other.Background;
    }

    /// <inheritdoc cref="Tile.FlipHorizontal"/>
    public void FlipHorizontal()
    {
        foreach (SpriteRow row in ArraySpriteRows)
            PatternMoves.Mirror(row.ArrayColumns);
    }

    /// <summary>Turns the drawing upside down, colours included.</summary>
    /// <remarks>
    /// The colour of a line belongs to that line, so it travels with it. In an MSX1 bank all
    /// sixteen go the same colour and this makes no difference; in MSX2 it is what keeps a
    /// figure with a red head from coming back with the red still on top.
    /// </remarks>
    public void FlipVertical() => Reorder(line => Rows - 1 - line);

    /// <summary>
    /// Turns the drawing a quarter, one way or the other.
    /// </summary>
    /// <remarks>
    /// The pixels turn and the line colours do not, because there is one per line and turning
    /// it would mean ending up down a column: the attribute table of the VDP has no way of
    /// saying that. A pattern of a single colour turns exactly.
    /// </remarks>
    public void Turn(bool clockwise) =>
        PatternMoves.Turn([.. ArraySpriteRows.Select(row => row.ArrayColumns)], clockwise);

    /// <inheritdoc cref="Tile.Shift"/>
    public void Shift(int dx, int dy)
    {
        foreach (SpriteRow row in ArraySpriteRows)
            PatternMoves.Shift(row.ArrayColumns, dx);

        if (dy != 0)
            Reorder(line => PatternMoves.LineFrom(line, dy, Rows));
    }

    /// <inheritdoc cref="Tile.Reorder"/>
    private void Reorder(Func<int, int> source)
    {
        Sprite before = Copy();

        for (int line = 0; line < Rows; line++)
            ArraySpriteRows[line].CopyFrom(before.ArraySpriteRows[source(line)]);
    }
}
