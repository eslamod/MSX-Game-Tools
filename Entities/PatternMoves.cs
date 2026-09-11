namespace MSX_GameTools.Entities;

/// <summary>
/// The moves a square drawing can make: mirror, quarter turn and one pixel shift.
/// </summary>
/// <remarks>
/// <para>
/// Pixels only. What happens to the colours is not the same for every move, and a tile does not
/// keep them like a sprite does -two per line against one-, so each drawing takes care of its
/// own. What they share is that a colour belongs to the line it is on, which is what decides
/// whether a move has to carry it along.
/// </para>
/// <para>
/// Shifting wraps around, which is what makes it useful on eight or sixteen pixels: nudging a
/// figure to see how it sits has to be undoable by nudging it back, and that is not true if
/// whatever falls off the edge is dropped.
/// </para>
/// </remarks>
internal static class PatternMoves
{
    /// <summary>Turns a line around: what was on the left ends up on the right.</summary>
    public static void Mirror(bool[] line)
    {
        for (int left = 0, right = line.Length - 1; left < right; left++, right--)
            (line[left], line[right]) = (line[right], line[left]);
    }

    /// <summary>Moves a line sideways, bringing in at one end what leaves at the other.</summary>
    public static void Shift(bool[] line, int by)
    {
        if (by == 0)
            return;

        bool[] before = (bool[])line.Clone();

        for (int column = 0; column < line.Length; column++)
            line[column] = before[Wrap(column - by, line.Length)];
    }

    /// <summary>
    /// Turns the square a quarter, one way or the other.
    /// </summary>
    /// <remarks>
    /// The pixels and nothing else. A quarter turn would need the colours to go from lines to
    /// columns, and that is exactly what neither the tile nor the sprite can say: a drawing of
    /// one colour comes out right, and one with colour bands comes out turned with its bands
    /// still lying flat, which is all the VDP knows how to paint.
    /// </remarks>
    public static void Turn(bool[][] lines, bool clockwise)
    {
        int size = lines.Length;
        bool[][] before = [.. lines.Select(line => (bool[])line.Clone())];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                lines[y][x] = clockwise
                    ? before[size - 1 - x][y]
                    : before[x][size - 1 - y];
            }
        }
    }

    /// <summary>Which line the one at <paramref name="line"/> takes its content from.</summary>
    /// <remarks>
    /// Moving whole lines is the drawing's own job, because its colours travel with them, but
    /// where each one comes from is this same wrapping and it is written down once.
    /// </remarks>
    public static int LineFrom(int line, int by, int count) => Wrap(line - by, count);

    /// <summary>A position inside the drawing, coming back in from the other side.</summary>
    private static int Wrap(int at, int size) => ((at % size) + size) % size;
}
