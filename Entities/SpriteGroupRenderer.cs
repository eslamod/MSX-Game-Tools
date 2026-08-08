using Avalonia.Media;

namespace MSX_SpritesEditor.Entities;

/// <summary>Compone en una imagen los sprites de un grupo con sus desplazamientos.</summary>
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

        // Los miembros se recorren de mayor a menor prioridad, como los planos.
        foreach (SpriteGroupMember member in group.Members)
        {
            if ((uint)member.PatternIndex >= (uint)bank.SpritesList.Count)
                continue;

            Compose(indices, bank.SpritesList[member.PatternIndex], member);
        }

        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i];

            Color color = index == Empty
                ? background
                : SpriteRenderer.ResolveRowColor(palette, index, background);

            target.SetPixel(i % PreviewSize, i / PreviewSize, color);
        }
    }

    private static void Compose(int[] indices, Sprite pattern, SpriteGroupMember member)
    {
        for (int y = 0; y < Sprite.Rows; y++)
        {
            int canvasY = Origin + member.OffsetY + y;
            if ((uint)canvasY >= PreviewSize)
                continue;

            SpriteRow row = pattern.ArraySpriteRows[y];
            SpriteAttributeRow attributes = member.Rows[y];

            for (int x = 0; x < SpriteRow.Columns; x++)
            {
                if (!row.ArrayColumns[x])
                    continue;

                int canvasX = Origin + member.OffsetX + x;
                if ((uint)canvasX >= PreviewSize)
                    continue;

                int offset = (canvasY * PreviewSize) + canvasX;

                if (indices[offset] == Empty)
                {
                    indices[offset] = attributes.Color;
                }
                else if (attributes.CombineColor)
                {
                    // CC: el V9938 hace OR de los códigos de color de 4 bits, no del RGB.
                    indices[offset] |= attributes.Color;
                }

                // Sin CC gana el de mayor prioridad, que ya está escrito.
            }
        }
    }
}
