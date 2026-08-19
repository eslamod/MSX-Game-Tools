using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Editar la lista de pasos de una animación.
/// </summary>
/// <remarks>
/// Los pasos son un árbol —un bucle lleva los suyos dentro— y se editan en una lista plana con
/// sangría. Todo lo que se comprueba aquí sale de eso: lo que se añade tiene que caer en la
/// lista correcta, y moverlo no puede sacarlo de su bucle por accidente.
/// </remarks>
public class AnimationEditingTests
{
    /// <summary>Sin nada elegido, lo que se añade va al final del todo.</summary>
    [AvaloniaFact]
    public void Sin_nada_elegido_se_anade_al_final()
    {
        SpriteAnimationViewModel panel = Empty();

        panel.AddFrameCommand.Execute(null);
        panel.AddFrameCommand.Execute(null);

        Assert.Equal(2, panel.Animation.Steps.Count);
        Assert.Equal(2, panel.Steps.Count);
    }

    /// <summary>Con un paso elegido, lo nuevo entra justo detrás.</summary>
    [AvaloniaFact]
    public void Lo_nuevo_entra_detras_del_elegido()
    {
        SpriteAnimationViewModel panel = Empty();

        panel.AddFrameCommand.Execute(null);
        panel.Steps[0].Frame!.Target = 1;

        panel.AddFrameCommand.Execute(null);
        panel.Steps[1].Frame!.Target = 3;

        // Y volviendo al primero, el siguiente cae en medio.
        panel.SelectedStep = panel.Steps[0];
        panel.AddFrameCommand.Execute(null);
        panel.Steps[1].Frame!.Target = 2;

        Assert.Equal([1, 2, 3], panel.Steps.Select(step => step.Frame!.Target));
    }

    /// <summary>
    /// Estando en un bucle, lo que se añade cae dentro del bucle.
    /// </summary>
    /// <remarks>
    /// Es lo único que permite montar un bucle sin arrastrar después cada paso a mano. Y con el
    /// bucle elegido cae dentro y no detrás: quien acaba de crearlo lo que quiere es llenarlo.
    /// </remarks>
    [AvaloniaFact]
    public void Dentro_de_un_bucle_lo_nuevo_cae_dentro()
    {
        SpriteAnimationViewModel panel = Empty();

        panel.AddLoopCommand.Execute(null);
        panel.AddFrameCommand.Execute(null);
        panel.AddFrameCommand.Execute(null);

        AnimationLoop loop = Assert.IsType<AnimationLoop>(Assert.Single(panel.Animation.Steps));

        Assert.Equal(2, loop.Steps.Count);

        // Y en la lista salen sangrados, que es lo que se ve.
        Assert.Equal([0, 1, 1], panel.Steps.Select(step => step.Depth));
    }

    /// <summary>Subir el primero de un bucle no lo saca del bucle.</summary>
    /// <remarks>
    /// Sacarlo sería otra cosa distinta y con el mismo gesto, así que en el borde no se mueve.
    /// Si subir lo sacara, no habría forma de reordenar dentro de un bucle sin desarmarlo.
    /// </remarks>
    [AvaloniaFact]
    public void Subir_el_primero_de_un_bucle_no_lo_saca()
    {
        SpriteAnimationViewModel panel = WithLoop();

        panel.SelectedStep = panel.Steps[1];
        panel.MoveUpCommand.Execute(null);

        Assert.Equal([0, 1, 1], panel.Steps.Select(step => step.Depth));
        Assert.Equal(1, panel.Steps[1].Frame!.Target);
    }

    /// <summary>Y dentro del bucle sí se reordena.</summary>
    [AvaloniaFact]
    public void Dentro_del_bucle_se_reordena()
    {
        SpriteAnimationViewModel panel = WithLoop();

        panel.SelectedStep = panel.Steps[2];
        panel.MoveDownCommand.Execute(null);

        // El ultimo del bucle tampoco sale de el.
        Assert.Equal([1, 2], Inside(panel).Select(step => step.Target));

        panel.MoveUpCommand.Execute(null);

        Assert.Equal([2, 1], Inside(panel).Select(step => step.Target));
    }

    /// <summary>Borrar un bucle se lleva lo que tenía dentro.</summary>
    [AvaloniaFact]
    public void Borrar_un_bucle_se_lleva_lo_de_dentro()
    {
        SpriteAnimationViewModel panel = WithLoop();

        panel.SelectedStep = panel.Steps[0];
        panel.RemoveStepCommand.Execute(null);

        Assert.Empty(panel.Animation.Steps);
        Assert.Empty(panel.Steps);
    }

