using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;
using static MSX_GameTools.Tests.SpriteCanvasHarness;

namespace MSX_GameTools.Tests;

/// <summary>
/// Colores del grupo. Son del miembro, no del patrón: el patrón sólo fue la plantilla
/// de la que se sembraron, que es lo que permite el mismo dibujo con dos colores.
/// </summary>
public class SpriteGroupColorTests
{
    private const int Origin = 0;

    [AvaloniaFact]
    public void Las_casillas_siguen_al_miembro_seleccionado()
    {
        using var editor = NewEditorWithGroup(SpriteBank.SpriteType.MSX2);
        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        group.AddMemberCommand.Execute(null);
        group.Group.Members[0].Rows[3].Color = 6;
        group.Group.Members[1].Rows[3].Color = 9;

        group.SelectedMember = group.Group.Members[0];
        Assert.Equal(6, group.MemberColors[3].Color.Index);

        group.SelectedMember = group.Group.Members[1];
        Assert.Equal(9, group.MemberColors[3].Color.Index);
    }

    [AvaloniaFact]
    public void Elegir_un_color_afecta_solo_a_esa_linea_del_miembro()
    {
        using var editor = NewEditorWithGroup(SpriteBank.SpriteType.MSX2);
        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        group.MemberColors[3].PickCommand.Execute(editor.Palette[6]);

        Assert.Equal(6, group.SelectedMember!.Rows[3].Color);
        Assert.Equal(15, group.SelectedMember.Rows[4].Color);

        // Y el patron sigue como estaba: los colores son del miembro.
        Assert.Equal(15, editor.Bank.SpritesList[0].ArraySpriteRows[3].Color);
    }

    [AvaloniaFact]
    public void En_msx1_el_color_va_a_las_16_lineas_del_miembro()
    {
        using var editor = NewEditorWithGroup(SpriteBank.SpriteType.MSX);
        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        Assert.True(group.IsMsx1);
        group.PickMemberColorCommand.Execute(editor.Palette[10]);

        Assert.All(group.SelectedMember!.Rows, row => Assert.Equal(10, row.Color));
        Assert.Equal(10, group.MemberColor!.Index);
    }

    [AvaloniaFact]
    public void El_mismo_patron_puede_llevar_dos_colores_distintos()
    {
        using var editor = NewEditorWithGroup(SpriteBank.SpriteType.MSX2);
        editor.Bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        group.AddMemberCommand.Execute(null);

        // Mismo patron en los dos miembros, colores distintos y uno desplazado:
        // es la tecnica del contorno, y es la razon de que los colores vivan aqui.
        Assert.Equal(
            group.Group.Members[0].PatternIndex,
            group.Group.Members[1].PatternIndex);

        group.Group.Members[0].Rows[0].Color = 8;
        group.Group.Members[1].Rows[0].Color = 2;
        group.Group.Members[1].OffsetX = 1;

        int[] pixels = PixelReader.Read(group.Preview);

        // Desplazar un plano ensancha el lienzo, asi que el ancho y el origen salen del grupo.
        (int width, _, int originX, int originY) = SpriteGroupRenderer.CanvasOf(group.Group);

        Assert.Equal(
            PixelReader.Bgra(editor.Palette.GetColor(8)),
            pixels[(originY * width) + originX]);

        Assert.Equal(
            PixelReader.Bgra(editor.Palette.GetColor(2)),
            pixels[(originY * width) + originX + 1]);
    }

    [AvaloniaFact]
    public void Cambiar_un_color_del_miembro_recompone_la_miniatura()
    {
        using var editor = NewEditorWithGroup(SpriteBank.SpriteType.MSX2);
        editor.Bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        group.MemberColors[0].PickCommand.Execute(editor.Palette[6]);

        Assert.Equal(
            PixelReader.Bgra(editor.Palette.GetColor(6)),
            PixelReader.At(group.Preview, Origin, Origin));
    }

