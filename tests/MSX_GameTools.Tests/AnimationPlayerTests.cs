using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Recorrer una animación: reproducir, parar y saltar de fotograma.
/// </summary>
/// <remarks>
/// El reproductor no tiene reloj; el reloj es de la vista y aquí sólo llegan pulsos. Así se
/// recorre una animación entera en una prueba sin esperar nada y sin que el resultado dependa de
/// lo cargada que esté la máquina.
/// </remarks>
public class AnimationPlayerTests
{
    /// <summary>Cada fotograma se queda los pulsos que dice, ni uno más.</summary>
    [Fact]
    public void Cada_fotograma_dura_lo_que_dice()
    {
        AnimationPlayerViewModel player = Playing(Frames((1, 3), (2, 1)));

        Assert.Equal(0, player.FrameIndex);

        player.Beat();
        player.Beat();

        // Dos pulsos de tres: sigue en el primero.
        Assert.Equal(0, player.FrameIndex);

        player.Beat();

        Assert.Equal(1, player.FrameIndex);
    }

    /// <summary>Al final, una animación de una vuelta para y se queda quieta.</summary>
    [Fact]
    public void Al_final_una_animacion_de_una_vuelta_para()
    {
        AnimationPlayerViewModel player = Playing(Frames((1, 1), (2, 1)));

        player.Beat();
        player.Beat();

        Assert.False(player.IsPlaying);
        Assert.Equal(1, player.FrameIndex);
    }

    /// <summary>Y una que se repite vuelve al principio sin parar.</summary>
    [Fact]
    public void Una_que_se_repite_vuelve_al_principio()
    {
        AnimationPlayerViewModel player = Playing(Frames((1, 1), (2, 1)), AnimationMode.Loop);

        player.Beat();
        player.Beat();

        Assert.True(player.IsPlaying);
        Assert.Equal(0, player.FrameIndex);
    }

    /// <summary>
    /// Ralentizar multiplica lo que dura cada fotograma.
    /// </summary>
    /// <remarks>
    /// Con los mismos pulsos se avanza la mitad de fotogramas al doble de lento, y eso es lo que
    /// hay que comprobar: mirando sólo dónde se acaba, una animación corta termina en el mismo
    /// sitio con y sin ralentizar y la prueba no mide nada.
    /// </remarks>
    [Fact]
    public void Ralentizar_multiplica_lo_que_dura_cada_fotograma()
    {
        AnimationPlayerViewModel normal = Playing(Frames((1, 1), (2, 1), (3, 1)), AnimationMode.Loop);
        AnimationPlayerViewModel slow = Playing(Frames((1, 1), (2, 1), (3, 1)), AnimationMode.Loop);

        slow.Slowdown = 2;

        for (int beat = 0; beat < 4; beat++)
        {
            normal.Beat();
            slow.Beat();
        }

        // Cuatro pulsos: cuatro fotogramas dando la vuelta, o dos yendo al doble de lento.
        Assert.Equal(1, normal.FrameIndex);
        Assert.Equal(2, slow.FrameIndex);
    }

    /// <summary>Parado, los pulsos no mueven nada.</summary>
    [Fact]
    public void Parado_los_pulsos_no_mueven_nada()
    {
        AnimationPlayerViewModel player = Loaded(Frames((1, 1), (2, 1)));

        player.Beat();
        player.Beat();

        Assert.Equal(0, player.FrameIndex);
    }

    // ------------------------------------------------------------------ a mano

    /// <summary>El fotograma anterior es restar uno, y da la vuelta si se repite.</summary>
    [Fact]
    public void El_anterior_da_la_vuelta_si_se_repite()
    {
        AnimationPlayerViewModel player = Loaded(Frames((1, 1), (2, 1), (3, 1)), AnimationMode.Loop);

        player.Previous();

        Assert.Equal(2, player.FrameIndex);
    }

    /// <summary>Y en una de una vuelta se queda en el primero.</summary>
    [Fact]
    public void El_anterior_del_primero_se_queda_en_el_primero()
    {
        AnimationPlayerViewModel player = Loaded(Frames((1, 1), (2, 1)));

        player.Previous();

        Assert.Equal(0, player.FrameIndex);
    }

    /// <summary>Parar vuelve al principio, que es lo que se espera de un botón de parar.</summary>
    [Fact]
    public void Parar_vuelve_al_principio()
    {
        AnimationPlayerViewModel player = Playing(Frames((1, 1), (2, 1), (3, 1)));

        player.Next();
        player.Stop();

        Assert.False(player.IsPlaying);
        Assert.Equal(0, player.FrameIndex);
    }

    // ------------------------------------------------------------------ al cambiar

