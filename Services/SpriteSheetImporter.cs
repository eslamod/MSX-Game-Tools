using Avalonia;
using Avalonia.Media;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>Un plano de un trozo de la figura, con donde cae ese trozo.</summary>
internal sealed record SheetSlice(int Pattern, int OffsetX, int OffsetY)
{
    /// <summary>De que trozo de la figura es, para saber cuales comparten linea.</summary>
    public (int X, int Y) Place => (OffsetX, OffsetY);
}

/// <summary>Lo que sale de traer una hoja: el banco con sus grupos y su paleta.</summary>
public sealed record SheetImport(
    SpriteBank? Bank,
    ColorPalette? Palette,
    SheetAnalysis Analysis,
    IReadOnlyList<string> Problems)
{
    public bool Ok => Bank is not null;
}

/// <summary>
/// Trae un rectángulo de una hoja de sprites a un banco con sus grupos.
/// </summary>
/// <remarks>
/// La última etapa, y la que menos decide: el reparto de índices y la descomposición ya están
/// hechos, así que aquí sólo se colocan los patrones en el banco y se monta un grupo por celda.
/// </remarks>
public static class SpriteSheetImporter
{
    /// <inheritdoc cref="SpriteSheetAnalysis.Analyse" path="/param[@name='transparent']"/>
    /// <inheritdoc cref="SpriteSheetAnalysis.Analyse" path="/param[@name='maxPlanes']"/>
    /// <inheritdoc cref="SpriteSheetAnalysis.Analyse" path="/param[@name='type']"/>
    /// <inheritdoc cref="SpriteSheetAnalysis.Analyse" path="/param[@name='across']"/>
    /// <inheritdoc cref="SpriteSheetAnalysis.Analyse" path="/param[@name='down']"/>
    public static SheetImport Import(
        int[] pixels,
        PixelSize size,
        int cellSize,
        Color? transparent,
        SheetSelection selection,
        int maxPlanes,
        string name,
        SpriteBank.SpriteType type = SpriteBank.SpriteType.MSX2,
        int across = 1,
        int down = 1)
    {
        SheetAnalysis analysis = SpriteSheetAnalysis.Analyse(
            pixels, size, cellSize, transparent, selection, maxPlanes, type, across, down);

        if (!analysis.Ok)
            return new SheetImport(null, null, analysis, analysis.Problems);

        if (!analysis.Fits)
        {
            return new SheetImport(null, null, analysis,
            [
                $"Harían falta {analysis.Patterns} patrones y un banco tiene "
                + $"{SpriteBank.MaxSprites}. Elige menos celdas.",
            ]);
        }

        var bank = new SpriteBank(type, name);
        ColorPalette palette = PaletteOf(name, analysis);

        var problems = new List<string>();
        var placed = new Dictionary<string, int>();
        int next = 0;

        // Por figuras, no por sprites: una figura de 16x32 son dos sprites apilados que van al
        // mismo grupo, cada uno con su desplazamiento. Es lo que hace que la cabeza y el cuerpo
        // de un personaje se coloquen juntos en vez de quedar como dos grupos sueltos.
        for (int row = 0; row + down <= selection.Rows; row += down)
        {
            for (int column = 0; column + across <= selection.Columns; column += across)
            {
                var figure = new List<SheetSlice>();

                for (int band = 0; band < down; band++)
                {
                    for (int slice = 0; slice < across; slice++)
                    {
                        IReadOnlyList<SheetPlane> planes = SpriteSheetDecomposer.Decompose(
                            pixels, size, cellSize, transparent, analysis.Colors, analysis.Masks,
                            selection.Left + column + slice, selection.Top + row + band, type);

                        // Un trozo vacio no gasta patron ni entra en el grupo. La figura sigue:
                        // un personaje con la esquina de arriba a la derecha en blanco no deja
                        // de ser un personaje.
                        if (planes.Count == 0)
                            continue;

                        if (!Place(bank, planes, cellSize, placed, ref next, out List<int> patterns))
                        {
                            problems.Add(
                                $"La celda {selection.Left + column + slice},"
                                + $"{selection.Top + row + band} no cabe: "
                                + $"el banco se ha quedado sin huecos.");

                            return new SheetImport(null, null, analysis, problems);
                        }

                        foreach (int pattern in patterns)
                            figure.Add(new SheetSlice(pattern, slice * cellSize, band * cellSize));
                    }
                }

                if (figure.Count == 0)
                    continue;

                Group(bank, figure, selection.Left + column, selection.Top + row, type);
            }
        }

        return new SheetImport(bank, palette, analysis, problems);
    }