    [AvaloniaFact]
    public void Activar_CC_combina_los_colores_con_or()
    {
        using var editor = NewEditorWithGroup(SpriteBank.SpriteType.MSX2);
        editor.Bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        group.AddMemberCommand.Execute(null);

        group.Group.Members[0].Rows[0].Color = 1; // 0001
        group.Group.Members[1].Rows[0].Color = 4; // 0100

        // Sin CC gana el de mayor prioridad.
        Assert.Equal(
            PixelReader.Bgra(editor.Palette.GetColor(1)),
            PixelReader.At(group.Preview, Origin, Origin));

        group.Group.Members[1].Rows[0].CombineColor = true;

        // 0001 OR 0100 = 0101 = 5.
        Assert.Equal(
            PixelReader.Bgra(editor.Palette.GetColor(5)),
            PixelReader.At(group.Preview, Origin, Origin));
    }

    [AvaloniaFact]
    public void Restaurar_vuelve_a_sembrar_los_colores_del_patron()
    {
        using var editor = NewEditorWithGroup(SpriteBank.SpriteType.MSX2);
        editor.Bank.SpritesList[0].ArraySpriteRows[3].Color = 12;

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        group.MemberColors[3].PickCommand.Execute(editor.Palette[6]);
        Assert.Equal(6, group.SelectedMember!.Rows[3].Color);

        group.RestoreColorsCommand.Execute(null);

        Assert.Equal(12, group.SelectedMember.Rows[3].Color);
        Assert.Equal(12, group.MemberColors[3].Color.Index);
    }

    [AvaloniaFact]
    public void Cambiar_la_paleta_del_banco_actualiza_las_casillas_del_grupo()
    {
        using var editor = NewEditorWithGroup(SpriteBank.SpriteType.MSX2);
        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        group.MemberColors[3].PickCommand.Execute(editor.Palette[6]);

        ColorPalette other = editor.Palette.Clone("Otra");
        other[6].SetComponents(7, 0, 7);

        editor.ViewModel.ColorPalette = other;

        Assert.Same(other[6], group.MemberColors[3].Color);
    }

    // Los desplegables viven en un popup, fuera del arbol visual del boton que los
    // abre. Sus enlaces $parent solo se resuelven al abrirlo, y Button.OnClick lee su
    // Command despues de lanzar el evento Click: hay que pulsarlos de verdad.

    [AvaloniaFact]
    public void Pulsar_un_color_del_desplegable_de_la_linea_lo_aplica()
    {
        using var editor = NewEditorWithGroup(SpriteBank.SpriteType.MSX2);
        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        Button swatch = editor.MemberColorSwatch(3);
        var flyout = (Flyout)swatch.Flyout!;
        flyout.ShowAt(swatch);
        SpriteCanvasHarness.Pump();

        Button colorButton = ((ItemsControl)flyout.Content!)
            .GetVisualDescendants().OfType<Button>().ElementAt(6);

        SpriteCanvasHarness.ClickButton(colorButton);

        Assert.Equal(6, group.SelectedMember!.Rows[3].Color);
        Assert.Equal(15, group.SelectedMember.Rows[4].Color);
        Assert.False(flyout.IsOpen);
    }

    [AvaloniaFact]
    public void En_msx1_pulsar_un_color_lo_aplica_a_todo_el_miembro()
    {
        using var editor = NewEditorWithGroup(SpriteBank.SpriteType.MSX);
        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        Button swatch = editor.GroupMemberColorSwatch();
        var flyout = (Flyout)swatch.Flyout!;
        flyout.ShowAt(swatch);
        SpriteCanvasHarness.Pump();

        Button colorButton = ((ItemsControl)flyout.Content!)
            .GetVisualDescendants().OfType<Button>().ElementAt(10);

        SpriteCanvasHarness.ClickButton(colorButton);

        Assert.All(group.SelectedMember!.Rows, row => Assert.Equal(10, row.Color));
        Assert.False(flyout.IsOpen);
    }

    private static SpriteCanvasHarness NewEditorWithGroup(SpriteBank.SpriteType type)
    {
        var editor = new SpriteCanvasHarness(PaintMode.Drag, type);

        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        return editor;
    }
}
