using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.Entities;

/// <summary>De qué van los pasos de una animación.</summary>
public enum AnimationKind
{
    /// <summary>Cada paso enseña un patrón del banco.</summary>
    Patterns,

    /// <summary>Cada paso coloca un grupo entero.</summary>
    Groups,
}

/// <summary>Qué pasa al llegar al final.</summary>
public enum AnimationMode
{
    /// <summary>Se ejecuta una vez y para.</summary>
    Single,

    /// <summary>Al acabar vuelve a empezar.</summary>
    Loop,

    /// <summary>Al acabar se recorre del revés, y vuelta a empezar.</summary>
    PingPong,
}

/// <summary>Un paso de una animación: o un fotograma o un bucle.</summary>
public abstract class AnimationStep : ObservableObject;

/// <summary>
/// Un fotograma: qué se enseña, cuánto tiempo y dónde.
/// </summary>
/// <remarks>
/// <para>
/// Atómico a propósito, en vez de una tira de instrucciones sueltas —poner patrón, esperar,
/// desplazar—. Con las instrucciones sueltas, dar la vuelta a una animación no da la animación
/// del revés: mezclan órdenes absolutas con relativas, así que al invertirlas los patrones van
/// hacia atrás mientras el desplazamiento sigue yendo hacia delante.
/// </para>
/// <para>
/// Por eso el desplazamiento es <b>absoluto</b> respecto al origen de la animación y no un
/// incremento sobre el fotograma anterior. Además es lo que pide el caso de verdad: cuadrar el
/// centro de un grupo con el del anterior cuando no coinciden.
/// </para>
/// </remarks>
public partial class AnimationFrame : AnimationStep
{
    /// <summary>Lo que dura un fotograma como poco: enseñarlo cero interrupciones no es nada.</summary>
    public const int MinWait = 1;

    /// <summary>Y lo más que puede durar, que es lo que cabe en el byte que lee el Z80.</summary>
    public const int MaxWait = 255;

    /// <summary>Cuánto se puede sacar del origen, en pixeles.</summary>
    public const int MinOffset = -128;

    /// <inheritdoc cref="MinOffset"/>
    public const int MaxOffset = 127;

    private int _target;
    private int _wait = MinWait;
    private int _offsetX;
    private int _offsetY;
    private int _event;

    /// <summary>El patrón o el grupo que se enseña, según de qué vaya la animación.</summary>
    public int Target
    {
        get => _target;
        set => SetProperty(ref _target, Math.Max(0, value));
    }

    /// <summary>Interrupciones que se queda en pantalla.</summary>
    /// <remarks>
    /// En interrupciones y no en milisegundos: la máquina cuenta interrupciones, y son 50 o 60
    /// por segundo según dónde se enchufe. Guardando los milisegundos, la misma animación duraría
    /// distinto en una máquina y en otra, o habría que redondear al abrirla.
    /// </remarks>
    public int Wait
    {
        get => _wait;
        set => SetProperty(ref _wait, Math.Clamp(value, MinWait, MaxWait));
    }

    public int OffsetX
    {
        get => _offsetX;
        set => SetProperty(ref _offsetX, Math.Clamp(value, MinOffset, MaxOffset));
    }

    public int OffsetY
    {
        get => _offsetY;
        set => SetProperty(ref _offsetY, Math.Clamp(value, MinOffset, MaxOffset));
    }

    /// <summary>
    /// Un aviso para el juego, o 0 si este fotograma no avisa de nada.
    /// </summary>
    /// <remarks>
    /// Para el sonido del pisotón, para activar el golpe, para soltar la bala. Sin esto el juego
    /// acaba contando fotogramas por su cuenta para saber cuándo pasa cada cosa, y en cuanto se
    /// retoca una espera se descuadra sin que nada lo diga.
    /// </remarks>
    public int Event
    {
        get => _event;
        set => SetProperty(ref _event, Math.Clamp(value, 0, 255));
    }
}

/// <summary>
/// Un bucle: repite lo que lleva dentro.
/// </summary>
/// <remarks>
/// Los pasos van <b>dentro</b> y no entre dos marcas sueltas. Con marcas hay que emparejarlas a
/// mano, se puede dejar una abierta o cerrar una que no se abrió, y el editor tendría que avisar
/// de todo eso. Anidado no existe el problema: un bucle mal formado no se puede ni escribir.
/// El «fin de grupo» que lee el Z80 no se coloca, sale al exportar.
/// </remarks>
public partial class AnimationLoop : AnimationStep
{
    public const int MinTimes = 1;

    /// <summary>Lo que cabe en el byte de la cuenta.</summary>
    public const int MaxTimes = 255;

    private int _times = 2;

    public int Times
    {
        get => _times;
        set => SetProperty(ref _times, Math.Clamp(value, MinTimes, MaxTimes));
    }

    public ObservableCollection<AnimationStep> Steps { get; } = [];
}

/// <summary>
/// Una animación de un banco de sprites.
/// </summary>
/// <remarks>
/// Cuelga del banco porque no significa nada sin él: sus pasos apuntan a patrones o a grupos
/// suyos. Es lo mismo que ya pasa con los grupos, que apuntan a patrones.
/// </remarks>
public partial class SpriteAnimation : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private AnimationKind _kind;

    [ObservableProperty]
    private AnimationMode _mode;

    public SpriteAnimation(string name = "", AnimationKind kind = AnimationKind.Patterns)
    {
        _name = name;
        _kind = kind;
    }

    public ObservableCollection<AnimationStep> Steps { get; } = [];
}
