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

    /// <summary>
    /// Las animaciones se pueden cambiar de orden con las flechas.
    /// </summary>
    /// <remarks>
    /// Con el ratón y con la ventana montada, que es donde salen los fallos de este estilo: el
    /// modelo de vista suelto siempre pasa, y lo que se rompe es la lista escribiendo de vuelta.
    /// </remarks>
    [AvaloniaFact]
    public void Las_animaciones_se_pueden_cambiar_de_orden()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        for (int each = 0; each < 3; each++)
            editor.ViewModel.AddAnimationCommand.Execute(null);

        editor.SetThumbnailMode(ThumbnailMode.Animations);

        editor.ViewModel.Animations[0].Name = "Andar";
        editor.ViewModel.Animations[1].Name = "Saltar";
        editor.ViewModel.Animations[2].Name = "Caer";

        editor.ViewModel.SelectedAnimation = editor.ViewModel.Animations[2];
        Pump();

        ClickButton(Arrow(editor, "AnimationUpButton"));

        Assert.Equal(["Andar", "Caer", "Saltar"], editor.ViewModel.Animations.Select(one => one.Name));

        // La del banco es la que se guarda: moviendo sólo la de la pestaña, al abrir el fichero
        // volvería el orden de antes.
        Assert.Equal(["Andar", "Caer", "Saltar"], editor.Bank.Animations.Select(one => one.Name));

        // Y sigue elegida la misma animación, no la que ha ocupado su sitio.
        Assert.Equal("Caer", editor.ViewModel.SelectedAnimation?.Name);
    }

    /// <summary>La primera no se puede subir más, y pulsar no la deshace ni la duplica.</summary>
    [AvaloniaFact]
    public void Subir_la_primera_no_hace_nada()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.ViewModel.AddAnimationCommand.Execute(null);

        editor.SetThumbnailMode(ThumbnailMode.Animations);

        editor.ViewModel.Animations[0].Name = "Andar";
        editor.ViewModel.Animations[1].Name = "Saltar";

        editor.ViewModel.SelectedAnimation = editor.ViewModel.Animations[0];
        Pump();

        ClickButton(Arrow(editor, "AnimationUpButton"));

        Assert.Equal(["Andar", "Saltar"], editor.ViewModel.Animations.Select(one => one.Name));
        Assert.Equal(["Andar", "Saltar"], editor.Bank.Animations.Select(one => one.Name));
        Assert.Equal("Andar", editor.ViewModel.SelectedAnimation?.Name);
    }

    /// <summary>Elegir un paso deja seleccionado el patrón que enseña.</summary>
    [AvaloniaFact]
    public void Elegir_un_paso_lleva_a_su_patron()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Animations);

        SpriteAnimationViewModel animation = editor.ViewModel.SelectedAnimation!;

        animation.AddFrameCommand.Execute(null);
        animation.Steps[0].Target = 7;

        // Se deselecciona y se vuelve a elegir, que es lo que hace quien lo pulsa.
        animation.SelectedStep = null;
        animation.SelectedStep = animation.Steps[0];

        Pump();

        Assert.Equal(7, editor.ViewModel.CurrentSpriteIndex);
    }

    /// <summary>
    /// Y en las de grupos, al grupo que lleva ese número.
    /// </summary>
    /// <remarks>
    /// Por el número y no por el sitio que ocupa: borrando un grupo los que quedan no se
    /// renumeran, así que buscarlo por su sitio enseñaría otro distinto, o ninguno. Aquí se
    /// borra el primero a propósito para que las dos cosas no coincidan.
    /// </remarks>
    [AvaloniaFact]
    public async Task Elegir_un_paso_lleva_a_su_grupo()
    {
        using var editor = new SpriteCanvasHarness(
            PaintMode.Drag, SpriteBank.SpriteType.MSX2, dialogs: new TestDialogService());

        for (int each = 0; each < 3; each++)
        {
            editor.ViewModel.CurrentSpriteIndex = each;
            editor.ViewModel.AddGroupCommand.Execute(null);
        }

        editor.ViewModel.SelectedGroup = editor.ViewModel.Groups[0];
        await editor.ViewModel.DeleteGroupCommand.ExecuteAsync(null);

        // El que ahora está en el sitio 1 se sigue llamando 2: si no, esta prueba no separa
        // buscar por número de buscar por posición y pasaría de las dos maneras.
        int wanted = editor.ViewModel.Groups[1].Group.Id;

        Assert.Equal(2, editor.ViewModel.Groups.Count);
        Assert.Equal(2, wanted);

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Animations);

        SpriteAnimationViewModel animation = editor.ViewModel.SelectedAnimation!;

        animation.Kind = AnimationKind.Groups;
        animation.AddFrameCommand.Execute(null);
        animation.Steps[0].Target = wanted;

        animation.SelectedStep = null;
        animation.SelectedStep = animation.Steps[0];

        Pump();

        Assert.Equal(wanted, editor.ViewModel.SelectedGroup?.Group.Id);
    }

    /// <summary>
    /// La vista previa mueve el fotograma lo que diga su desplazamiento.
    /// </summary>
    /// <remarks>
    /// En píxeles de la máquina por el zoom que haya puesto, que es lo que hace falta para ver
    /// si una figura cojea al andar. Sin desplazamiento tiene que quedar centrada.
    /// </remarks>
    [AvaloniaFact]
    public void La_vista_previa_mueve_el_fotograma_desplazado()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Animations);

        SpriteAnimationViewModel animation = editor.ViewModel.SelectedAnimation!;

        animation.AddFrameCommand.Execute(null);
        Pump();

        Image preview = editor.View.GetVisualDescendants()
            .OfType<Image>()
            .Single(image => image.Name == "AnimationFrameImage");

        Assert.Equal(0, Moved(preview).X, 1);
        Assert.Equal(0, Moved(preview).Y, 1);

        animation.Steps[0].OffsetX = 4;
        animation.Steps[0].OffsetY = -2;
        animation.Refresh();

        Pump();

        // Un patrón mide 16 y el recuadro abarca 32, así que cada píxel de la máquina son dos
        // de pantalla al zoom de partida. El desplazamiento va en esos mismos píxeles.
        double scale = editor.View.AnimationPreviewSize / 32;

        Assert.Equal(4 * scale, Moved(preview).X, 1);
        Assert.Equal(-2 * scale, Moved(preview).Y, 1);
    }

    /// <summary>
    /// Y lo que se centra es la figura, no el dibujo.
    /// </summary>
    /// <remarks>
    /// La composición de un grupo puede sobresalir por arriba o por la izquierda, y entonces su
    /// centro no es el de la figura. Centrando el dibujo a secas, un grupo que asoma por arriba
    /// se vería más bajo que un patrón suelto, y al cambiar de fotograma daría un salto.
    /// </remarks>
    [AvaloniaFact]
    public void La_figura_queda_centrada_aunque_el_grupo_sobresalga()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        group.AddMemberCommand.Execute(null);
        group.Group.Members[1].OffsetY = -Sprite.Rows;

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Animations);

        SpriteAnimationViewModel animation = editor.ViewModel.SelectedAnimation!;

        animation.Kind = AnimationKind.Groups;
        animation.AddFrameCommand.Execute(null);
        animation.Steps[0].Target = group.Group.Id;
        animation.Refresh();

        Pump();

        Image preview = editor.View.GetVisualDescendants()
            .OfType<Image>()
            .Single(image => image.Name == "AnimationFrameImage");

        // El dibujo mide 32 de alto y el sprite sin desplazar es la mitad de abajo, así que hay
        // que bajarlo 8 para que quede centrado él y no la composición entera.
        double scale = editor.View.AnimationPreviewSize / 32;

        Assert.Equal(Sprite.Rows / 2 * scale, Moved(preview).Y, 1);
        Assert.Equal(0, Moved(preview).X, 1);
    }

    private static TranslateTransform Moved(Image preview) =>
        preview.RenderTransform as TranslateTransform
        ?? throw new InvalidOperationException("La vista previa no lleva desplazamiento.");

    private static Button Arrow(SpriteCanvasHarness editor, string name) => editor.View
        .GetVisualDescendants()
        .OfType<Button>()
        .Single(button => button.Name == name);

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
