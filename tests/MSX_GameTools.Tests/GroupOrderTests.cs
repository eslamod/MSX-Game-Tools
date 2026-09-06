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
/// Los grupos se cambian de sitio arrastrando su ficha, como los colores de la paleta.
/// </summary>
/// <remarks>
/// El orden importa: es el que se exporta y por el que el juego los direcciona. Lo que no se
/// rompe al moverlos son las animaciones, que apuntan al grupo por su identificador.
/// </remarks>
public class GroupOrderTests
{
    /// <summary>Mover un grupo lo mueve en las dos listas: la que se ve y la que se guarda.</summary>
    [AvaloniaFact]
    public void Mover_un_grupo_lo_cambia_de_sitio_en_las_dos_listas()
    {
        using var editor = ThreeGroups();

        SpriteGroupViewModel moved = editor.ViewModel.Groups[0];
        SpriteGroup inTheBank = moved.Group;

        editor.ViewModel.MoveGroup(0, 2);

        Assert.Equal(2, editor.ViewModel.Groups.IndexOf(moved));
        Assert.Equal(2, editor.Bank.Groups.IndexOf(inTheBank));
        Assert.True(editor.ViewModel.IsModified);

        // Y el cambio de orden se deshace, que también es un cambio del banco.
        editor.ViewModel.UndoDrawingCommand.Execute(null);

        Assert.Equal(0, editor.ViewModel.Groups.IndexOf(moved));
        Assert.Equal(0, editor.Bank.Groups.IndexOf(inTheBank));
    }

    /// <summary>
    /// Y el grupo movido sigue siendo el de delante.
    /// </summary>
    /// <remarks>
    /// Con la ventana montada porque es donde falla: el ListBox escribe null en su selección
    /// cuando el elemento que tenía se mueve de sitio, y con el modelo de vista suelto eso no
    /// se ve. Quien arrastra un grupo espera seguir trabajando con él.
    /// </remarks>
    [AvaloniaFact]
    public void El_grupo_movido_se_queda_seleccionado()
    {
        using var editor = ThreeGroups();

        SpriteGroupViewModel moved = editor.ViewModel.Groups[0];

        editor.ViewModel.SelectedGroup = moved;

        editor.ViewModel.MoveGroup(0, 2);

        Assert.Same(moved, editor.ViewModel.SelectedGroup);
        Assert.Same(moved, editor.GroupList.SelectedItem);
    }

    /// <summary>Pulsar una ficha la deja lista para arrastrarla.</summary>
    /// <remarks>
    /// El arrastre de verdad necesita sistema operativo debajo y no se puede simular aquí;
    /// lo que sí se puede comprobar es que la pulsación llega, que es la mitad que fallaba en
    /// la lista de la paleta por culpa del ListBoxItem.
    /// </remarks>
    [AvaloniaFact]
    public void Pulsar_una_ficha_la_deja_lista_para_arrastrar()
    {
        using var editor = ThreeGroups();

        SpriteGroupViewModel first = editor.ViewModel.Groups[0];

        // Por el centro: una ficha que no está seleccionada se arrastra entera, que sobre
        // ella no hay planos que colocar.
        ListBoxItem card = editor.GroupList.GetRealizedContainers()
            .OfType<ListBoxItem>()
            .First(item => ReferenceEquals(item.DataContext, first));

        Point edge = editor.PointIn(card, card.Bounds.Width / 2, card.Bounds.Height / 2);

        editor.PressAt(edge);

        Assert.Same(first, editor.View.PressedCard);

        editor.ReleaseAt(edge);

        Assert.Null(editor.View.PressedCard);
    }

    /// <summary>
    /// Sobre la figura del grupo seleccionado no: ahí se colocan sus planos.
    /// </summary>
    [AvaloniaFact]
    public void Sobre_la_figura_del_grupo_seleccionado_no_se_arrastra_la_ficha()
    {
        using var editor = ThreeGroups();

        editor.ViewModel.SelectedGroup = editor.ViewModel.Groups[0];

        Panel preview = editor.GroupPreview(0);

        editor.PressAt(editor.PointIn(preview, 4, 4));

        Assert.Null(editor.View.PressedCard);

        editor.ReleaseAt(editor.PointIn(preview, 4, 4));
    }

    /// <summary>
    /// Crear un grupo se deshace, y lo que se dibujó antes sigue en la historia.
    /// </summary>
    /// <remarks>
    /// Crear un grupo vaciaba la pila entera, así que deshacer se quedaba muerto sin que
    /// nada lo dijera: se ve igual que si el botón no respondiera.
    /// </remarks>
    [AvaloniaFact]
    public async Task Crear_un_grupo_se_deshace()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.SetThumbnailMode(ThumbnailMode.Groups);

        editor.ViewModel.PixelSurface.Set(1, 1, true);
        editor.ViewModel.PixelSurface.EndStroke();

        editor.ViewModel.AddGroupCommand.Execute(null);
        Pump();

        Assert.Single(editor.Bank.Groups);

        editor.ViewModel.UndoDrawingCommand.Execute(null);

        Assert.Empty(editor.Bank.Groups);
        Assert.Empty(editor.ViewModel.Groups);

        // Y el trazo de antes sigue estando, que es lo que se perdía.
        editor.ViewModel.UndoDrawingCommand.Execute(null);

        Assert.False(editor.ViewModel.PixelSurface.IsSet(1, 1));

        await Task.CompletedTask;
    }

    /// <summary>
    /// Eliminar un grupo se deshace, con sus planos y su nombre.
    /// </summary>
    /// <remarks>
    /// Vuelve el mismo grupo, no uno parecido: las animaciones lo buscan por su
    /// identificador, y uno nuevo tendría otro.
    /// </remarks>
    [AvaloniaFact]
    public async Task Eliminar_un_grupo_se_deshace()
    {
        using var editor = new SpriteCanvasHarness(
            PaintMode.Drag, SpriteBank.SpriteType.MSX2, new TestDialogService { ConfirmAnswer = true });

        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);
        editor.ViewModel.AddGroupCommand.Execute(null);
        Pump();

        SpriteGroupViewModel doomed = editor.ViewModel.Groups[0];

        editor.ViewModel.SelectedGroup = doomed;
        doomed.AddMember(4);

        int id = doomed.Group.Id;
        int planes = doomed.Group.Members.Count;

        await editor.ViewModel.DeleteGroupCommand.ExecuteAsync(null);

        Assert.DoesNotContain(doomed.Group, editor.Bank.Groups);

        editor.ViewModel.UndoDrawingCommand.Execute(null);

        SpriteGroup back = Assert.Single(editor.Bank.Groups, group => group.Id == id);

        Assert.Same(doomed.Group, back);
        Assert.Equal(planes, back.Members.Count);
        Assert.Equal(0, editor.ViewModel.Groups.IndexOf(doomed));

        // Y las dos listas no se separan: la que se ve y la que se guarda dicen lo mismo.
        Assert.Equal(
            [.. editor.ViewModel.Groups.Select(panel => panel.Group)], editor.Bank.Groups);
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un editor en modo grupos con tres grupos.</summary>
    private static SpriteCanvasHarness ThreeGroups()
    {
        var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.SetThumbnailMode(ThumbnailMode.Groups);

        for (int group = 0; group < 3; group++)
            editor.ViewModel.AddGroupCommand.Execute(null);

        // Colocado antes de devolverlo: sin esto las fichas no tienen sitio todavía y las
        // pulsaciones caen donde no hay nada.
        SpriteCanvasHarness.Pump();

        return editor;
    }
}
