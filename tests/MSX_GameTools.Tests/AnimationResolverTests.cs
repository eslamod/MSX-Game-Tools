using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Poner una animación en fila: desenrollar los bucles y aplicar el modo.
/// </summary>
/// <remarks>
/// Es lo que hace manejable el resto. Con los pasos tal cual, ir al fotograma anterior obliga a
/// rehacer la cuenta de los bucles desde el principio y arrastrar por la animación no se puede
/// hacer; resuelta, las dos cosas son un índice.
/// </remarks>
public class AnimationResolverTests
{
    [Fact]
    public void Los_fotogramas_salen_en_orden()
    {
        SpriteAnimation animation = Walking(1, 2, 3);

        AnimationTimeline timeline = AnimationResolver.Of(animation);

        Assert.Equal([1, 2, 3], timeline.Frames.Select(frame => frame.Target));
        Assert.False(timeline.Repeats);
        Assert.False(timeline.Truncated);
    }

    /// <summary>Un bucle repite lo que lleva dentro, y lo de fuera sale una vez.</summary>
    [Fact]
    public void Un_bucle_repite_lo_que_lleva_dentro()
    {
        var animation = new SpriteAnimation("Andar");

        animation.Steps.Add(Frame(1));

        var loop = new AnimationLoop { Times = 3 };
        loop.Steps.Add(Frame(2));
        loop.Steps.Add(Frame(3));

        animation.Steps.Add(loop);
        animation.Steps.Add(Frame(4));

        Assert.Equal(
            [1, 2, 3, 2, 3, 2, 3, 4],
            AnimationResolver.Of(animation).Frames.Select(frame => frame.Target));
    }

    /// <summary>Y los bucles se anidan, que es lo que permite tenerlos bien formados.</summary>
    [Fact]
    public void Los_bucles_se_anidan()
    {
        var animation = new SpriteAnimation("Andar");

        var outer = new AnimationLoop { Times = 2 };
        var inner = new AnimationLoop { Times = 2 };

        inner.Steps.Add(Frame(7));
        outer.Steps.Add(inner);
        outer.Steps.Add(Frame(8));

        animation.Steps.Add(outer);

        Assert.Equal(
            [7, 7, 8, 7, 7, 8],
            AnimationResolver.Of(animation).Frames.Select(frame => frame.Target));
    }

    // ------------------------------------------------------------------ los modos

    /// <summary>
    /// El ping-pong da la vuelta a los fotogramas, no a los pasos.
    /// </summary>
    /// <remarks>
    /// Es la razón de que exista esta resolución. Con los pasos del revés, un bucle del revés no
    /// significa nada; y con las órdenes sueltas de antes —poner patrón, desplazar— la animación
    /// invertida enseñaba los patrones hacia atrás mientras el bicho seguía andando hacia
    /// delante, porque unas órdenes son absolutas y otras relativas.
    /// </remarks>
    [Fact]
    public void El_ping_pong_da_la_vuelta_a_los_fotogramas()
    {
        SpriteAnimation animation = Walking(1, 2, 3, 4);
        animation.Mode = AnimationMode.PingPong;

        AnimationTimeline timeline = AnimationResolver.Of(animation);

        // Ida entera, y vuelta sin repetir los extremos.
        Assert.Equal([1, 2, 3, 4, 3, 2], timeline.Frames.Select(frame => frame.Target));
        Assert.True(timeline.Repeats);
    }

    /// <summary>
    /// Y con el desplazamiento absoluto, la vuelta deshace el camino.
    /// </summary>
    /// <remarks>
    /// El motivo de que el desplazamiento sea absoluto y no un incremento. Con incrementos, al
    /// invertir la lista el bicho seguiría avanzando en la misma dirección mientras los dibujos
    /// van hacia atrás, que es lo contrario de lo que se ve en un ping-pong.
    /// </remarks>
    [Fact]
    public void La_vuelta_deshace_el_camino()
    {
        var animation = new SpriteAnimation("Andar") { Mode = AnimationMode.PingPong };

        animation.Steps.Add(Frame(1, x: 0));
        animation.Steps.Add(Frame(2, x: 4));
        animation.Steps.Add(Frame(3, x: 8));

        Assert.Equal(
            [0, 4, 8, 4],
            AnimationResolver.Of(animation).Frames.Select(frame => frame.OffsetX));
    }

