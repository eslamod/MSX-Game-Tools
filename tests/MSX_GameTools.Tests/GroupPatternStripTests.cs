using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;
using static MSX_GameTools.Tests.SpriteCanvasHarness;

namespace MSX_GameTools.Tests;

/// <summary>
/// La tira de patrones de la pestaña de grupos: de ahí se traen arrastrando.
/// </summary>
/// <remarks>
/// Se puede esconder, y lo que se elija se guarda: la tira ocupa alto y ese alto es de los
/// grupos, que es lo que se está mirando.
/// </remarks>
public class GroupPatternStripTests
{
    /// <summary>Soltar un patrón en el grupo le añade un plano con ese patrón.</summary>
    [AvaloniaFact]
    public void Soltar_un_patron_anade_un_plano_con_el()
    {
        using var editor = Grouped();

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        int had = group.Group.Members.Count;

        Assert.True(group.AddMember(7));

        Assert.Equal(had + 1, group.Group.Members.Count);
        Assert.Equal(7, group.Group.Members[^1].PatternIndex);

        // Y queda seleccionado, que es el que se va a colocar.
        Assert.Equal(7, group.SelectedMember!.PatternIndex);
    }

    /// <summary>Con el grupo lleno no cabe ninguno más.</summary>
    [AvaloniaFact]
    public void Con_el_grupo_lleno_no_se_anade()
    {
        using var editor = Grouped();

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        while (group.Group.CanAddMember)
            Assert.True(group.AddMember(1));

        Assert.Equal(SpriteGroup.MaxMembers, group.Group.Members.Count);
        Assert.False(group.AddMember(2));
    }

    /// <summary>Y un patrón que el banco no tiene tampoco entra.</summary>
    [AvaloniaFact]
    public void Un_patron_que_no_existe_no_entra()
    {
        using var editor = Grouped();

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        Assert.False(group.AddMember(editor.Bank.SpritesList.Count));
        Assert.False(group.AddMember(-1));
    }

    /// <summary>
    /// La tira se enseña y se esconde con su interruptor, y sólo está en la de grupos.
    /// </summary>
    /// <remarks>
    /// Montada porque lo que hay que comprobar es que el borde de la tira aparece y
    /// desaparece de verdad, que va por enlace.
    /// </remarks>
    [AvaloniaFact]
    public void El_interruptor_esconde_la_tira()
    {
        using var editor = Grouped();

        Border strip = editor.View.GetVisualDescendants()
            .OfType<Border>()
            .Single(border => border.Name == "GroupPatternStrip");

        Assert.True(strip.IsVisible);

        editor.ViewModel.ShowGroupPatterns = false;
        Pump();

        Assert.False(strip.IsVisible);

        editor.ViewModel.ShowGroupPatterns = true;
        editor.SetThumbnailMode(ThumbnailMode.Patterns);
        Pump();

        // En la pestaña de patrones no pinta nada: los patrones ya son lo que se ve.
        Assert.False(strip.IsVisible);
    }

    /// <summary>Y lo que se elija se guarda de una sesión a otra.</summary>
    [AvaloniaFact]
    public void Esconderla_se_recuerda()
    {
        string folder = Directory.CreateTempSubdirectory("tira").FullName;

        try
        {
            var store = new SettingsStore(folder);
            var preferences = new EditorPreferences { GroupPatternsOpen = false };

            store.Save(new Settings("es", preferences, []));

            Assert.False(store.Load().Preferences.GroupPatternsOpen);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Pulsar un patrón de la tira lo deja listo para arrastrarlo.</summary>
    /// <inheritdoc cref="GroupOrderTests.Pulsar_una_ficha_la_deja_lista_para_arrastrar" path="/remarks"/>
    [AvaloniaFact]
    public void Pulsar_un_patron_lo_deja_listo_para_arrastrar()
    {
        using var editor = Grouped();

        ListBox strip = editor.View.GetVisualDescendants()
            .OfType<ListBox>()
            .Single(list => list.Name == "GroupPatternList");

        Pump();

        ListBoxItem first = strip.GetRealizedContainers().OfType<ListBoxItem>().First();

        // Sobre la imagen del patrón, que es lo que se ve y donde se pulsa.
        Image drawn = first.GetVisualDescendants().OfType<Image>().First();

        Point at = editor.PointIn(drawn, drawn.Bounds.Width / 2, drawn.Bounds.Height / 2);

        editor.PressAt(at);

        Assert.Equal(0, editor.View.PressedPattern);

        editor.ReleaseAt(at);

        Assert.Null(editor.View.PressedPattern);
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un editor en modo grupos, con un grupo ya creado y colocado.</summary>
    private static SpriteCanvasHarness Grouped()
    {
        var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.SetThumbnailMode(ThumbnailMode.Groups);
        editor.ViewModel.AddGroupCommand.Execute(null);

        Pump();

        return editor;
    }
}
