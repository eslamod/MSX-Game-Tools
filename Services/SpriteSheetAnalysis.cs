using Avalonia;
using Avalonia.Media;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>Un rectángulo de celdas de la hoja, en celdas y no en pixeles.</summary>
public sealed record SheetSelection(int Left, int Top, int Columns, int Rows)
{
    public int Cells => Columns * Rows;
}

/// <summary>Lo que le va a costar una celda: los planos que hay que superponer.</summary>
public sealed record SheetCellPlan(int Column, int Row, int Planes);

/// <summary>
/// Lo que se sabe de una hoja antes de tocar nada.
/// </summary>
/// <param name="Colors">Los colores distintos que trae la selección, sin el transparente.</param>
/// <param name="Masks">El índice de paleta que le tocaría a cada uno de esos colores.</param>
/// <param name="Cells">Lo que pide cada celda, para saber cuáles salen caras.</param>
/// <param name="Planes">Los planos de la celda que más pide.</param>
/// <param name="Patterns">
/// Los patrones que gastaría del banco. Es la suma de los planos de cada celda y no las celdas
/// por el máximo: una celda de dos colores no gasta cuatro huecos porque otra los necesite.
/// </param>
public sealed record SheetAnalysis(
    IReadOnlyList<Color> Colors,
    IReadOnlyList<int> Masks,
    IReadOnlyList<SheetCellPlan> Cells,
    int Planes,
    int Patterns,
    IReadOnlyList<string> Problems)
{
    public bool Ok => Problems.Count == 0;

    /// <summary>
    /// El banco más pequeño en el que cabe lo que se ha pedido.
    /// </summary>
    /// <remarks>
    /// Traer una hoja crea el banco, así que el tamaño no hay que preguntarlo: sale de lo que
    /// se ha elegido. Pasar de 64 no es gratis -eso ya no cabe en la tabla de patrones de la
    /// VRAM y hay que ir volcándolo por partes-, así que se coge el más pequeño que valga y se
    /// dice cuál es antes de importar.
    /// </remarks>
    public int BankSize =>
        SpriteBank.Capacities.FirstOrDefault(size => Patterns <= size, SpriteBank.Capacities[^1]);

    /// <summary>Si lo que pide entra en el banco más grande que hay.</summary>
    public bool Fits => Patterns <= SpriteBank.Capacities[^1];
}

/// <summary>
/// Mira una hoja de sprites y dice qué haría falta para traerla.
/// </summary>
/// <remarks>
/// <para>
/// Es la primera etapa del importador y no una previa aparte: sin repartir los índices no se
/// puede descomponer nada, y sin contar los patrones no se sabe si cabe. Lo que se enseña
/// antes de escribir es justo el resultado de este paso.
/// </para>
/// <para>
/// Y hace falta enseñarlo porque el banco tiene 64 huecos y una hoja cualquiera trae cientos
/// de celdas. Con tres planos, veintiuna celdas lo llenan. Traer una hoja entera no existe:
/// lo que existe es elegir un rectángulo y saber de antemano si entra.
/// </para>
/// </remarks>
public static class SpriteSheetAnalysis
{
    /// <summary>Lados de celda que sabe leer, que son los dos tamaños de sprite del VDP.</summary>
    public static IReadOnlyList<int> CellSizes { get; } = [8, 16];

