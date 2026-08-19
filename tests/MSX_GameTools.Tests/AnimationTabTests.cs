using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;
using static MSX_GameTools.Tests.SpriteCanvasHarness;

namespace MSX_GameTools.Tests;

/// <summary>
/// La pestaña de animaciones montada de verdad.
/// </summary>
/// <remarks>
/// Los enlaces del XAML no los prueba nadie más: el modelo de vista puede estar perfecto y la
/// pestaña salir vacía porque un nombre está mal escrito. Aquí se monta la vista y se mira.
/// </remarks>
public class AnimationTabTests
{
    [AvaloniaFact]
    public void La_pestana_de_animaciones_ensena_las_del_banco()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.ViewModel.AddAnimationCommand.Execute(null);

        editor.SetThumbnailMode(ThumbnailMode.Animations);

        ListBox list = editor.View.GetVisualDescendants()
            .OfType<ListBox>()
            .Single(box => box.Name == "AnimationList");

        Assert.Equal(2, list.ItemCount);
        Assert.True(list.IsEffectivelyVisible);
    }

    /// <summary>Y los pasos de la que esté elegida, con su sangría.</summary>
    [AvaloniaFact]
    public void Los_pasos_salen_en_la_lista_con_su_sangria()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Animations);

        SpriteAnimationViewModel animation = editor.ViewModel.SelectedAnimation!;

        animation.AddLoopCommand.Execute(null);
        animation.AddFrameCommand.Execute(null);

        Pump();

        ListBox steps = editor.View.GetVisualDescendants()
            .OfType<ListBox>()
            .Single(box => box.Name == "StepList");

        Assert.Equal(2, steps.ItemCount);
        Assert.Equal([0, 1], animation.Steps.Select(step => step.Depth));
        Assert.Equal(16, animation.Steps[1].Indent.Left);
    }

    /// <summary>
    /// Las tres pestañas se excluyen: sólo se ve una a la vez.
    /// </summary>
    /// <remarks>
    /// Comparten hueco, así que si dos se creyeran visibles a la vez se pintarían una encima de
    /// otra. Es el fallo que deja el panel de las animaciones tapando los patrones.
    /// </remarks>
    [AvaloniaFact]
    public void Solo_se_ve_una_pestana_a_la_vez()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.SetThumbnailMode(ThumbnailMode.Animations);

        Assert.True(editor.ViewModel.ShowsAnimations);
        Assert.False(editor.ViewModel.ShowsPatterns);
        Assert.False(editor.ViewModel.ShowsGroups);

        Assert.True(Board(editor).IsEffectivelyVisible);
        Assert.False(editor.Thumbnails.IsEffectivelyVisible);

        editor.SetThumbnailMode(ThumbnailMode.Patterns);

        Assert.False(Board(editor).IsEffectivelyVisible);
        Assert.True(editor.Thumbnails.IsEffectivelyVisible);
    }

    /// <summary>
    /// El nombre de la animación se ve en la lista al escribirlo.
    /// </summary>
    /// <remarks>
    /// Es el enlace que más fácil se rompe: la lista enseña el envoltorio y el nombre vive en la
    /// entidad, así que sin reemitirlo la lista se queda con el nombre de cuando se creó.
    /// </remarks>
    [AvaloniaFact]
    public void El_nombre_se_ve_en_la_lista_al_cambiarlo()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Animations);

        editor.ViewModel.SelectedAnimation!.Name = "Andar";

        Pump();

        ListBox list = editor.View.GetVisualDescendants()
            .OfType<ListBox>()
            .Single(box => box.Name == "AnimationList");

        TextBlock label = ((Control)list.ContainerFromIndex(0)!)
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .First();

        Assert.Equal("Andar", label.Text);
    }

    private static Control Board(SpriteCanvasHarness editor) => editor.View
        .GetVisualDescendants()
        .OfType<Grid>()
        .Single(grid => grid.Name == "AnimationBoard");
}
