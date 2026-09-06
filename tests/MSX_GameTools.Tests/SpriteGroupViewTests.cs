using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;
using static MSX_GameTools.Tests.SpriteCanvasHarness;

namespace MSX_GameTools.Tests;

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
        editor.ViewModel.NextSpriteCommand.Execute(null); // el 2 pasa a ser el actual
        editor.SetThumbnailMode(ThumbnailMode.Groups);

        editor.ViewModel.AddGroupCommand.Execute(null);

        Assert.Single(editor.Bank.Groups);
        Assert.NotNull(editor.ViewModel.SelectedGroup);
        Assert.Equal(1, editor.ViewModel.SelectedGroup.Group.Members[0].PatternIndex);
        Assert.Equal(1, editor.GroupList.ItemCount);
    }

    /// <summary>Un empujón mueve el plano un pixel, que es lo que hacen los cursores.</summary>
    [AvaloniaFact]
    public void Empujar_mueve_el_desplazamiento_del_miembro_seleccionado()
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
        editor.ViewModel.NextSpriteCommand.Execute(null);
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
        int before = PixelReader.At(group.Preview, 0, 0);

        group.NudgeOffsetCommand.Execute("right");

        int after = PixelReader.At(group.Preview, 0, 0);
        int moved = PixelReader.At(group.Preview, 1, 0);

        Assert.NotEqual(before, after);
        Assert.Equal(before, moved);
    }

    /// <summary>
    /// Un grupo recién creado ocupa justo un sprite.
    /// </summary>
    /// <remarks>
    /// El lienzo sale de lo que ocupa el grupo, así que el caso corriente -un plano sin
    /// desplazar- es el sprite pelado. Antes eran 46x46 fijos y la miniatura iba casi vacía.
    /// </remarks>
    [AvaloniaFact]
    public void La_miniatura_de_un_grupo_recien_creado_es_un_sprite()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        ImageMini preview = editor.ViewModel.SelectedGroup!.Preview;

        Assert.Equal(SpriteGroupRenderer.NominalSize, preview.Width);
        Assert.Equal(SpriteGroupRenderer.NominalSize, preview.Height);
    }

    // Estar en modo Grupos no impide seguir dibujando: el lienzo sigue al patrón del
    // miembro que estés tocando, para poder ver el efecto en la composición.

    [AvaloniaFact]
    public void Seleccionar_un_miembro_lleva_el_lienzo_a_su_patron()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.ViewModel.NextSpriteCommand.Execute(null);
        editor.ViewModel.NextSpriteCommand.Execute(null); // 3 patrones

        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);  // miembro sobre el patron 2

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        group.AddMemberCommand.Execute(null);
        group.Group.Members[1].PatternIndex = 0;

        group.SelectedMember = group.Group.Members[0];
        Assert.Equal(2, editor.ViewModel.CurrentSpriteIndex);

        group.SelectedMember = group.Group.Members[1];
        Assert.Equal(0, editor.ViewModel.CurrentSpriteIndex);
    }

    /// <summary>
    /// El número de patrón del miembro se escribe, y al cambiarlo el lienzo va a ese patrón.
    /// </summary>
    /// <remarks>
    /// Por la caja y no por el modelo de vista: antes eran dos botones de uno en uno, y en un
    /// banco de 64 llegar al 57 eran cincuenta y siete clics. Que se pueda escribir depende
    /// del enlace, que es lo que esto comprueba de paso.
    /// </remarks>
    [AvaloniaFact]
    public void Cambiar_el_patron_del_miembro_lleva_el_lienzo_a_ese_patron()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.ViewModel.NextSpriteCommand.Execute(null);

        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        Assert.Equal(1, editor.ViewModel.CurrentSpriteIndex);

        NumericUpDown box = editor.GroupPanel.GetVisualDescendants()
            .OfType<NumericUpDown>()
            .Single(control => control.Name == "MemberPattern");

        // Y no deja escribir un patrón que el banco no tiene.
        Assert.Equal(0, box.Minimum);
        Assert.Equal(group.MaxPatternIndex, box.Maximum);

        box.Value = 0;

        Assert.Equal(0, group.SelectedMember!.PatternIndex);
        Assert.Equal(0, editor.ViewModel.CurrentSpriteIndex);
    }

    [AvaloniaFact]
    public void Pasar_a_modo_grupos_lleva_el_lienzo_al_patron_del_miembro()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        editor.SetThumbnailMode(ThumbnailMode.Patterns);
        editor.ViewModel.NextSpriteCommand.Execute(null); // el lienzo se va al patron 2

        editor.SetThumbnailMode(ThumbnailMode.Groups);

        // Vuelve al del miembro seleccionado, no se queda donde lo dejaste.
        Assert.Equal(0, editor.ViewModel.CurrentSpriteIndex);
    }

    [AvaloniaFact]
    public void Dibujar_en_modo_grupos_actualiza_la_miniatura_del_grupo()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        int before = PixelReader.At(group.Preview, 0, 0);

        // Un trazo de verdad sobre el lienzo, con su pulsar y soltar.
        editor.Press(0, 0);
        editor.Release(0, 0);

        int after = PixelReader.At(group.Preview, 0, 0);

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

    /// <summary>
    /// Y dice qué animaciones lo usan, si alguna lo usa.
    /// </summary>
    /// <remarks>
    /// Borrarlo no las corrige ni las rompe en silencio: se quedan apuntando a un número que ya
    /// no existe. En el editor se ve —la vista previa se queda en blanco— y al exportar se avisa
    /// otra vez, pero el momento de decirlo es antes de borrar, que es cuando aún se puede.
    /// </remarks>
    [AvaloniaFact]
    public async Task Eliminar_un_grupo_dice_que_animaciones_lo_usan()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = false };
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2, dialogs);

        editor.ViewModel.AddGroupCommand.Execute(null);

        int number = editor.ViewModel.SelectedGroup!.Group.Id;

        editor.ViewModel.AddAnimationCommand.Execute(null);

        SpriteAnimationViewModel animation = editor.ViewModel.SelectedAnimation!;

        animation.Name = "Andar";
        animation.Kind = AnimationKind.Groups;
        animation.AddFrameCommand.Execute(null);
        animation.Steps[0].Target = number;

        await editor.ViewModel.DeleteGroupCommand.ExecuteAsync(null);

        Assert.Contains("Andar", dialogs.LastConfirmMessage);
    }

    /// <summary>Pero no lo menciona cuando no lo usa ninguna.</summary>
    [AvaloniaFact]
    public async Task Eliminar_un_grupo_que_no_usa_nadie_no_habla_de_animaciones()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = false };
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2, dialogs);

        editor.ViewModel.AddGroupCommand.Execute(null);
        editor.ViewModel.AddAnimationCommand.Execute(null);

        SpriteAnimationViewModel animation = editor.ViewModel.SelectedAnimation!;

        animation.Name = "Andar";
        animation.Kind = AnimationKind.Groups;

        // Un fotograma que apunta a otro número: la animación existe, pero no usa este grupo.
        animation.AddFrameCommand.Execute(null);
        animation.Steps[0].Target = editor.ViewModel.SelectedGroup!.Group.Id + 1;

        await editor.ViewModel.DeleteGroupCommand.ExecuteAsync(null);

        Assert.DoesNotContain("Andar", dialogs.LastConfirmMessage);
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

    /// <summary>
    /// El nombre de cada grupo cae a la misma altura, midan lo que midan las miniaturas.
    /// </summary>
    /// <remarks>
    /// El lienzo de cada grupo sale de lo que ocupa, así que en una fila conviven fichas de
    /// alturas distintas y el WrapPanel le da a todas el alto de la más alta. Con el nombre
    /// pegado detrás de la imagen, el de los grupos bajos quedaba a media ficha y a distinta
    /// altura en cada uno; va abajo del todo para que se lean en línea.
    /// </remarks>
    [AvaloniaFact]
    public void Los_nombres_de_los_grupos_van_todos_a_la_misma_altura()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.SetThumbnailMode(ThumbnailMode.Groups);

        // Uno corriente y otro con un plano sacado hacia abajo, que mide bastante más.
        editor.ViewModel.AddGroupCommand.Execute(null);
        editor.ViewModel.AddGroupCommand.Execute(null);

        SpriteGroupViewModel tall = editor.ViewModel.Groups[1];
        tall.Group.Members[0].OffsetY = 20;

        Pump();

        List<TextBlock> names =
        [
            .. editor.GroupList.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(text => editor.ViewModel.Groups.Any(group => group.Group.Name == text.Text)),
        ];

        Assert.Equal(2, names.Count);

        // Las dos fichas miden lo mismo -las estira el WrapPanel-, así que basta con que el
        // nombre acabe a la misma distancia del fondo de su ficha.
        double[] bottoms =
        [
            .. names.Select(name =>
            {
                Control card = name.GetVisualAncestors().OfType<DockPanel>().First();

                return card.Bounds.Height
                    - (name.TranslatePoint(new Point(0, name.Bounds.Height), card)?.Y ?? -1);
            }),
        ];

        // Pegados al fondo de su ficha, y no sólo a la misma altura: las fichas de una fila
        // ya miden todas lo mismo, así que «a la misma altura» se cumple pongas el nombre
        // donde lo pongas, también arriba del todo.
        Assert.All(bottoms, bottom => Assert.True(bottom < 1, $"el nombre acaba a {bottom} del fondo."));
    }

    /// <summary>
    /// La miniatura va centrada en su ficha.
    /// </summary>
    /// <remarks>
    /// Con el zoom bajo, el nombre del grupo es más ancho que el dibujo y es él quien decide
    /// lo que mide la ficha. Sin centrar, el dibujo se quedaba pegado a un lado y la tira se
    /// leía en zigzag.
    /// </remarks>
    [AvaloniaFact]
    public void La_miniatura_va_centrada_en_su_ficha()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);
        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        // Con un nombre largo, que es cuando se nota: un grupo corriente mide mas que su
        // nombre y entonces la ficha es justo la miniatura y no hay nada que centrar.
        editor.ViewModel.Groups[0].Group.Name = "Enemigo volador de tres planos";
        Pump();

        Image thumbnail = editor.GroupList.GetVisualDescendants()
            .OfType<Image>()
            .First(image => image.Bounds.Width > 0);

        Control card = thumbnail.GetVisualAncestors().OfType<DockPanel>().First();

        // Un grupo recién creado mide un sprite, y el nombre ocupa más: si no fuera así el
        // centrado no se notaría y la prueba pasaría sin comprobar nada.
        Assert.True(card.Bounds.Width > thumbnail.Bounds.Width, "el nombre no ensancha la ficha.");

        double left = thumbnail.TranslatePoint(new Point(0, 0), card)!.Value.X;

        Assert.Equal((card.Bounds.Width - thumbnail.Bounds.Width) / 2, left, 1);
    }
}
