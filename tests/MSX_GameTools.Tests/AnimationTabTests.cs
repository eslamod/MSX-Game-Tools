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
    /// Sin desplazamientos la figura queda centrada, mida lo que mida.
    /// </summary>
    /// <remarks>
    /// Una figura de dos sprites de alto es lo normal, y salía subida: se centraba la casilla
    /// de 16x16 del primer sprite en vez de la figura, así que la mitad de abajo se iba por
    /// fuera del recuadro. Lo que se centra es lo que ocupa la animación entera.
    /// </remarks>
    [AvaloniaFact]
    public void Sin_desplazamientos_la_figura_queda_centrada()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        SpriteAnimationViewModel animation = WithTallFigure(editor);

        animation.AddFrameCommand.Execute(null);
        animation.Refresh();

        Pump();

        Image preview = Preview(editor);

        Assert.Equal(0, Moved(preview).X, 1);
        Assert.Equal(0, Moved(preview).Y, 1);

        Fits(preview);
    }

    /// <summary>Y una que sobresale por arriba también, que su origen es el mismo.</summary>
    [AvaloniaFact]
    public void Una_figura_que_sobresale_por_arriba_tambien_queda_centrada()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        SpriteAnimationViewModel animation = WithTallFigure(editor, above: true);

        animation.AddFrameCommand.Execute(null);
        animation.Refresh();

        Pump();

        Image preview = Preview(editor);

        Assert.Equal(0, Moved(preview).X, 1);
        Assert.Equal(0, Moved(preview).Y, 1);

        Fits(preview);
    }

    /// <summary>
    /// El desplazamiento separa un fotograma del otro, y los dos caben.
    /// </summary>
    /// <remarks>
    /// Lo que hace falta para ver si una figura cojea al andar: no dónde cae uno suelto, sino
    /// cuánto se mueve respecto al de al lado. El hueco crece hacia donde vaya el
    /// desplazamiento, así que ninguno de los dos se recorta contra el borde.
    /// </remarks>
    [AvaloniaFact]
    public void El_desplazamiento_separa_un_fotograma_del_otro()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        SpriteAnimationViewModel animation = WithTallFigure(editor);

        animation.AddFrameCommand.Execute(null);
        animation.AddFrameCommand.Execute(null);

        animation.Steps[1].OffsetY = 4;
        animation.Refresh();

        Pump();

        Image preview = Preview(editor);

        double first = Moved(preview).Y;

        Fits(preview);

        editor.ViewModel.Player.Next();
        Pump();

        double second = Moved(preview).Y;

        Fits(preview);

        // Un pixel de la maquina son tantos de pantalla como diga la imagen: mide 16 de ancho.
        double scale = preview.Width / SpriteRow.Columns;

        Assert.Equal(4 * scale, second - first, 1);

        // Y el par queda centrado: uno sube lo que el otro baja.
        Assert.Equal(0, first + second, 1);
    }

    /// <summary>Un desplazamiento grande encoge la figura, pero no la recorta.</summary>
    [AvaloniaTheory]
    [InlineData("1")]
    [InlineData("4")]
    public void Un_desplazamiento_grande_no_recorta_nada(string zoom)
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        SpriteAnimationViewModel animation = WithTallFigure(editor);

        animation.AddFrameCommand.Execute(null);
        animation.AddFrameCommand.Execute(null);

        animation.Steps[1].OffsetY = 40;
        animation.Steps[1].OffsetX = -24;
        animation.Refresh();

        Zoom(editor, zoom);

        Image preview = Preview(editor);

        Fits(preview);

        editor.ViewModel.Player.Next();
        Pump();

        Fits(preview);
    }

    /// <summary>
    /// El zoom cambia de verdad el tamaño del dibujo, no sólo el del recuadro.
    /// </summary>
    /// <remarks>
    /// El tamaño ya no sale de un enlace del XAML sino de una cuenta, así que hay que rehacerla
    /// al cambiar el zoom. Sin eso el recuadro encogía y el dibujo se quedaba con el tamaño de
    /// antes: se veía bien justo al zoom con el que se hubiera calculado y recortado en los
    /// demás. Y no vale con mirar que quepa, que un dibujo que se ha quedado pequeño cabe.
    /// </remarks>
    [AvaloniaFact]
    public void El_zoom_cambia_el_tamano_del_dibujo()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        SpriteAnimationViewModel animation = WithTallFigure(editor);

        animation.AddFrameCommand.Execute(null);
        animation.Refresh();

        Zoom(editor, "1");

        Image preview = Preview(editor);
        Panel box = preview.GetVisualParent<Panel>()!;

        // Sin desplazamientos, una figura de dos sprites de alto llena el recuadro a lo alto.
        Assert.Equal(box.Bounds.Height, preview.Height, 1);

        double small = preview.Height;

        Zoom(editor, "4");

        Assert.Equal(4 * small, preview.Height, 1);
        Assert.Equal(box.Bounds.Height, preview.Height, 1);
    }

    /// <summary>
    /// Los Hz y la velocidad se cambian de un clic.
    /// </summary>
    /// <remarks>
    /// Son cuatro valores y dos: con un desplegable hay que abrirlo primero y elegir después,
    /// y con la tira de botones basta con pulsar el que se quiere.
    /// </remarks>
    [AvaloniaFact]
    public void Los_hz_y_la_velocidad_se_cambian_de_un_clic()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Animations);

        Pump();

        Assert.Equal(50, editor.ViewModel.Player.Hz);

        ClickButton(Toggle(editor, "AnimHz", "60"));

        Assert.Equal(60, editor.ViewModel.Player.Hz);

        ClickButton(Toggle(editor, "AnimSlowdown", "4"));

        Assert.Equal(4, editor.ViewModel.Player.Slowdown);
    }

    /// <summary>Y el botón encendido es el del valor que hay, lo ponga quien lo ponga.</summary>
    [AvaloniaFact]
    public void El_boton_encendido_es_el_del_valor_que_hay()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Animations);

        editor.ViewModel.Player.Slowdown = 8;

        Pump();

        Assert.True(Toggle(editor, "AnimSlowdown", "8").IsChecked);
        Assert.False(Toggle(editor, "AnimSlowdown", "1").IsChecked);
    }

    /// <summary>
    /// Hay un botón por cada valor que admite el reproductor, y en su orden.
    /// </summary>
    /// <remarks>
    /// Los valores viven en el reproductor y los botones en el XAML, que es donde tienen que
    /// estar; esto es lo que impide que se separen. Añadir una frecuencia y olvidarse del botón
    /// no daría ningún error: simplemente no habría forma de elegirla.
    /// </remarks>
    [AvaloniaFact]
    public void Hay_un_boton_por_cada_valor()
    {
        using var editor = new SpriteCanvasHarness(PaintMode.Drag, SpriteBank.SpriteType.MSX2);

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Animations);

        Pump();

        Assert.Equal(AnimationPlayerViewModel.Frequencies, Values(editor, "AnimHz"));
        Assert.Equal(AnimationPlayerViewModel.Slowdowns, Values(editor, "AnimSlowdown"));
    }

    private static IReadOnlyList<int> Values(SpriteCanvasHarness editor, string group) =>
    [
        .. editor.View
            .GetVisualDescendants()
            .OfType<RadioButton>()
            .Where(one => one.GroupName == group)
            .Select(one => int.Parse((string)one.Content!)),
    ];

    private static RadioButton Toggle(SpriteCanvasHarness editor, string group, string content) =>
        editor.View
            .GetVisualDescendants()
            .OfType<RadioButton>()
            .Single(one => one.GroupName == group && (string?)one.Content == content);

    private static void Zoom(SpriteCanvasHarness editor, string factor)
    {
        editor.View.GetVisualDescendants()
            .OfType<RadioButton>()
            .Single(one => one.GroupName == "PreviewZoom" && (string?)one.Tag == factor)
            .IsChecked = true;

        Pump();
    }

    /// <summary>Una figura de dos sprites de alto, que es lo corriente.</summary>
    private static SpriteAnimationViewModel WithTallFigure(SpriteCanvasHarness editor, bool above = false)
    {
        editor.ViewModel.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.ViewModel.SelectedGroup!;

        group.AddMemberCommand.Execute(null);
        group.Group.Members[1].OffsetY = above ? -Sprite.Rows : Sprite.Rows;

        editor.ViewModel.AddAnimationCommand.Execute(null);
        editor.SetThumbnailMode(ThumbnailMode.Animations);

        SpriteAnimationViewModel animation = editor.ViewModel.SelectedAnimation!;

        animation.Kind = AnimationKind.Groups;

        return animation;
    }

    private static Image Preview(SpriteCanvasHarness editor) => editor.View
        .GetVisualDescendants()
        .OfType<Image>()
        .Single(image => image.Name == "AnimationFrameImage");

    /// <summary>Que el dibujo entre entero en el recuadro, con el desplazamiento puesto.</summary>
    private static void Fits(Image preview)
    {
        Panel box = preview.GetVisualParent<Panel>()!;
        TranslateTransform moved = Moved(preview);

        // Con las medidas pedidas y no con las que quedan después de redondear a píxel entero:
        // medio píxel de redondeo no es un recorte, y lo que se comprueba aquí es la cuenta.
        double top = ((box.Bounds.Height - preview.Height) / 2) + moved.Y;
        double left = ((box.Bounds.Width - preview.Width) / 2) + moved.X;

        Assert.True(top >= -0.01, $"se sale por arriba: {top}");
        Assert.True(left >= -0.01, $"se sale por la izquierda: {left}");

        Assert.True(
            top + preview.Height <= box.Bounds.Height + 0.01,
            $"se sale por abajo: {top + preview.Height} de {box.Bounds.Height}");

        Assert.True(
            left + preview.Width <= box.Bounds.Width + 0.01,
            $"se sale por la derecha: {left + preview.Width} de {box.Bounds.Width}");
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