    /// <param name="transparent">
    /// El color que hace de transparente, para las hojas que no traen alfa. Los pixeles con
    /// alfa a cero lo son siempre, se pase lo que se pase aquí.
    /// </param>
    /// <param name="maxPlanes">Sprites superpuestos que se está dispuesto a gastar por celda.</param>
    /// <param name="type">
    /// De qué máquina va a ser el banco, que cambia la aritmética entera.
    /// </param>
    /// <param name="across">Sprites de ancho que mide una figura de la hoja.</param>
    /// <param name="down">Y de alto.</param>
    /// <remarks>
    /// <para>
    /// Los dos modos no se parecen en nada más que en leer la hoja. En <b>MSX2</b> el color va
    /// por línea y existe el bit CC, que mezcla con un OR los colores de los sprites que se
    /// solapan: con las máscaras a potencias de dos, k planos dan 2^k-1 colores, y por eso dos
    /// sprites pueden enseñar tres.
    /// </para>
    /// <para>
    /// En <b>MSX1</b> no hay nada de eso. Un sprite tiene un color, el mismo para sus dieciséis
    /// líneas, y solaparlos no mezcla: gana el de más prioridad. Así que los planos que hace
    /// falta son tantos como colores distintos tenga la celda, sin repartos ni combinaciones.
    /// </para>
    /// </remarks>
    public static SheetAnalysis Analyse(
        int[] pixels,
        PixelSize size,
        int cellSize,
        Color? transparent,
        SheetSelection selection,
        int maxPlanes,
        SpriteBank.SpriteType type = SpriteBank.SpriteType.MSX2,
        int across = 1,
        int down = 1)
    {
        if (Fence(size, cellSize, selection) is { } stopped)
            return stopped;

        var problems = new List<string>();

        // Los colores primero: sin ellos no hay nada que repartir, y son el primer sitio donde
        // una hoja se cae -quince es el tope, y una hoja de verdad trae cientos-.
        List<Color> colors = [.. Colors(pixels, size, cellSize, transparent, selection)];

        if (colors.Count == 0)
        {
            problems.Add("El rectángulo elegido está entero en el color transparente.");

            return Empty(problems);
        }

        if (colors.Count > SpritePlaneAssignment.MaxColors)
        {
            problems.Add(
                $"El rectángulo elegido trae {colors.Count} colores y en la paleta caben "
                + $"{SpritePlaneAssignment.MaxColors}, porque el índice 0 es el transparente.");

            return Empty(problems);
        }

        // En MSX1 no hay nada que repartir: cada color es un sprite y su índice de paleta es
        // el que le toque por orden. Lo único que puede fallar es que una celda traiga más
        // colores de los sprites que se está dispuesto a superponer.
        if (type == SpriteBank.SpriteType.MSX)
        {
            List<int> plain = [.. Enumerable.Range(1, colors.Count)];

            List<SheetCellPlan> flat =
                [.. Plans(pixels, size, cellSize, transparent, selection, colors, plain, type)];

            if (flat.FirstOrDefault(cell => cell.Planes > maxPlanes) is { } tight)
            {
                problems.Add(
                    $"La celda {tight.Column},{tight.Row} trae {tight.Planes} colores y en MSX1 "
                    + $"cada sprite es de un color, así que harían falta {tight.Planes} "
                    + $"superpuestos y se han pedido {maxPlanes}.");

                return Empty(problems);
            }

            return new SheetAnalysis(
                colors,
                plain,
                flat,
                PerScanline(flat, selection, across, down),
                flat.Sum(cell => cell.Planes),
                problems);
        }

        // Cada línea de cada celda es un problema aparte: en modo 2 tanto los bits del patrón
        // como el color van por línea, así que nunca hay que resolver un cuadrado de 16x16.
        List<int[]> lines = [.. Lines(pixels, size, cellSize, transparent, selection, colors)];

        if (SpritePlaneAssignment.Solve(lines, colors.Count, maxPlanes) is not { } solved)
        {
            problems.Add(Why(lines, colors, maxPlanes));

            return Empty(problems);
        }

        List<SheetCellPlan> cells =
            [.. Plans(pixels, size, cellSize, transparent, selection, colors, solved.Masks, type)];

        return new SheetAnalysis(
            colors,
            solved.Masks,
            cells,
            PerScanline(cells, selection, across, down),
            cells.Sum(cell => cell.Planes),
            problems);
    }

    /// <summary>
    /// Cuántos sprites coinciden en la línea de barrido peor de todas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No es la suma de la figura ni el máximo de sus trozos. Una figura de 16x32 son dos
    /// sprites apilados: el de arriba cubre las líneas 0 a 15 y el de abajo las 16 a 31, así que
    /// no coinciden en ninguna y sus planos no se suman. Los que sí se suman son los que van
    /// lado a lado, que comparten las mismas líneas.
    /// </para>
    /// <para>
    /// Es el número que decide si la figura se puede enseñar: el VDP dibuja cuatro sprites por
    /// línea en modo 1 y ocho en el 2, y a partir de ahí deja de pintar. Los patrones que gasta
    /// del banco son otra cuenta, y esa sí es la suma de todo.
    /// </para>
    /// </remarks>
    private static int PerScanline(
        IReadOnlyList<SheetCellPlan> cells, SheetSelection selection, int across, int down)
    {
        if (cells.Count == 0)
            return 0;

        int worst = 0;

        for (int top = 0; top + down <= selection.Rows; top += down)
        {
            for (int left = 0; left + across <= selection.Columns; left += across)
            {
                for (int band = 0; band < down; band++)
                {
                    int line = 0;

                    for (int column = 0; column < across; column++)
                        line += cells[((top + band) * selection.Columns) + left + column].Planes;

                    worst = Math.Max(worst, line);
                }
            }
        }

        return worst;
    }