    /// <summary>
    /// Al recargar se queda donde estaba, si ese fotograma sigue existiendo.
    /// </summary>
    /// <remarks>
    /// Se recarga en cuanto se toca un paso, y saltar al principio cada vez obligaría a volver a
    /// buscar el fotograma que se estaba mirando después de cada retoque.
    /// </remarks>
    [Fact]
    public void Al_recargar_se_queda_donde_estaba()
    {
        AnimationPlayerViewModel player = Loaded(Frames((1, 1), (2, 1), (3, 1)));

        player.Next();
        player.Next();

        player.Load(Timeline(Frames((1, 1), (2, 1), (3, 1), (4, 1))));

        Assert.Equal(2, player.FrameIndex);
    }

    /// <summary>Y si ya no existe, al principio en vez de a un sitio que no hay.</summary>
    [Fact]
    public void Si_el_fotograma_ya_no_existe_vuelve_al_principio()
    {
        AnimationPlayerViewModel player = Loaded(Frames((1, 1), (2, 1), (3, 1)));

        player.Next();
        player.Next();

        player.Load(Timeline(Frames((1, 1))));

        Assert.Equal(0, player.FrameIndex);
        Assert.Equal(1, player.Current!.Target);
    }

    /// <summary>Lo que dura depende de la frecuencia, que es de la vista y no del dato.</summary>
    [Fact]
    public void Lo_que_dura_depende_de_la_frecuencia()
    {
        AnimationPlayerViewModel player = Loaded(Frames((1, 25), (2, 25)));

        Assert.Equal(1000, player.Milliseconds);

        player.Hz = 60;

        Assert.Equal(833.33, player.Milliseconds, 2);
    }

    /// <summary>Una animación sin pasos no se reproduce ni revienta.</summary>
    [Fact]
    public void Una_animacion_vacia_no_se_reproduce()
    {
        var player = new AnimationPlayerViewModel();

        player.Play();
        player.Beat();
        player.Next();
        player.Previous();

        Assert.False(player.IsPlaying);
        Assert.False(player.HasFrames);
        Assert.Null(player.Current);
    }

    /// <summary>
    /// La animación avanza por el tiempo que ha pasado, no por pulsos del temporizador.
    /// </summary>
    /// <remarks>
    /// El temporizador de la interfaz no da el intervalo que se le pide: en Windows la
    /// resolución del reloj del sistema son unos 15,6 ms, así que pidiendo 16,7 llegan cada 26 y
    /// pico. Contando pulsos, la vista previa iba a la mitad y media de la velocidad que decía
    /// —medido sobre una captura: 156 ms por fotograma donde tocaban 100—, y eso es justo lo que
    /// se está mirando ahí, si una figura cojea al andar.
    /// </remarks>
    [Fact]
    public void La_animacion_avanza_por_el_tiempo_y_no_por_pulsos()
    {
        AnimationPlayerViewModel player = Playing(Frames((1, 6), (2, 6)), AnimationMode.Loop);

        // Un temporizador que llega tarde: 26 ms a 60 Hz son 1,56 interrupciones por pulso.
        const double Late = 26.0 * 60 / 1000;

        player.Advance(Late);
        player.Advance(Late);
        player.Advance(Late);

        Assert.Equal(0, player.FrameIndex);     // 4,68 interrupciones de las 6

        player.Advance(Late);

        Assert.Equal(1, player.FrameIndex);     // 6,24: ya toca
    }

    /// <summary>
    /// Y si el programa se ha quedado parado, se saltan fotogramas en vez de ir con retraso.
    /// </summary>
    /// <remarks>
    /// Lo que tiene que salir bien es el ritmo, no verlos todos: una animación que se recupera
    /// despacio de un parón enseñaría una cadencia que la máquina no va a tener.
    /// </remarks>
    [Fact]
    public void Un_paron_salta_fotogramas_en_vez_de_ir_con_retraso()
    {
        AnimationPlayerViewModel player = Playing(Frames((1, 6), (2, 6)), AnimationMode.Loop);

        // Un segundo entero de golpe: son cinco vueltas justas de doce interrupciones.
        player.Advance(60);

        Assert.Equal(0, player.FrameIndex);
    }

    // ------------------------------------------------------------------ los andamios

    private static SpriteAnimation Frames(params (int Target, int Wait)[] frames)
    {
        var animation = new SpriteAnimation("Andar");

        foreach ((int target, int wait) in frames)
            animation.Steps.Add(new AnimationFrame { Target = target, Wait = wait });

        return animation;
    }

    private static AnimationTimeline Timeline(SpriteAnimation animation) =>
        AnimationResolver.Of(animation);

    private static AnimationPlayerViewModel Loaded(
        SpriteAnimation animation, AnimationMode mode = AnimationMode.Single)
    {
        animation.Mode = mode;

        var player = new AnimationPlayerViewModel();
        player.Load(Timeline(animation));

        return player;
    }

    private static AnimationPlayerViewModel Playing(
        SpriteAnimation animation, AnimationMode mode = AnimationMode.Single)
    {
        AnimationPlayerViewModel player = Loaded(animation, mode);
        player.Play();

        return player;
    }
}
