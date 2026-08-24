using CommunityToolkit.Mvvm.ComponentModel;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Lleva la cuenta de por dónde va una animación al reproducirla.
/// </summary>
/// <remarks>
/// <para>
/// No tiene reloj. Quien lo tiene es la vista, y aquí sólo llegan pulsos: así se puede recorrer
/// una animación entera en una prueba sin esperar ni un milisegundo, y sin que el resultado
/// dependa de lo cargada que esté la máquina.
/// </para>
/// <para>
/// Trabaja sobre la línea de tiempo ya resuelta, no sobre los pasos. Por eso ir al fotograma
/// anterior es restar uno en vez de rehacer la cuenta de los bucles desde el principio.
/// </para>
/// </remarks>
public partial class AnimationPlayerViewModel : ObservableObject
{
    /// <summary>Las dos frecuencias a las que puede ir un MSX, según dónde se enchufe.</summary>
    public static IReadOnlyList<int> Frequencies { get; } = [50, 60];

    /// <summary>Lo que se puede ralentizar para mirar un fotograma con calma.</summary>
    public static IReadOnlyList<int> Slowdowns { get; } = [1, 2, 4, 8];

    private AnimationTimeline _timeline = new([], false, false);

    /// <summary>Pulsos que llevamos dentro del fotograma que se está viendo.</summary>
    private double _waited;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Current))]
    [NotifyPropertyChangedFor(nameof(Position))]
    private int _frameIndex;

    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// A cuántas interrupciones por segundo se está mirando.
    /// </summary>
    /// <remarks>
    /// De la vista y no del dato: la animación guarda interrupciones, y que sean 50 o 60 por
    /// segundo depende de dónde esté enchufada la máquina. Aquí sólo cambia lo que se lee en
    /// milisegundos y lo deprisa que va la vista previa.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Milliseconds))]
    private int _hz = 50;

    [ObservableProperty]
    private int _slowdown = 1;

    /// <summary>Los fotogramas que se están recorriendo.</summary>
    public AnimationTimeline Timeline
    {
        get => _timeline;
        private set
        {
            _timeline = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(Position));
            OnPropertyChanged(nameof(Milliseconds));
            OnPropertyChanged(nameof(HasFrames));
        }
    }

    public bool HasFrames => Timeline.Frames.Count > 0;

    /// <summary>El fotograma que toca ver, o nada si la animación está vacía.</summary>
    public TimelineFrame? Current =>
        (uint)FrameIndex < (uint)Timeline.Frames.Count ? Timeline.Frames[FrameIndex] : null;

    /// <summary>Por dónde va, para leerlo.</summary>
    public string Position => HasFrames ? $"{FrameIndex + 1} / {Timeline.Frames.Count}" : "-";

    /// <summary>Lo que dura una vuelta a la frecuencia elegida.</summary>
    public double Milliseconds => Timeline.Milliseconds(Hz);

    /// <summary>
    /// Cambia lo que se reproduce.
    /// </summary>
    /// <remarks>
    /// Se queda donde estaba si el fotograma sigue existiendo. Editar un paso del medio y que la
    /// vista previa salte al principio obligaría a volver a buscarlo cada vez que se toca algo.
    /// </remarks>
    public void Load(AnimationTimeline timeline)
    {
        Timeline = timeline;
        _waited = 0;

        if (FrameIndex >= timeline.Frames.Count)
            FrameIndex = 0;
    }

    /// <summary>Un pulso del reloj de la máquina.</summary>
    public void Beat() => Advance(1);

    /// <summary>
    /// Adelanta la animación las interrupciones que hayan pasado de verdad.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Por tiempo pasado y no contando pulsos: el temporizador de la interfaz no da 16,7 ms
    /// aunque se le pidan —en Windows la resolución del reloj del sistema es de unos 15,6 y los
    /// avisos se agrupan—, así que contando pulsos la vista previa iba a la mitad y media de la
    /// velocidad que decía. Medido sobre una captura: 156 ms por fotograma donde tocaban 100.
    /// Y eso es justo lo que se está mirando aquí, si una figura cojea al andar.
    /// </para>
    /// <para>
    /// Si el programa se ha quedado parado un rato, se saltan fotogramas en vez de ir con
    /// retraso: lo que tiene que salir bien es el ritmo, no verlos todos.
    /// </para>
    /// <para>
    /// Ralentizar multiplica lo que dura cada fotograma, así que las proporciones entre ellos se
    /// mantienen: uno que dure el doble que otro lo sigue durando a cualquier velocidad.
    /// </para>
    /// </remarks>
    public void Advance(double interrupts)
    {
        if (!IsPlaying || !HasFrames || interrupts <= 0)
            return;

        _waited += interrupts;

        // El sobrante se guarda y se repone: Next pone la cuenta a cero, que es lo que hace
        // falta cuando se avanza a mano, y aqui se comeria lo que llevabamos de mas.
        while (IsPlaying && _waited >= (Current?.Wait ?? 1) * Slowdown)
        {
            double left = _waited - ((Current?.Wait ?? 1) * Slowdown);

            Next();

            _waited = left;
        }
    }

    /// <summary>Al siguiente fotograma. Al final, o vuelve a empezar o para.</summary>
    public void Next()
    {
        if (!HasFrames)
            return;

        if (FrameIndex + 1 < Timeline.Frames.Count)
        {
            FrameIndex++;
        }
        else if (Timeline.Repeats)
        {
            FrameIndex = 0;
        }
        else
        {
            IsPlaying = false;
        }

        _waited = 0;
    }

    /// <summary>Al anterior, dando la vuelta si la animación se repite.</summary>
    public void Previous()
    {
        if (!HasFrames)
            return;

        FrameIndex = FrameIndex > 0
            ? FrameIndex - 1
            : Timeline.Repeats ? Timeline.Frames.Count - 1 : 0;

        _waited = 0;
    }

    public void Play() => IsPlaying = HasFrames;

    public void Pause() => IsPlaying = false;

    /// <summary>Para y vuelve al principio, que es lo que se espera de un botón de parar.</summary>
    public void Stop()
    {
        IsPlaying = false;
        FrameIndex = 0;
        _waited = 0;
    }
}
