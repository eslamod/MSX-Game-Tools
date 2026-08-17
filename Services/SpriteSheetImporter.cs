using Avalonia;
using Avalonia.Media;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

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
    public static SheetImport Import(
        int[] pixels,
        PixelSize size,
        int cellSize,
        Color? transparent,
        SheetSelection selection,
        int maxPlanes,
        string name)
    {
        SheetAnalysis analysis = SpriteSheetAnalysis.Analyse(
            pixels, size, cellSize, transparent, selection, maxPlanes);

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
        ColorPalette palette = PaletteOf(name, analysis);

        var problems = new List<string>();
        var placed = new Dictionary<string, int>();
        int next = 0;

        for (int row = 0; row < selection.Rows; row++)
        {
            for (int column = 0; column < selection.Columns; column++)
            {
                IReadOnlyList<SheetPlane> planes = SpriteSheetDecomposer.Decompose(
                    pixels, size, cellSize, transparent, analysis.Colors, analysis.Masks,
                    selection.Left + column, selection.Top + row);

                // Una celda vacía es un hueco de la hoja: ni patrón ni grupo.
                if (planes.Count == 0)
                    continue;

                if (!Place(bank, planes, cellSize, placed, ref next, out List<int> patterns))
                {
                    problems.Add(
                        $"La celda {selection.Left + column},{selection.Top + row} no cabe: "
                        + $"el banco se ha quedado sin huecos.");

                    return new SheetImport(null, null, analysis, problems);
                }

                Group(bank, patterns, selection.Left + column, selection.Top + row);
            }
        }

        return new SheetImport(bank, palette, analysis, problems);
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
    /// Monta el grupo de una celda: un plano por patrón, con CC en todos menos el primero.
    /// </summary>
    /// <remarks>
    /// El primero sin CC porque es contra él contra quien combinan los demás. Todos van sin
    /// desplazamiento: son el mismo dibujo superpuesto, no un personaje repartido en trozos.
    /// </remarks>
    private static void Group(SpriteBank bank, IReadOnlyList<int> patterns, int column, int row)
    {
        if (bank.NewGroup(patterns[0]) is not { } group)
            return;

        group.Name = $"{column},{row}";

        for (int plane = 1; plane < patterns.Count; plane++)
            group.Add(new SpriteGroupMember(patterns[plane], bank.SpritesList[patterns[plane]]));

        for (int plane = 1; plane < group.Members.Count; plane++)
        {
            foreach (SpriteAttributeRow line in group.Members[plane].Rows)
                line.CombineColor = true;
        }
    }
}