    /// <summary>
    /// Cualquier cambio vuelve a resolver la animación.
    /// </summary>
    /// <remarks>
    /// Es lo que hace que la vista previa y las cuentas digan la verdad mientras se edita. Sin
    /// esto habría que volver a elegir la animación para ver el efecto de cada retoque.
    /// </remarks>
    [AvaloniaFact]
    public void Cualquier_cambio_vuelve_a_resolver()
    {
        SpriteAnimationViewModel panel = WithLoop();

        Assert.Equal(4, panel.Timeline.Frames.Count);

        Inside(panel)[0].Wait = 5;
        panel.Refresh();

        // Dos vueltas de (5 + 1).
        Assert.Equal(12, panel.Timeline.Ticks);

        // Y subir las vueltas del bucle alarga la animación sola.
        Assert.IsType<AnimationLoop>(panel.Animation.Steps[0]).Times = 3;
        panel.Refresh();

        Assert.Equal(6, panel.Timeline.Frames.Count);
    }

    /// <summary>El tope de lo que se puede apuntar depende de a qué apunte la animación.</summary>
    [AvaloniaFact]
    public void El_tope_depende_de_si_son_patrones_o_grupos()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos", 128);

        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;
        bank.NewGroup(0);
        bank.NewGroup(0);

        var animation = new SpriteAnimation("Andar");
        var panel = new SpriteAnimationViewModel(animation, bank);

        Assert.Equal(127, panel.MaxTarget);

        panel.Kind = AnimationKind.Groups;

        Assert.Equal(1, panel.MaxTarget);
    }

    // ------------------------------------------------------------------ los andamios

    private static IReadOnlyList<AnimationFrame> Inside(SpriteAnimationViewModel panel) =>
        [.. Assert.IsType<AnimationLoop>(panel.Animation.Steps[0]).Steps.Cast<AnimationFrame>()];

    private static SpriteAnimationViewModel Empty() =>
        new(new SpriteAnimation("Andar"), new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos"));

    /// <summary>Un bucle de dos vueltas con dos fotogramas dentro.</summary>
    private static SpriteAnimationViewModel WithLoop()
    {
        SpriteAnimationViewModel panel = Empty();

        panel.AddLoopCommand.Execute(null);
        panel.AddFrameCommand.Execute(null);
        panel.Steps[1].Frame!.Target = 1;

        panel.AddFrameCommand.Execute(null);
        panel.Steps[2].Frame!.Target = 2;

        panel.Refresh();

        return panel;
    }

    /// <summary>
    /// Una fila se entera sola de que le han cambiado los números.
    /// </summary>
    /// <remarks>
    /// Sin esto habría que rehacer la lista al tocar una espera, y rehacerla vuelve a enlazar los
    /// controles que editan el paso, que vuelven a avisar de que han cambiado: se montaba un
    /// tiovivo de seis reconstrucciones por cada clic.
    /// </remarks>
    [AvaloniaFact]
    public void Una_fila_se_entera_de_sus_numeros_sin_rehacer_la_lista()
    {
        SpriteAnimationViewModel panel = Empty();

        panel.AddFrameCommand.Execute(null);

        AnimationStepViewModel row = panel.Steps[0];

        List<string> changed = [];
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        row.Frame!.Wait = 7;

        Assert.Equal("7", row.WaitText);
        Assert.Contains(nameof(AnimationStepViewModel.WaitText), changed);

        // Y la fila sigue siendo la misma: no se ha rehecho la lista por debajo.
        Assert.Same(row, panel.Steps[0]);
    }

    /// <summary>
    /// Vaciar una casilla no revienta ni pone el paso a cero.
    /// </summary>
    /// <remarks>
    /// Borrando el contenido para escribir otro número, la casilla se queda un momento sin nada.
    /// Eso llegaba al paso como un nulo y el editor se llenaba de un error de conversión mientras
    /// escribías. Poner cero tampoco vale: se vería el patrón 0 de refilón en cada tecleo.
    /// </remarks>
    [AvaloniaFact]
    public void Vaciar_una_casilla_deja_el_paso_como_estaba()
    {
        SpriteAnimationViewModel panel = Empty();

        panel.AddFrameCommand.Execute(null);

        AnimationStepViewModel row = panel.Steps[0];

        row.Target = 18;
        row.Wait = 6;

        // Lo que hace la casilla al quedarse en blanco.
        row.Target = null;
        row.Wait = null;
        row.OffsetX = null;
        row.Event = null;

        Assert.Equal(18, row.Frame!.Target);
        Assert.Equal(6, row.Frame.Wait);
    }

    /// <summary>Y lo mismo con las vueltas de un bucle.</summary>
    [AvaloniaFact]
    public void Vaciar_las_vueltas_deja_el_bucle_como_estaba()
    {
        SpriteAnimationViewModel panel = Empty();

        panel.AddLoopCommand.Execute(null);

        AnimationStepViewModel row = panel.Steps[0];

        row.Times = 5;
        row.Times = null;

        Assert.Equal(5, row.Loop!.Times);
    }
}
