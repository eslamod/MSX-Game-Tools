using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Ocultar un plano del grupo para poder mirar los demás.
/// </summary>
/// <remarks>
/// Lo delicado es el bit CC: una línea con CC sólo se dibuja si algún sprite anterior
/// con CC a 0 cubre esa misma línea. Si ocultar un plano lo sacara del todo de la
/// composición, al ocultar el plano habilitador desaparecerían también líneas de los
/// otros, y estarías mirando algo que no es «los demás planos».
/// </remarks>
public class GroupMemberVisibilityTests
{
    [AvaloniaFact]
    public void Un_plano_oculto_no_pinta_sus_pixeles()
    {
        (SpriteBank bank, SpriteGroup group) = TwoMemberGroup();

        // El de arriba tapa al de abajo en toda la línea 0.
        SetLine(group.Members[0], row: 0, color: 4, combine: false);
        SetLine(group.Members[1], row: 0, color: 8, combine: false);

        Assert.Equal(4, ComposeAt(bank, group, 0, 0));

        group.Members[0].IsVisible = false;

        // Al quitar el de mayor prioridad se ve el que había debajo.
        Assert.Equal(8, ComposeAt(bank, group, 0, 0));
    }

    /// <summary>
    /// La parte que sí tiene arreglo: un plano oculto sigue habilitando a los CC de
    /// detrás. Si no, ocultar el plano 0 haría desaparecer líneas del plano 1.
    /// </summary>
    [AvaloniaFact]
    public void Un_plano_oculto_sigue_habilitando_las_lineas_con_CC_de_los_de_detras()
    {
        (SpriteBank bank, SpriteGroup group) = TwoMemberGroup();

        SetLine(group.Members[0], row: 0, color: 4, combine: false);
        SetLine(group.Members[1], row: 0, color: 8, combine: true);

        // Con los dos visibles, el CC combina con OR sobre el de mayor prioridad.
        Assert.Equal(4 | 8, ComposeAt(bank, group, 0, 0));

        group.Members[0].IsVisible = false;

        // Sigue viéndose: lo que se oculta son sus pixeles, no su papel de habilitador.
        Assert.Equal(8, ComposeAt(bank, group, 0, 0));
    }

    [AvaloniaFact]
    public void Sin_habilitador_visible_ni_oculto_la_linea_con_CC_no_se_dibuja()
    {
        (SpriteBank bank, SpriteGroup group) = TwoMemberGroup();

        // Los dos con CC: no hay ninguno con CC a 0 que habilite, así que no se pinta
        // nada. Es la regla del manual, y ocultar no tiene nada que ver aquí.
        SetLine(group.Members[0], row: 0, color: 4, combine: true);
        SetLine(group.Members[1], row: 0, color: 8, combine: true);

        Assert.Equal(-1, ComposeAt(bank, group, 0, 0));
    }

    [AvaloniaFact]
    public void Un_plano_arranca_visible()
        => Assert.True(TwoMemberGroup().Group.Members[0].IsVisible);

    /// <summary>Un banco con dos patrones macizos y un grupo que usa los dos.</summary>
    private static (SpriteBank Bank, SpriteGroup Group) TwoMemberGroup()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        foreach (Sprite pattern in bank.SpritesList)
        {
            foreach (SpriteRow row in pattern.ArraySpriteRows)
                Array.Fill(row.ArrayColumns, true);
        }

        SpriteGroup group = bank.NewGroup(0)!;
        group.Add(new SpriteGroupMember(1, bank.SpritesList[1]));

        // Todo apagado salvo lo que ponga cada test.
        foreach (SpriteGroupMember member in group.Members)
        {
            foreach (SpriteAttributeRow row in member.Rows)
            {
                row.Color = 0;
                row.CombineColor = false;
            }
        }

        return (bank, group);
    }

    private static void SetLine(SpriteGroupMember member, int row, int color, bool combine)
    {
        member.Rows[row].Color = color;
        member.Rows[row].CombineColor = combine;
    }

    /// <summary>
    /// Índice de color compuesto en un pixel del patrón; -1 si no se pinta.
    /// </summary>
    /// <remarks>
    /// En coordenadas del patrón y no del lienzo: dónde cae el desplazamiento cero sale de lo
    /// que ocupa el grupo, así que fijarlo a un número aquí ataría la prueba a un tamaño de
    /// miniatura que no es lo que se está comprobando.
    /// </remarks>
    private static int ComposeAt(SpriteBank bank, SpriteGroup group, int x, int y)
    {
        ImageMini preview = SpriteGroupRenderer.CreatePreview(group);
        (_, _, int originX, int originY) = SpriteGroupRenderer.CanvasOf(group);

        x += originX;
        y += originY;
        var palette = ColorPalette.CreateMsxStandard();

        // Cada indice a un color distinto y reconocible: el propio indice en el rojo.
        SpriteGroupRenderer.Render(group, bank, palette, Avalonia.Media.Colors.Transparent, preview);

        int bgra = PixelReader.At(preview, x, y);

        if ((uint)bgra >> 24 == 0)
            return -1;

        var color = Avalonia.Media.Color.FromRgb(
            (byte)((bgra >> 16) & 0xFF), (byte)((bgra >> 8) & 0xFF), (byte)(bgra & 0xFF));

        for (int index = 0; index < ColorPalette.Size; index++)
        {
            if (palette.GetColor(index) == color)
                return index;
        }

        return -2;
    }
}
