using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>Un fotograma ya resuelto: sin bucles y con el sitio donde cae.</summary>
public sealed record TimelineFrame(int Target, int Wait, int OffsetX, int OffsetY, int Event);

/// <summary>
/// Una animación puesta en fila, tal y como se va a ver.
/// </summary>
/// <param name="Frames">Los fotogramas de una vuelta entera, bucles ya desenrollados.</param>
/// <param name="Repeats">Si al llegar al final vuelve a empezar.</param>
/// <param name="Truncated">
/// Si se dejaron fotogramas fuera por pasarse del tope. Un bucle de 200 dentro de otro de 200
/// son cuarenta mil fotogramas: se puede escribir, y desenrollarlo entero no cabe ni sirve para
/// verlo. Lo que se enseña se corta y se dice.
/// </param>
public sealed record AnimationTimeline(
    IReadOnlyList<TimelineFrame> Frames, bool Repeats, bool Truncated)
{
    /// <summary>Lo que dura una vuelta en interrupciones.</summary>
    public int Ticks => Frames.Sum(frame => frame.Wait);

    /// <summary>Y en milisegundos, que depende de dónde esté enchufada la máquina.</summary>
    public double Milliseconds(int hz) => hz <= 0 ? 0 : Ticks * 1000.0 / hz;

    /// <summary>Los patrones o grupos que toca, para saber qué hay que tener cargado.</summary>
    public IReadOnlyList<int> Targets => [.. Frames.Select(frame => frame.Target).Distinct().Order()];
}

/// <summary>
/// Pone una animación en fila: desenrolla los bucles y aplica el modo.
/// </summary>
/// <remarks>
/// <para>
/// Es lo que hace manejable todo lo demás. Con los pasos tal cual, ir al fotograma anterior
/// obliga a rehacer la cuenta de los bucles desde el principio, y arrastrar por la animación no
/// se puede hacer en absoluto. Resuelta, las dos cosas son un índice.
/// </para>
/// <para>
/// Y es lo que hace que el ping-pong signifique algo. Del revés hay que recorrer los
/// <b>fotogramas</b>, no los pasos: los pasos llevan bucles, y un bucle del revés no es nada.
/// </para>
/// </remarks>
public static class AnimationResolver
{
    /// <summary>
    /// Hasta dónde se desenrolla.
    /// </summary>
    /// <remarks>
    /// No es un límite de lo que se puede escribir, que eso lo decide la máquina: es hasta dónde
    /// tiene sentido resolver para verlo. Nadie mira cuarenta mil fotogramas de vista previa.
    /// </remarks>
    public const int MaxFrames = 1024;

    public static AnimationTimeline Of(SpriteAnimation animation)
    {
        var frames = new List<TimelineFrame>();
        bool truncated = !Walk(animation.Steps, frames);

        if (animation.Mode == AnimationMode.PingPong)
            frames.AddRange(Back(frames));

        return new AnimationTimeline(frames, animation.Mode != AnimationMode.Single, truncated);
    }

    /// <summary>
    /// La vuelta atrás del ping-pong, sin repetir los extremos.
    /// </summary>
    /// <remarks>
    /// Sin el primero y sin el último a propósito: incluyéndolos, esos dos fotogramas se ven el
    /// doble de tiempo que los demás en cada vuelta y la animación cojea en las puntas. Con tres
    /// fotogramas o menos no hay vuelta que dar.
    /// </remarks>
    private static IEnumerable<TimelineFrame> Back(List<TimelineFrame> frames) =>
        frames.Count < 3 ? [] : Enumerable.Range(1, frames.Count - 2).Reverse().Select(at => frames[at]);

    /// <summary>Recorre los pasos y va soltando fotogramas. Devuelve si cupieron todos.</summary>
    private static bool Walk(IEnumerable<AnimationStep> steps, List<TimelineFrame> frames)
    {
        foreach (AnimationStep step in steps)
        {
            if (frames.Count >= MaxFrames)
                return false;

            switch (step)
            {
                case AnimationFrame frame:
                    frames.Add(new TimelineFrame(
                        frame.Target, frame.Wait, frame.OffsetX, frame.OffsetY, frame.Event));

                    break;

                case AnimationLoop loop:
                    for (int time = 0; time < loop.Times; time++)
                    {
                        if (!Walk(loop.Steps, frames))
                            return false;
                    }

                    break;
            }
        }

        return frames.Count <= MaxFrames;
    }
}
