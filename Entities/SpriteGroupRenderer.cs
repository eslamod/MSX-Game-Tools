using Avalonia.Media;

namespace MSX_GameTools.Entities;

/// <summary>
/// Compone en una imagen los sprites de un grupo con sus desplazamientos, siguiendo
/// las reglas de prioridad y de combinación de color del modo 2 del V9938.
/// </summary>
/// <remarks>
/// Reglas del bit CC, según el apartado 5.2.5 del manual del V9938:
/// <list type="bullet">
/// <item>Con CC a 0 manda la prioridad normal: el sprite de menor número tapa a los
/// demás.</item>
/// <item>Con CC a 1 se cancela la prioridad de esa línea y su color se combina con OR
/// con el de los sprites con los que solape.</item>
/// <item>Una línea con CC a 1 <b>no se dibuja en absoluto</b> si en esa misma línea de
/// pantalla no hay ningún sprite de número menor cuya línea tenga CC a 0.</item>
/// </list>
/// Esa última condición es <b>por línea de pantalla, no por pixel</b>: basta con que el
/// sprite habilitante cubra la línea para que el de CC se vea, incluso en las columnas
/// donde el otro no pinta nada. Es lo que produce las regiones sueltas del ejemplo de
/// siete colores del manual.
/// </remarks>
public static class SpriteGroupRenderer
{
    /// <summary>
    /// Lado del lienzo del grupo: 16 del sprite más 15 de margen a cada lado, que es
    /// el desplazamiento máximo. Fijo a propósito, para que la miniatura no cambie de
    /// tamaño y reordene el panel cada vez que se toca una flecha.
    /// </summary>
    public const int PreviewSize = SpriteRow.Columns + (2 * SpriteGroupMember.MaxOffset);

    /// <summary>Punto del lienzo donde cae el desplazamiento cero.</summary>
    private const int Origin = SpriteGroupMember.MaxOffset;

    /// <summary>Marca de celda vacía en el buffer de índices de color.</summary>
    private const int Empty = -1;

    public static ImageMini CreatePreview() => new(PreviewSize, PreviewSize);

    public static void Render(
        SpriteGroup group,
        SpriteBank bank,
        ColorPalette palette,
        Color background,
        ImageMini target)
    {
        int[] indices = new int[PreviewSize * PreviewSize];
        Array.Fill(indices, Empty);

        // Los patrones se resuelven una vez: la composición recorre líneas de pantalla,
        // no miembros, porque la regla de CC depende de qué hay en cada línea.
        List<(SpriteGroupMember Member, Sprite Pattern)> members =
        [
            .. group.Members
                .Where(member => (uint)member.PatternIndex < (uint)bank.SpritesList.Count)
                .Select(member => (member, bank.SpritesList[member.PatternIndex])),
        ];

        for (int canvasY = 0; canvasY < PreviewSize; canvasY++)
            ComposeLine(indices, members, canvasY);

        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i];

            Color color = index == Empty
                ? background
                : SpriteRenderer.ResolveRowColor(palette, index, background);

            target.SetPixel(i % PreviewSize, i / PreviewSize, color);
        }
    }

    private static void ComposeLine(
        int[] indices,
        List<(SpriteGroupMember Member, Sprite Pattern)> members,
        int canvasY)
    {
        // ¿Ha aparecido ya, en esta línea, un sprite de mayor prioridad con CC a 0? Es
        // lo que habilita a los de CC. Se mira sólo hacia atrás, así que un CC en el
        // primer miembro nunca llega a dibujarse: no hay nadie por delante.
        bool enabledByHigherPriority = false;

        foreach ((SpriteGroupMember member, Sprite pattern) in members)
        {
            int row = canvasY - Origin - member.OffsetY;
            if ((uint)row >= Sprite.Rows)
                continue;

            SpriteAttributeRow attributes = member.Rows[row];

            if (!attributes.CombineColor)
            {
                if (member.IsVisible)
                    DrawLine(indices, pattern, member, row, canvasY, attributes.Color, combine: false);

                // Basta con que su línea caiga aquí, aunque no pinte ningún pixel. Y
                // sigue habilitando aunque esté oculto: ocultar un plano para mirar los
                // demás no puede hacer que a los demás se les caigan sus líneas con CC,
                // porque entonces no estarías viendo los demás, estarías viendo otra cosa.
                enabledByHigherPriority = true;

                continue;
            }

            if (enabledByHigherPriority && member.IsVisible)
                DrawLine(indices, pattern, member, row, canvasY, attributes.Color, combine: true);
        }
    }

    private static void DrawLine(
        int[] indices,
        Sprite pattern,
        SpriteGroupMember member,
        int row,
        int canvasY,
        int color,
        bool combine)
    {
        SpriteRow patternRow = pattern.ArraySpriteRows[row];

        for (int column = 0; column < SpriteRow.Columns; column++)
        {
            if (!patternRow.ArrayColumns[column])
                continue;

            int canvasX = Origin + member.OffsetX + column;
            if ((uint)canvasX >= PreviewSize)
                continue;

            int offset = (canvasY * PreviewSize) + canvasX;

            if (indices[offset] == Empty)
            {
                indices[offset] = color;
            }
            else if (combine)
            {
                // CC: el V9938 hace OR de los códigos de color de 4 bits, no del RGB.
                indices[offset] |= color;
            }

            // Sin CC gana el de mayor prioridad, que ya está escrito.
        }
    }
}
