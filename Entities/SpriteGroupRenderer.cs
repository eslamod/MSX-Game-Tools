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
    /// Lo que mide el lienzo de un grupo de un solo sprite sin desplazar.
    /// </summary>
    /// <remarks>
    /// El caso corriente, y lo que usa la interfaz para dimensionar la tira de miniaturas por
    /// defecto. Cada grupo se pinta en el suyo, que puede ser mayor; esto es sólo el punto de
    /// partida de los pasos de zoom.
    /// </remarks>
    public const int NominalSize = SpriteRow.Columns;

    /// <summary>Marca de celda vacía en el buffer de índices de color.</summary>
    private const int Empty = -1;

    /// <summary>
    /// El lienzo que le hace falta a un grupo, y dónde cae dentro el desplazamiento cero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sale de lo que el grupo ocupa de verdad y no de un tamaño fijo, porque una figura de
    /// dos sprites de alto no cabía: con un lienzo atado al desplazamiento máximo, subir ese
    /// tope para que quepa agranda además la miniatura de todos los demás grupos, que se
    /// quedarían casi vacías.
    /// </para>
    /// <para>
    /// Justo la caja que ocupa, sin margen. Un margen se lleva sitio en la tira de miniaturas
    /// para no enseñar nada: cuando un plano se saca del grupo el lienzo ya crece con él, así
    /// que no hace falta dejarle hueco por si acaso.
    /// </para>
    /// <para>
    /// La caja incluye siempre el sprite sin desplazar aunque no haya ningún plano ahí. Es lo
    /// que hace que un grupo con todo corrido a la derecha se vea corrido: sin eso, un plano
    /// solo desplazado diez pixeles saldría igual que uno sin desplazar.
    /// </para>
    /// </remarks>
    public static (int Width, int Height, int OriginX, int OriginY) CanvasOf(SpriteGroup group)
    {
        int left = 0;
        int top = 0;
        int right = SpriteRow.Columns;
        int bottom = Sprite.Rows;

        foreach (SpriteGroupMember member in group.Members)
        {
            left = Math.Min(left, member.OffsetX);
            top = Math.Min(top, member.OffsetY);
            right = Math.Max(right, member.OffsetX + SpriteRow.Columns);
            bottom = Math.Max(bottom, member.OffsetY + Sprite.Rows);
        }

        return (right - left, bottom - top, -left, -top);
    }

    public static ImageMini CreatePreview(SpriteGroup group)
    {
        (int width, int height, _, _) = CanvasOf(group);

        return new ImageMini(width, height);
    }

    public static void Render(
        SpriteGroup group,
        SpriteBank bank,
        ColorPalette palette,
        Color background,
        ImageMini target)
    {
        (int width, int height, int originX, int originY) = CanvasOf(group);

        int[] indices = new int[width * height];
        Array.Fill(indices, Empty);

        // Los patrones se resuelven una vez: la composición recorre líneas de pantalla,
        // no miembros, porque la regla de CC depende de qué hay en cada línea.
        List<(SpriteGroupMember Member, Sprite Pattern)> members =
        [
            .. group.Members
                .Where(member => (uint)member.PatternIndex < (uint)bank.SpritesList.Count)
                .Select(member => (member, bank.SpritesList[member.PatternIndex])),
        ];

        for (int canvasY = 0; canvasY < height; canvasY++)
            ComposeLine(indices, members, canvasY, width, originX, originY);

        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i];

            Color color = index == Empty
                ? background
                : SpriteRenderer.ResolveRowColor(palette, index, background);

            target.SetPixel(i % width, i / width, color);
        }
    }

    private static void ComposeLine(
        int[] indices,
        List<(SpriteGroupMember Member, Sprite Pattern)> members,
        int canvasY,
        int width,
        int originX,
        int originY)
    {
        // ¿Ha aparecido ya, en esta línea, un sprite de mayor prioridad con CC a 0? Es
        // lo que habilita a los de CC. Se mira sólo hacia atrás, así que un CC en el
        // primer miembro nunca llega a dibujarse: no hay nadie por delante.
        bool enabledByHigherPriority = false;

        foreach ((SpriteGroupMember member, Sprite pattern) in members)
        {
            int row = canvasY - originY - member.OffsetY;
            if ((uint)row >= Sprite.Rows)
                continue;

            SpriteAttributeRow attributes = member.Rows[row];

            if (!attributes.CombineColor)
            {
                if (member.IsVisible)
                    DrawLine(indices, pattern, member, row, canvasY, attributes.Color, false, width, originX);

                // Basta con que su línea caiga aquí, aunque no pinte ningún pixel. Y
                // sigue habilitando aunque esté oculto: ocultar un plano para mirar los
                // demás no puede hacer que a los demás se les caigan sus líneas con CC,
                // porque entonces no estarías viendo los demás, estarías viendo otra cosa.
                enabledByHigherPriority = true;

                continue;
            }

            if (enabledByHigherPriority && member.IsVisible)
                DrawLine(indices, pattern, member, row, canvasY, attributes.Color, true, width, originX);
        }
    }

    private static void DrawLine(
        int[] indices,
        Sprite pattern,
        SpriteGroupMember member,
        int row,
        int canvasY,
        int color,
        bool combine,
        int width,
        int originX)
    {
        SpriteRow patternRow = pattern.ArraySpriteRows[row];

        for (int column = 0; column < SpriteRow.Columns; column++)
        {
            if (!patternRow.ArrayColumns[column])
                continue;

            int canvasX = originX + member.OffsetX + column;
            if ((uint)canvasX >= (uint)width)
                continue;

            int offset = (canvasY * width) + canvasX;

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