    /// <summary>Con dos fotogramas o menos no hay vuelta que dar.</summary>
    [Fact]
    public void Con_dos_fotogramas_el_ping_pong_no_anade_nada()
    {
        SpriteAnimation animation = Walking(1, 2);
        animation.Mode = AnimationMode.PingPong;

        Assert.Equal([1, 2], AnimationResolver.Of(animation).Frames.Select(frame => frame.Target));
    }

    /// <summary>El modo dice si vuelve a empezar, que es lo que necesita el reproductor.</summary>
    [Fact]
    public void El_modo_dice_si_vuelve_a_empezar()
    {
        Assert.False(AnimationResolver.Of(Walking(1)).Repeats);

        SpriteAnimation loop = Walking(1);
        loop.Mode = AnimationMode.Loop;

        Assert.True(AnimationResolver.Of(loop).Repeats);
    }

    // ------------------------------------------------------------------ las cuentas

    /// <summary>
    /// Lo que dura, en interrupciones y en milisegundos.
    /// </summary>
    /// <remarks>
    /// Los hercios no son del dato: la animación guarda interrupciones y son 50 o 60 por segundo
    /// según dónde esté enchufada la máquina. Guardando milisegundos, la misma animación duraría
    /// distinto en cada una.
    /// </remarks>
    [Fact]
    public void Se_sabe_lo_que_dura_en_las_dos_maquinas()
    {
        var animation = new SpriteAnimation("Andar");

        animation.Steps.Add(Frame(1, wait: 10));
        animation.Steps.Add(Frame(2, wait: 15));

        AnimationTimeline timeline = AnimationResolver.Of(animation);

        Assert.Equal(25, timeline.Ticks);
        Assert.Equal(500, timeline.Milliseconds(50));
        Assert.Equal(416.67, timeline.Milliseconds(60), 2);
    }

    /// <summary>
    /// Qué patrones toca, que es lo que hay que tener cargado.
    /// </summary>
    /// <remarks>
    /// Con un banco en ROM del que se vuelca a VRAM por animación, esto es justo lo que hace
    /// falta para saber qué subir: sin repetidos y en orden, para poder mirarlo de un vistazo.
    /// </remarks>
    [Fact]
    public void Se_sabe_que_patrones_toca()
    {
        var animation = new SpriteAnimation("Andar");

        var loop = new AnimationLoop { Times = 4 };
        loop.Steps.Add(Frame(12));
        loop.Steps.Add(Frame(13));

        animation.Steps.Add(loop);
        animation.Steps.Add(Frame(12));

        Assert.Equal([12, 13], AnimationResolver.Of(animation).Targets);
    }

    /// <summary>
    /// Un bucle enorme se corta y se dice, en vez de colgarse.
    /// </summary>
    /// <remarks>
    /// Doscientos dentro de doscientos son cuarenta mil fotogramas. Se puede escribir y la
    /// máquina lo aguanta; lo que no tiene sentido es desenrollarlo entero para enseñarlo.
    /// </remarks>
    [Fact]
    public void Un_bucle_enorme_se_corta_y_se_dice()
    {
        var animation = new SpriteAnimation("Bestia");

        var outer = new AnimationLoop { Times = 200 };
        var inner = new AnimationLoop { Times = 200 };

        inner.Steps.Add(Frame(1));
        outer.Steps.Add(inner);
        animation.Steps.Add(outer);

        AnimationTimeline timeline = AnimationResolver.Of(animation);

        Assert.True(timeline.Truncated);
        Assert.Equal(AnimationResolver.MaxFrames, timeline.Frames.Count);
    }

    // ------------------------------------------------------------------ los andamios

    private static AnimationFrame Frame(int target, int wait = 1, int x = 0, int y = 0) =>
        new() { Target = target, Wait = wait, OffsetX = x, OffsetY = y };

    private static SpriteAnimation Walking(params int[] targets)
    {
        var animation = new SpriteAnimation("Andar");

        foreach (int target in targets)
            animation.Steps.Add(Frame(target));

        return animation;
    }
}