    /// <summary>
    /// Trae la selección como tabla de patrones: sin color y sin grupos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Un pixel está o no está: donde la hoja no es transparente va un bit, y donde lo es no va
    /// nada. Es lo que hace falta cuando el dibujo se colorea en el juego, o cuando la hoja
    /// viene ya en blanco y negro y el color no significa nada.
    /// </para>
    /// <para>
    /// Un patrón por celda y en orden de lectura, incluidas las vacías, y sin reaprovechar las
    /// repetidas. Al revés que el modo de color, y a propósito: de una tabla se espera que el
    /// patrón número N sea la celda número N de lo que se eligió, y saltarse una rompería esa
    /// cuenta sin decir nada.
    /// </para>
    /// <para>
    /// Con <paramref name="skipEmpty"/> se hace lo contrario, y también tiene su razón: una
    /// celda vacía son treinta y dos bytes de patrón y un hueco del banco para no pintar nada,
    /// y en una hoja con separación entre figuras eso es la mitad de la tabla. Lo que se pierde
    /// es poder direccionar el patrón por el número de celda, así que lo elige quien importa.
    /// </para>
    /// </remarks>
    public static SheetImport ImportPatterns(
        int[] pixels,
        PixelSize size,
        int cellSize,
        Color? transparent,
        SheetSelection selection,
        string name,
        bool skipEmpty = false)
    {
        SheetAnalysis analysis = SpriteSheetAnalysis.AnalysePatterns(
            pixels, size, cellSize, transparent, selection, skipEmpty);

        if (!analysis.Ok)
            return new SheetImport(null, null, analysis, analysis.Problems);

        if (!analysis.Fits)
        {
            return new SheetImport(null, null, analysis,
            [
                $"Harían falta {analysis.Patterns} patrones y un banco tiene "
                + $"{SpriteBank.MaxSprites}. Elige menos celdas.",
            ]);
        }

        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, name);
        int next = 0;

        for (int row = 0; row < selection.Rows; row++)
        {
            for (int column = 0; column < selection.Columns; column++)
            {
                if (skipEmpty && SpriteSheetPixels.IsBlankCell(
                        pixels, size, cellSize, transparent,
                        selection.Left + column, selection.Top + row))
                {
                    continue;
                }

                Trace(
                    bank.SpritesList[next++],
                    pixels, size, cellSize, transparent,
                    (selection.Left + column) * cellSize,
                    (selection.Top + row) * cellSize);
            }
        }