    /// <summary>
    /// Lo que cuesta traer la selección como tabla de patrones, sin color ni grupos.
    /// </summary>
    /// <remarks>
    /// Un patrón por celda, cuente los colores que cuente: aquí un pixel sólo está o no está.
    /// Sin saltarse las celdas vacías y sin reaprovechar las repetidas, al revés que el modo de
    /// color, porque lo que se quiere de una tabla es que el patrón número N sea la celda
    /// número N de lo que se eligió. Saltarse una rompería esa cuenta sin decir nada.
    /// <para>
    /// Con <paramref name="pack"/> se hace lo del otro modo: fuera las vacías y un solo patrón
    /// para las que salen iguales. Hay que mirar los pixeles para contarlo, porque si no el
    /// número que se enseña antes de importar no sería el que se va a escribir.
    /// </para>
    /// </remarks>
    public static SheetAnalysis AnalysePatterns(
        int[] pixels,
        PixelSize size,
        int cellSize,
        Color? transparent,
        SheetSelection selection,
        bool pack = false)
    {
        if (Fence(size, cellSize, selection) is { } stopped)
            return stopped;

        var cells = new List<SheetCellPlan>();
        var seen = new HashSet<string>();
        int patterns = 0;

        for (int row = 0; row < selection.Rows; row++)
        {
            for (int column = 0; column < selection.Columns; column++)
            {
                int left = selection.Left + column;
                int top = selection.Top + row;

                bool blank = pack && SpriteSheetPixels.IsBlankCell(
                    pixels, size, cellSize, transparent, left, top);

                cells.Add(new SheetCellPlan(left, top, blank ? 0 : 1));

                if (blank)
                    continue;

                // Aprovechando el banco, dos celdas con el mismo dibujo comparten patrón. Las
                // celdas siguen contándose todas: lo que baja es lo que se va a escribir.
                if (!pack || seen.Add(SpriteSheetPixels.CellKey(
                        pixels, size, cellSize, transparent, left, top)))
                {
                    patterns++;
                }
            }
        }

        return new SheetAnalysis([], [], cells, 1, patterns, []);
    }

    /// <summary>
    /// Lo que hay que mirar antes de nada, y es igual en los dos modos.
    /// </summary>
    /// <returns>El resultado con el problema, o <c>null</c> si se puede seguir.</returns>
    private static SheetAnalysis? Fence(PixelSize size, int cellSize, SheetSelection selection)
    {
        if (!CellSizes.Contains(cellSize))
        {
            return Empty(
                [$"La celda mide {cellSize} y sólo se sabe leer de {string.Join(" o ", CellSizes)}."]);
        }

        if (!Inside(size, cellSize, selection))
        {
            return Empty(
            [
                $"El rectángulo elegido se sale de la hoja, que mide "
                + $"{size.Width / cellSize}x{size.Height / cellSize} celdas de {cellSize}.",
            ]);
        }

        return null;
    }

    private static SheetAnalysis Empty(IReadOnlyList<string> problems) =>
        new([], [], [], 0, 0, problems);

    /// <summary>
    /// Por qué no sale, con los colores que se estorban por delante.
    /// </summary>
    /// <remarks>
    /// «Con 2 planos no salen estos 5 colores» deja adivinando cuál sobra. Diciendo cuáles se
    /// atan y por dónde, se sabe qué retocar en la hoja: casi siempre hay un color que coincide
    /// con todo —una sombra, un contorno— y es el que está gastando el plano de más.
    /// </remarks>
    private static string Why(
        IReadOnlyList<int[]> lines, IReadOnlyList<Color> colors, int maxPlanes)
    {
        int fit = (1 << maxPlanes) - 1;

        string general =
            $"Con {maxPlanes} sprites superpuestos caben {fit} colores en la misma línea, y el "
            + $"reparto es de toda la selección a la vez porque la paleta es una sola.";

        if (SpritePlaneAssignment.Explain(lines, colors.Count, maxPlanes) is not { } clash)
            return $"{general} Estos {colors.Count} colores no salen.";

        string offending = Names(clash.Colors, colors);

        if (clash.Shared.Count == 0)
            return $"{general} En una misma línea coinciden {clash.Colors.Count}: {offending}.";

        return $"{general} {Names(clash.Shared, colors)} coinciden en una línea con unos colores "
            + $"y en otra con otros, así que los {clash.Colors.Count} tienen que caber en los "
            + $"mismos planos: {offending}.";
    }

