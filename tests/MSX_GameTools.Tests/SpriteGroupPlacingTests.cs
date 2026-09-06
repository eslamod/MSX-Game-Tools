using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
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

    /// <summary>
    /// Y dejar una caja vacía se dice en cristiano.
    /// </summary>
    /// <remarks>
    /// Sin plantilla, Avalonia enseña el texto de la excepción del enlace —«Could not convert
    /// '(null)' (null) to System.Int32»—, que no le dice nada a quien está montando un
    /// personaje. Y es lo único que estas cajas pueden dar: lo que se sale de los topes lo
    /// recorta el propio control.
    /// </remarks>
    [AvaloniaFact]
    public void Vaciar_una_caja_lo_dice_en_cristiano()
    {
        using var editor = Grouped();

        NumericUpDown x = Box(editor, "MemberOffsetX");

        x.Value = null;
        Dispatcher.UIThread.RunJobs();

        string[] said = [.. editor.View.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty)];

        Assert.Contains(Localizer.Instance["FormNumberNeeded"], said);
        Assert.DoesNotContain(said, text => text.Contains("Convert", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Colocar un plano se deshace, y el arrastre entero es un solo paso.
    /// </summary>
    /// <remarks>
    /// Un arrastre deja decenas de cambios por el camino; deshacerlos de uno en uno serían
    /// treinta veces deshacer para volver donde estabas.
    /// </remarks>
    [AvaloniaFact]
    public void Arrastrar_un_plano_es_un_solo_paso()
    {
        using var editor = Grouped();

        SpriteGroupMember member = editor.ViewModel.SelectedGroup!.SelectedMember!;

        Panel preview = editor.GroupPreview(0);
        double scale = editor.View.GroupPixelSize;

        Point from = editor.PointIn(preview, 8, 8);

        editor.PressAt(from);

        for (int step = 1; step <= 4; step++)
            editor.MoveAt(new Point(from.X + (step * scale), from.Y + (step * scale)));

        editor.ReleaseAt(new Point(from.X + (4 * scale), from.Y + (4 * scale)));

        Assert.Equal((4, 4), (member.OffsetX, member.OffsetY));

        // Y pulsar sin llegar a mover no deja paso, igual que un clic que no pinta.
        editor.PressAt(from);
        editor.ReleaseAt(from);

        editor.ViewModel.UndoDrawingCommand.Execute(null);

        Assert.Equal((0, 0), (member.OffsetX, member.OffsetY));
        Assert.False(editor.ViewModel.CanUndoDrawing);
    }

    /// <summary>Y los cursores y las cajas dejan un paso por cambio.</summary>
    [AvaloniaFact]
    public void Los_ajustes_sueltos_se_deshacen_uno_a_uno()
    {
        using var editor = Grouped();

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;
        SpriteGroupMember member = group.SelectedMember!;

        group.NudgeOffsetCommand.Execute("right");
        group.NudgeOffsetCommand.Execute("down");

        // Por la caja y no por el plano: es lo que hace la ventana, y es donde se anota.
        group.MemberPattern = 5;

        Assert.Equal((1, 1, 5), (member.OffsetX, member.OffsetY, member.PatternIndex));

        editor.ViewModel.UndoDrawingCommand.Execute(null);

        Assert.Equal(0, member.PatternIndex);

        editor.ViewModel.UndoDrawingCommand.Execute(null);
        editor.ViewModel.UndoDrawingCommand.Execute(null);

        Assert.Equal((0, 0), (member.OffsetX, member.OffsetY));
    }

    /// <summary>
    /// Y lo que se dibujó antes sigue ahí: colocar un plano ya no tira la historia.
    /// </summary>
    /// <remarks>
    /// Era lo que hacía que deshacer pareciera no responder: cualquier cosa que no pasara por
    /// la pila la vaciaba, y colocar planos es lo que más se hace mientras se monta un grupo.
    /// </remarks>
    [AvaloniaFact]
    public void Colocar_un_plano_no_se_lleva_por_delante_lo_dibujado()
    {
        using var editor = Grouped();

        editor.ViewModel.PixelSurface.Set(1, 1, true);
        editor.ViewModel.PixelSurface.EndStroke();

        editor.ViewModel.SelectedGroup!.MemberOffsetX = 6;

        // Un paso para el desplazamiento y otro para el trazo.
        editor.ViewModel.UndoDrawingCommand.Execute(null);
        editor.ViewModel.UndoDrawingCommand.Execute(null);

        Assert.False(editor.ViewModel.PixelSurface.IsSet(1, 1));
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
