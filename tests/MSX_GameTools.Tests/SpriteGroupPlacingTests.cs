using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;
using static MSX_GameTools.Tests.SpriteCanvasHarness;

namespace MSX_GameTools.Tests;

/// <summary>
/// Los planos de un grupo se colocan sobre la composición, con el ratón y con los cursores.
/// </summary>
/// <remarks>
/// Antes sólo con cuatro botones de uno en uno. Montar una figura de dos sprites de alto son
/// dieciséis pulsaciones para bajar el de abajo a su sitio, mirando un número en vez de la
/// figura.
/// </remarks>
public class SpriteGroupPlacingTests
{
    /// <summary>Arrastrar sobre la miniatura coloca el plano seleccionado.</summary>
    [AvaloniaFact]
    public void Arrastrar_en_la_miniatura_coloca_el_plano()
    {
        using var editor = Grouped();

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        SpriteGroupMember member = group.SelectedMember!;

        Assert.Equal((0, 0), (member.OffsetX, member.OffsetY));

        Panel preview = editor.GroupPreview(0);
        double scale = editor.View.GroupPixelSize;

        Point from = editor.PointIn(preview, 8, 8);

        editor.PressAt(from);
        editor.MoveAt(new Point(from.X + (3 * scale), from.Y + (2 * scale)));
        editor.ReleaseAt(new Point(from.X + (3 * scale), from.Y + (2 * scale)));

        Assert.Equal((3, 2), (member.OffsetX, member.OffsetY));
    }

    /// <summary>
    /// Y los cursores rematan el ajuste, de pixel en pixel.
    /// </summary>
    /// <remarks>
    /// Después de arrastrar, que es cuando hace falta afinar y cuando la composición tiene el
    /// foco: en la lista de miembros los cursores eligen miembro, como en cualquier lista.
    /// </remarks>
    [AvaloniaFact]
    public void Los_cursores_rematan_el_ajuste()
    {
        using var editor = Grouped();

        SpriteGroupMember member = editor.ViewModel.SelectedGroup!.SelectedMember!;

        Panel preview = editor.GroupPreview(0);

        editor.PressAt(editor.PointIn(preview, 8, 8));
        editor.ReleaseAt(editor.PointIn(preview, 8, 8));

        editor.PressKey(Key.Right);
        editor.PressKey(Key.Right);
        editor.PressKey(Key.Down);

        Assert.Equal((2, 1), (member.OffsetX, member.OffsetY));

        editor.PressKey(Key.Left);
        editor.PressKey(Key.Up);

        Assert.Equal((1, 0), (member.OffsetX, member.OffsetY));
    }

    /// <summary>
    /// Sobre un grupo que no es el seleccionado no se arrastra nada.
    /// </summary>
    /// <remarks>
    /// Esa pulsación es la que lo selecciona. Moviendo además un plano, el usuario acabaría
    /// con un desplazamiento puesto en un grupo que ni siquiera estaba mirando.
    /// </remarks>
    [AvaloniaFact]
    public void Sobre_otro_grupo_la_pulsacion_solo_lo_selecciona()
    {
        using var editor = Grouped();

        SpriteGroupMember first = editor.ViewModel.SelectedGroup!.SelectedMember!;

        editor.ViewModel.AddGroupCommand.Execute(null);

        // El nuevo queda seleccionado, así que el de la primera ficha ya no lo está. Pero su
        // panel sigue recordando qué plano tenía elegido: sin esto, la prueba pasaría porque
        // no hay plano que mover y no porque el grupo no esté seleccionado.
        editor.ViewModel.Groups[0].SelectedMember = first;

        Panel preview = editor.GroupPreview(0);
        Point from = editor.PointIn(preview, 8, 8);
        double scale = editor.View.GroupPixelSize;

        editor.PressAt(from);
        editor.MoveAt(new Point(from.X + (5 * scale), from.Y));
        editor.ReleaseAt(new Point(from.X + (5 * scale), from.Y));

        Assert.Equal(0, first.OffsetX);
    }

    /// <summary>
    /// Y los dos desplazamientos se escriben, con la misma caja que el patrón.
    /// </summary>
    /// <remarks>
    /// El tope sale de lo que el miembro deja: escribir 200 en la caja no puede colocar un
    /// plano donde el fichero exportado no lo sabría contar.
    /// </remarks>
    [AvaloniaFact]
    public void Los_desplazamientos_se_escriben()
    {
        using var editor = Grouped();

        SpriteGroupMember member = editor.ViewModel.SelectedGroup!.SelectedMember!;

        NumericUpDown x = Box(editor, "MemberOffsetX");
        NumericUpDown y = Box(editor, "MemberOffsetY");

        Assert.Equal(SpriteGroupMember.MinOffset, x.Minimum);
        Assert.Equal(SpriteGroupMember.MaxOffset, x.Maximum);

        x.Value = 7;
        y.Value = -5;

        Assert.Equal((7, -5), (member.OffsetX, member.OffsetY));
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Una de las cajas del panel del grupo, por su nombre.</summary>
    private static NumericUpDown Box(SpriteCanvasHarness editor, string name) =>
        editor.GroupPanel.GetVisualDescendants()
            .OfType<NumericUpDown>()
            .Single(box => box.Name == name);

    /// <summary>Un editor en modo grupos, con un grupo y su plano seleccionados.</summary>
    private static SpriteCanvasHarness Grouped()
    {
        var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        return editor;
    }
}
