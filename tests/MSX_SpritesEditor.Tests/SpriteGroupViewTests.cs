using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;
using Xunit;
using static MSX_SpritesEditor.Tests.SpriteCanvasHarness;

namespace MSX_SpritesEditor.Tests;

/// <summary>El conmutador Patrones/Grupos y el panel del grupo, con la vista montada.</summary>
public class SpriteGroupViewTests
{
    [AvaloniaFact]
    public void El_editor_arranca_en_modo_patrones()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        Assert.True(editor.ViewModel.ShowsPatterns);
        Assert.True(editor.Thumbnails.IsVisible);
        Assert.False(editor.GroupList.IsVisible);
        Assert.False(editor.GroupPanel.IsVisible);
        Assert.True(editor.RowColorStrip.IsVisible);
    }

    [AvaloniaFact]
    public void Al_pasar_a_grupos_cambia_lo_que_se_ve()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.SetThumbnailMode(ThumbnailMode.Groups);

        Assert.False(editor.Thumbnails.IsVisible);
        Assert.True(editor.GroupList.IsVisible);
        Assert.True(editor.GroupPanel.IsVisible);

        // La columna de colores es del patron, no del grupo.
        Assert.False(editor.RowColorStrip.IsVisible);
    }

    [AvaloniaFact]
    public void Crear_un_grupo_usa_el_sprite_actual_y_lo_deja_seleccionado()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.ViewModel.AddSpriteCommand.Execute(null); // el 2 pasa a ser el actual
        editor.SetThumbnailMode(ThumbnailMode.Groups);

        editor.ViewModel.AddGroupCommand.Execute(null);

        Assert.Single(editor.Bank.Groups);
        Assert.NotNull(editor.ViewModel.SelectedGroup);
        Assert.Equal(1, editor.ViewModel.SelectedGroup.Group.Members[0].PatternIndex);
        Assert.Equal(1, editor.GroupList.ItemCount);
    }

    [AvaloniaFact]
    public void Las_flechas_mueven_el_desplazamiento_del_miembro_seleccionado()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        group.NudgeOffsetCommand.Execute("right");
        group.NudgeOffsetCommand.Execute("right");
        group.NudgeOffsetCommand.Execute("down");

        Assert.Equal(2, group.SelectedMember!.OffsetX);
        Assert.Equal(1, group.SelectedMember.OffsetY);
    }

    [AvaloniaFact]
    public void Anadir_un_miembro_repite_el_patron_del_seleccionado()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.ViewModel.AddSpriteCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        group.AddMemberCommand.Execute(null);

        // Mismo patron con un desplazamiento pequeno es la tecnica del contorno.
        Assert.Equal(2, group.Group.Members.Count);
        Assert.Equal(1, group.Group.Members[1].PatternIndex);
        Assert.Same(group.Group.Members[1], group.SelectedMember);
    }

    [AvaloniaFact]
    public void El_boton_de_anadir_miembro_se_deshabilita_en_el_cuarto()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        while (group.AddMemberCommand.CanExecute(null))
            group.AddMemberCommand.Execute(null);

        Assert.Equal(SpriteGroup.MaxMembers, group.Group.Members.Count);
    }

    [AvaloniaFact]
    public void La_miniatura_del_grupo_se_rehace_al_mover_un_offset()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.Bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;
        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        int before = PixelReader.At(group.Preview, SpriteGroupMember.MaxOffset, SpriteGroupMember.MaxOffset);

        group.NudgeOffsetCommand.Execute("right");

        int after = PixelReader.At(group.Preview, SpriteGroupMember.MaxOffset, SpriteGroupMember.MaxOffset);
        int moved = PixelReader.At(group.Preview, SpriteGroupMember.MaxOffset + 1, SpriteGroupMember.MaxOffset);

        Assert.NotEqual(before, after);
        Assert.Equal(before, moved);
    }

    [AvaloniaFact]
    public void La_miniatura_del_grupo_es_de_46x46()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        ImageMini preview = editor.ViewModel.SelectedGroup!.Preview;

        Assert.Equal(SpriteGroupRenderer.PreviewSize, preview.Width);
        Assert.Equal(SpriteGroupRenderer.PreviewSize, preview.Height);
    }

    // Estar en modo Grupos no impide seguir dibujando: el lienzo sigue al patrón del
    // miembro que estés tocando, para poder ver el efecto en la composición.

    [AvaloniaFact]
    public void Seleccionar_un_miembro_lleva_el_lienzo_a_su_patron()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.ViewModel.AddSpriteCommand.Execute(null);
        editor.ViewModel.AddSpriteCommand.Execute(null); // 3 patrones

        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);  // miembro sobre el patron 2

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        group.AddMemberCommand.Execute(null);
        group.Group.Members[1].PatternIndex = 0;

        group.SelectedMember = group.Group.Members[0];
        Assert.Equal(3, editor.ViewModel.CurrentSpritePosition);

        group.SelectedMember = group.Group.Members[1];
        Assert.Equal(1, editor.ViewModel.CurrentSpritePosition);
    }

    [AvaloniaFact]
    public void Cambiar_el_patron_del_miembro_lleva_el_lienzo_a_ese_patron()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.ViewModel.AddSpriteCommand.Execute(null);

        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        Assert.Equal(2, editor.ViewModel.CurrentSpritePosition);

        group.StepPatternCommand.Execute("-1");

        Assert.Equal(0, group.SelectedMember!.PatternIndex);
        Assert.Equal(1, editor.ViewModel.CurrentSpritePosition);
    }

    [AvaloniaFact]
    public void Pasar_a_modo_grupos_lleva_el_lienzo_al_patron_del_miembro()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        editor.SetThumbnailMode(ThumbnailMode.Patterns);
        editor.ViewModel.AddSpriteCommand.Execute(null); // el lienzo se va al patron 2

        editor.SetThumbnailMode(ThumbnailMode.Groups);

        // Vuelve al del miembro seleccionado, no se queda donde lo dejaste.
        Assert.Equal(1, editor.ViewModel.CurrentSpritePosition);
    }

    [AvaloniaFact]
    public void Dibujar_en_modo_grupos_actualiza_la_miniatura_del_grupo()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        int before = PixelReader.At(group.Preview, SpriteGroupMember.MaxOffset, SpriteGroupMember.MaxOffset);

        // Un trazo de verdad sobre el lienzo, con su pulsar y soltar.
        editor.Press(0, 0);
        editor.Release(0, 0);

        int after = PixelReader.At(group.Preview, SpriteGroupMember.MaxOffset, SpriteGroupMember.MaxOffset);

        Assert.True(editor.Bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0]);
        Assert.NotEqual(before, after);
    }

    [AvaloniaFact]
    public void La_barra_de_la_tira_no_se_mueve_al_cambiar_de_modo()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        Point inPatterns = editor.ModeToggleOrigin();
        double heightInPatterns = editor.ToolbarHeight;

        editor.SetThumbnailMode(ThumbnailMode.Groups);

        // Ni se desplaza a la derecha al aparecer el panel del grupo, ni crece de alto
        // al aparecer los botones de anadir y eliminar.
        Assert.Equal(inPatterns, editor.ModeToggleOrigin());
        Assert.Equal(heightInPatterns, editor.ToolbarHeight);
    }

    [AvaloniaFact]
    public void La_fila_del_miembro_separa_el_patron_de_los_desplazamientos()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        group.NudgeOffsetCommand.Execute("right");
        group.NudgeOffsetCommand.Execute("up");
        SpriteCanvasHarness.Pump();

        ListBox members = editor.MemberList;
        Control container = members.ContainerFromIndex(0)!;
        string row = string.Concat(container.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));

        Assert.Equal("Patrón 0 ; x:1 ; y:-1", row);
    }

    [AvaloniaFact]
    public async Task Eliminar_un_grupo_pide_confirmacion()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = false };
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2, dialogs);

        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        await editor.ViewModel.DeleteGroupCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ConfirmCalls);
        Assert.Contains("Group 1", dialogs.LastConfirmMessage);
        Assert.Single(editor.Bank.Groups);
    }

    [AvaloniaFact]
    public async Task Eliminar_el_grupo_seleccionado_no_deja_la_seleccion_vacia()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.SetThumbnailMode(ThumbnailMode.Groups);

        editor.ViewModel.AddGroupCommand.Execute(null);
        editor.ViewModel.AddGroupCommand.Execute(null);
        SpriteCanvasHarness.Pump();

        await editor.ViewModel.DeleteGroupCommand.ExecuteAsync(null);
        SpriteCanvasHarness.Pump();

        Assert.Single(editor.Bank.Groups);
        Assert.NotNull(editor.ViewModel.SelectedGroup);
        Assert.Same(editor.ViewModel.SelectedGroup, editor.GroupList.SelectedItem);
    }
}