        return new SheetImport(bank, null, analysis, []);
    }

    /// <summary>
    /// Calca una celda en un patrón: un bit donde haya pixel.
    /// </summary>
    /// <remarks>
    /// El color de las líneas se queda como estaba. No es dejarse nada: en este modo el color
    /// no sale de la hoja, y ponerlo a algo sería inventarse una decisión que es del juego.
    /// </remarks>
    private static void Trace(
        Sprite sprite, int[] pixels, PixelSize size, int cellSize, Color? transparent, int left, int top)
    {
        for (int row = 0; row < Sprite.Rows; row++)
        {
            SpriteRow line = sprite.ArraySpriteRows[row];

            for (int column = 0; column < SpriteRow.Columns; column++)
            {
                line.ArrayColumns[column] = row < cellSize && column < cellSize
                    && !SpriteSheetPixels.IsClear(
                        pixels[((top + row) * size.Width) + left + column], transparent);
            }
        }
    }

    /// <summary>
    /// La paleta con cada color en el índice que le tocó.
    /// </summary>
    /// <remarks>
    /// Aquí es donde el reparto se hace de verdad: el índice que eligió el repartidor tiene que
    /// llevar ese color, porque es lo que hace que el OR del VDP dé el color que toca. Los
    /// índices que no usa nadie se quedan como estaban.
    /// </remarks>
    private static ColorPalette PaletteOf(string name, SheetAnalysis analysis)
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard().Clone(name);

        for (int color = 0; color < analysis.Colors.Count; color++)
        {
            Color rgb = analysis.Colors[color];

            palette[analysis.Masks[color]].SetComponents(
                Component(rgb.R), Component(rgb.G), Component(rgb.B));
        }

        return palette;
    }

    /// <summary>De 0-255 a los tres bits por componente del V9938.</summary>
    private static int Component(byte value) => (value * 7) / 255;

    /// <summary>
    /// Coloca los planos de una celda en el banco, reaprovechando los que ya estén.
    /// </summary>
    /// <remarks>
    /// Dos celdas de una hoja repiten patrones a menudo -una animación cambia dos líneas y el
    /// resto es el mismo dibujo-, y con 64 huecos eso decide si entra o no. Se compara el
    /// patrón entero, bits y color, porque el mismo dibujo a otro color es otro patrón.
    /// </remarks>
    private static bool Place(
        SpriteBank bank,
        IReadOnlyList<SheetPlane> planes,
        int cellSize,
        Dictionary<string, int> placed,
        ref int next,
        out List<int> patterns)
    {
        patterns = [];

        foreach (SheetPlane plane in planes)
        {
            string key = KeyOf(plane, cellSize);

            if (placed.TryGetValue(key, out int already))
            {
                patterns.Add(already);

                continue;
            }

            if (next >= SpriteBank.MaxSprites)
                return false;

            Write(bank.SpritesList[next], plane, cellSize);

            placed[key] = next;
            patterns.Add(next);

            next++;
        }

        return true;
    }

    /// <summary>Cómo se reconoce un patrón ya colocado: sus bits y su color.</summary>
    private static string KeyOf(SheetPlane plane, int cellSize)
    {
        var key = new System.Text.StringBuilder();

        key.Append(plane.Color).Append(':');

        for (int y = 0; y < cellSize; y++)
        {
            for (int x = 0; x < cellSize; x++)
                key.Append(plane.Rows[y][x] ? '1' : '0');
        }

        return key.ToString();
    }

    /// <summary>
    /// Vuelca un plano en un patrón del banco.
    /// </summary>
    /// <remarks>
    /// Una celda de 8x8 se queda arriba a la izquierda de un patrón de 16x16: el banco es de
    /// sprites de 16 y el resto queda en blanco, que es lo mismo que hace el VDP con un sprite
    /// de 8 puesto en modo 16.
    /// </remarks>
    private static void Write(Sprite sprite, SheetPlane plane, int cellSize)
    {
        for (int row = 0; row < Sprite.Rows; row++)
        {
            SpriteRow line = sprite.ArraySpriteRows[row];

            line.Color = plane.Color;

            for (int column = 0; column < SpriteRow.Columns; column++)
            {
                line.ArrayColumns[column] =
                    row < cellSize && column < cellSize && plane.Rows[row][column];
            }
        }
    }

    /// <summary>
    /// Monta el grupo de una celda: un plano por patrón.
    /// </summary>
    /// <remarks>
    /// <para>
    /// En MSX2, con CC en todos menos el primero, que es contra quien combinan los demás: ese
    /// es el OR que hace que dos sprites ensenen tres colores.
    /// </para>
    /// <para>
    /// En MSX1 no se toca el CC porque no existe. Los sprites de una celda se solapan y ya
    /// esta: cada uno pinta su color y donde coinciden gana el de mas prioridad. Encenderlo
    /// aqui escribiria un banco que la maquina no puede ensenar.
    /// </para>
    /// <para>
    /// Todos van sin desplazamiento: son el mismo dibujo superpuesto, no un personaje repartido
    /// en trozos.
    /// </para>
    /// </remarks>
    private static void Group(
        SpriteBank bank,
        IReadOnlyList<SheetSlice> figure,
        int column,
        int row,
        SpriteBank.SpriteType type)
    {
        if (bank.NewGroup(figure[0].Pattern) is not { } group)
            return;

        group.Name = $"{column},{row}";

        for (int plane = 1; plane < figure.Count; plane++)
        {
            group.Add(new SpriteGroupMember(figure[plane].Pattern, bank.SpritesList[figure[plane].Pattern])
            {
                OffsetX = figure[plane].OffsetX,
                OffsetY = figure[plane].OffsetY,
            });
        }

        if (type == SpriteBank.SpriteType.MSX)
            return;

        // El primer plano de cada trozo se queda sin CC, y no vale con el primero del grupo:
        // dos trozos que no comparten ninguna linea de barrido no se habilitan entre si, asi
        // que el CC del segundo no lo encenderia nadie y esa linea no se dibujaria.
        for (int plane = 1; plane < group.Members.Count; plane++)
        {
            if (figure[plane].Place != figure[plane - 1].Place)
                continue;

            foreach (SpriteAttributeRow line in group.Members[plane].Rows)
                line.CombineColor = true;
        }
    }
}
