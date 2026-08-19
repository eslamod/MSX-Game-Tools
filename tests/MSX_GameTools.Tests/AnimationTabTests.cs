using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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

    /// <summary>
    /// El color de fondo nuevo llega también a la vista previa de la animación.
    /// </summary>
    /// <remarks>
    /// Las miniaturas no se repintan encima: al cambiar el fondo se tira la que había y se hace
    /// otra. Quien la tiene enlazada se entera solo, pero la de la animación se pone a mano, y se
    /// quedaba con la de antes. Se veía el editor entero en negro y ese recuadro blanco.
    /// </remarks>
    [AvaloniaFact]
    public void El_fondo_nuevo_llega_a_la_vista_previa()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.ViewModel.SelectedAnimation!.AddFrameCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Animations);

        editor.ViewModel.BackgroundColorIndex = 1;
        Pump();

        Color before = editor.ViewModel.BackgroundColor.Color;

        editor.ViewModel.BackgroundColorIndex = 2;
        Pump();

        Color after = editor.ViewModel.BackgroundColor.Color;

        // Si los dos colores fueran el mismo la prueba pasaría sin comprobar nada.
        Assert.NotEqual(before, after);
        Assert.Equal(PixelReader.Bgra(after), Corner(editor));
    }

    /// <summary>La esquina de lo que enseña la vista previa, que en un patrón vacío es el fondo.</summary>
    private static int Corner(SpriteCanvasHarness editor)
    {
        Image preview = editor.View.GetVisualDescendants()
            .OfType<Image>()
            .Single(image => image.Name == "AnimationFrameImage");

        var bitmap = (WriteableBitmap?)preview.Source
                     ?? throw new InvalidOperationException("La vista previa no está enseñando nada.");

        int[] first = new int[bitmap.PixelSize.Width];

        using ILockedFramebuffer buffer = bitmap.Lock();
        Marshal.Copy(buffer.Address, first, 0, first.Length);

        return first[0];
    }

    private static Control Board(SpriteCanvasHarness editor) => editor.View
        .GetVisualDescendants()
        .OfType<Grid>()
        .Single(grid => grid.Name == "AnimationBoard");

    /// <summary>
    /// Los botones del zoom mueven también la vista previa de la animación.
    /// </summary>
    /// <remarks>
    /// Son los que hay a mano arriba a la derecha y ya escalan las miniaturas; quien pulsa X4
    /// espera que le crezca lo que está mirando, y en esta pestaña lo que se mira es la vista
    /// previa. Además el hueco da de sobra.
    /// </remarks>
    [AvaloniaFact]
    public void El_zoom_mueve_la_vista_previa_de_la_animacion()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Animations);

        double before = editor.View.AnimationPreviewSize;

        RadioButton bigger = editor.View.GetVisualDescendants()
            .OfType<RadioButton>()
            .Single(button => button.GroupName == "PreviewZoom" && (string?)button.Tag == "4");

        bigger.IsChecked = true;
        Pump();

        Assert.True(
            editor.View.AnimationPreviewSize > before,
            $"antes {before} y despues {editor.View.AnimationPreviewSize}");
    }
}