    /// <summary>Los colores en hexadecimal, que es como se reconocen en el editor de imágenes.</summary>
    private static string Names(IReadOnlyList<int> numbers, IReadOnlyList<Color> colors) =>
        string.Join(", ", numbers
            .Where(number => number < colors.Count)
            .Select(number => $"#{colors[number].R:X2}{colors[number].G:X2}{colors[number].B:X2}"));

    private static bool Inside(PixelSize size, int cellSize, SheetSelection selection) =>
        selection.Left >= 0
        && selection.Top >= 0
        && selection.Columns > 0
        && selection.Rows > 0
        && (selection.Left + selection.Columns) * cellSize <= size.Width
        && (selection.Top + selection.Rows) * cellSize <= size.Height;

    /// <summary>Los colores distintos del rectángulo, en el orden en que se encuentran.</summary>
    private static IEnumerable<Color> Colors(
        int[] pixels, PixelSize size, int cellSize, Color? transparent, SheetSelection selection)
    {
        var seen = new List<Color>();

        foreach (int pixel in Pixels(pixels, size, cellSize, selection))
        {
            if (SpriteSheetPixels.IsClear(pixel, transparent))
                continue;

            Color color = SpriteSheetPixels.FromBgra(pixel);

            if (!seen.Contains(color))
                seen.Add(color);
        }

        return seen;
    }

    /// <summary>Los colores que coinciden en cada línea de cada celda del rectángulo.</summary>
    private static IEnumerable<int[]> Lines(
        int[] pixels,
        PixelSize size,
        int cellSize,
        Color? transparent,
        SheetSelection selection,
        IReadOnlyList<Color> colors)
    {
        for (int row = 0; row < selection.Rows; row++)
        {
            for (int column = 0; column < selection.Columns; column++)
            {
                for (int line = 0; line < cellSize; line++)
                    yield return Line(pixels, size, cellSize, transparent, selection, colors, column, row, line);
            }
        }
    }

    /// <summary>Los números de color distintos de una línea de una celda.</summary>
    private static int[] Line(
        int[] pixels,
        PixelSize size,
        int cellSize,
        Color? transparent,
        SheetSelection selection,
        IReadOnlyList<Color> colors,
        int column,
        int row,
        int line)
    {
        var seen = new List<int>();

        int left = (selection.Left + column) * cellSize;
        int top = ((selection.Top + row) * cellSize) + line;

        for (int x = 0; x < cellSize; x++)
        {
            int color = SpriteSheetPixels.ColorAt(
                pixels, size, left + x, top, transparent, colors);

            if (color >= 0 && !seen.Contains(color))
                seen.Add(color);
        }

        return [.. seen];
    }

    /// <summary>Lo que pide cada celda con los índices ya repartidos.</summary>
    private static IEnumerable<SheetCellPlan> Plans(
        int[] pixels,
        PixelSize size,
        int cellSize,
        Color? transparent,
        SheetSelection selection,
        IReadOnlyList<Color> colors,
        IReadOnlyList<int> masks,
        SpriteBank.SpriteType type)
    {
        for (int row = 0; row < selection.Rows; row++)
        {
            for (int column = 0; column < selection.Columns; column++)
            {
                int planes = 0;

                // En MSX1 el color es del sprite entero, así que la cuenta es de la celda y no
                // de una línea: dos colores que no coinciden en ninguna línea siguen siendo dos
                // sprites, porque ninguno de los dos puede cambiar de color a medio camino.
                if (type == SpriteBank.SpriteType.MSX)
                {
                    var seen = new HashSet<int>();

                    for (int line = 0; line < cellSize; line++)
                    {
                        seen.UnionWith(Line(
                            pixels, size, cellSize, transparent, selection, colors, column, row, line));
                    }

                    planes = seen.Count;
                }
                else
                {
                    for (int line = 0; line < cellSize; line++)
                    {
                        int merged = 0;

                        foreach (int color in Line(
                            pixels, size, cellSize, transparent, selection, colors, column, row, line))
                        {
                            merged |= masks[color];
                        }

                        planes = Math.Max(planes, System.Numerics.BitOperations.PopCount((uint)merged));
                    }
                }

                yield return new SheetCellPlan(selection.Left + column, selection.Top + row, planes);
            }
        }
    }

    private static IEnumerable<int> Pixels(
        int[] pixels, PixelSize size, int cellSize, SheetSelection selection)
    {
        for (int y = 0; y < selection.Rows * cellSize; y++)
        {
            int top = (selection.Top * cellSize) + y;

            for (int x = 0; x < selection.Columns * cellSize; x++)
                yield return pixels[(top * size.Width) + (selection.Left * cellSize) + x];
        }
    }

}
