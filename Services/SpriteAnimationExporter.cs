using System.Text;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Vuelca las animaciones de un banco a la tira de bytes que recorre el juego.
/// </summary>
/// <remarks>
/// <para>
/// <b>Los bucles salen como bucles.</b> Se podrían estirar aquí y dejar la tira lista para leer
/// de corrido, pero un bucle de doscientas vueltas ocuparía doscientas veces lo mismo en ROM.
/// Salen tal cual y las vueltas las cuenta el juego, que es donde no cuestan nada.
/// </para>
/// <para>
/// <b>Un fotograma ocupa tres bytes o seis.</b> Lo normal es no moverse ni avisar de nada, y
/// pagar en cada fotograma los tres bytes del desplazamiento y el aviso para dejarlos a cero es
/// justo lo que no interesa. Quien los usa ya paga por ellos, y al juego le cuesta un salto.
/// </para>
/// <para>
/// <b>Los grupos salen por su sitio, no por su número.</b> Dentro se conocen por un número que
/// no se mueve al borrar otros; fuera se exportan en orden y el juego los cuenta. Un grupo que
/// ya no existe sale como <see cref="NoGroup"/>, que no es el sitio de ninguno porque no caben
/// tantos, y además se puede preguntar antes con <see cref="MissingGroups"/>.
/// </para>
/// <para>
/// <b>No hay cuenta de animaciones</b>, igual que en los grupos: se recorre de
/// <c>_animations</c> a <c>_animations_end</c> y cada una acaba en su <see cref="End"/>.
/// </para>
/// </remarks>
public static class SpriteAnimationExporter
{
    /// <summary>Se acabó la animación.</summary>
    public const byte End = 0x00;

    /// <summary>Enseña esto y espera: a qué apunta y cuánto se queda.</summary>
    public const byte Frame = 0x01;

    /// <summary>Igual, pero desplazado o avisando: apunta, espera, Y, X y aviso.</summary>
    public const byte MovedFrame = 0x02;

    /// <summary>Empieza un bucle, y cuántas vueltas da.</summary>
    public const byte LoopStart = 0x03;

    /// <summary>Ahí acaba el bucle.</summary>
    public const byte LoopEnd = 0x04;

    /// <summary>Un grupo que ya no está. No es el sitio de ninguno: no caben tantos.</summary>
    public const byte NoGroup = 0xFF;

    /// <summary>Una animación que apunta a un grupo que ya no existe.</summary>
    public sealed record MissingGroup(string Animation, int Group);

    /// <summary>
    /// Los grupos que alguna animación pide y ya no están.
    /// </summary>
    /// <remarks>
    /// Para poder decirlo antes de escribir el fichero. Borrar un grupo no toca las animaciones
    /// —no las corrige ni las rompe en silencio—, así que el aviso se queda para la exportación,
    /// que es cuando importa de verdad.
    /// </remarks>
    public static IReadOnlyList<MissingGroup> MissingGroups(SpriteBank bank)
    {
        var missing = new List<MissingGroup>();

        foreach (SpriteAnimation animation in bank.Animations)
        {
            if (animation.Kind != AnimationKind.Groups)
                continue;

            foreach (int target in Targets(animation.Steps))
            {
                if (bank.OrdinalOfGroup(target) < 0)
                    missing.Add(new MissingGroup(animation.Name, target));
            }
        }

        return missing;
    }

    public static byte[] ToBinary(SpriteBank bank)
    {
        var bytes = new List<byte>();

        foreach (SpriteAnimation animation in bank.Animations)
        {
            bytes.Add(EndingOf(animation.Mode));

            Emit(bytes, bank, animation, animation.Steps);

            bytes.Add(End);
        }

        return [.. bytes];
    }

    public static string ToAssembler(SpriteBank bank)
    {
        var text = new StringBuilder();
        string label = SpriteBankExporter.LabelOf(bank.Name);

        text.AppendLine($"; Sprite animations - {bank.Name} ({bank.Type})");
        text.AppendLine("; Per animation: 1 byte with what to do when it ends, then its steps.");
        text.AppendLine(";   ending: 0x00 once, 0x01 loop, 0x02 ping-pong");
        text.AppendLine("; Steps:");
        text.AppendLine(";   0x01, target, wait                             show it and hold");
        text.AppendLine(";   0x02, target, wait, offset Y, offset X, event  the same, but moved");
        text.AppendLine(";   0x03, times                                    start of a loop");
        text.AppendLine(";   0x04                                           end of a loop");
        text.AppendLine(";   0x00                                           end of the animation");
        text.AppendLine("; Waits are in interrupts and are never zero.");
        text.AppendLine("; Offsets are two's complement and absolute: each one replaces the one");
        text.AppendLine("; before it, they are not added up.");
        text.AppendLine("; A target is a pattern number, or the position of the group in the group");
        text.AppendLine($"; table - not the number shown in the editor. {Hex(NoGroup)} is a group that is gone.");
        text.AppendLine("; Loops can nest. There is no animation count: walk from");
        text.AppendLine($"; {label}_animations to {label}_animations_end.");
        text.AppendLine();
        text.AppendLine($"{label}_animations:");

        for (int index = 0; index < bank.Animations.Count; index++)
        {
            SpriteAnimation animation = bank.Animations[index];
            string made = animation.Kind == AnimationKind.Groups ? "groups" : "patterns";

            text.AppendLine();
            text.AppendLine($"{label}_animation_{index}:      ; {animation.Name} ({made})");

            Line(text, 1, [EndingOf(animation.Mode)], Ending(animation.Mode));

            Write(text, bank, animation, animation.Steps, 1);

            Line(text, 1, [End], "end");
        }

        text.AppendLine();
        text.AppendLine($"{label}_animations_end:");

        return text.ToString();
    }

    private static string Data => SpriteBankExporter.DataDirective;

    private static string Hex(byte value) => SpriteBankExporter.HexOf(value);

    /// <summary>
    /// El byte del final, puesto a mano y no sacado del número del enumerado.
    /// </summary>
    /// <remarks>
    /// Reordenar el enumerado un día no puede cambiar en silencio lo que ya lee un juego.
    /// </remarks>
    private static byte EndingOf(AnimationMode mode) => mode switch
    {
        AnimationMode.Loop => 0x01,
        AnimationMode.PingPong => 0x02,
        _ => 0x00,
    };

    private static string Ending(AnimationMode mode) => mode switch
    {
        AnimationMode.Loop => "loop",
        AnimationMode.PingPong => "ping-pong",
        _ => "once",
    };

    private static IEnumerable<int> Targets(IEnumerable<AnimationStep> steps)
    {
        foreach (AnimationStep step in steps)
        {
            switch (step)
            {
                case AnimationFrame frame:
                    yield return frame.Target;
                    break;

                case AnimationLoop loop:
                    foreach (int target in Targets(loop.Steps))
                        yield return target;

                    break;
            }
        }
    }

    private static void Emit(
        List<byte> bytes,
        SpriteBank bank,
        SpriteAnimation animation,
        IEnumerable<AnimationStep> steps)
    {
        foreach (AnimationStep step in steps)
        {
            switch (step)
            {
                case AnimationFrame frame:
                    bytes.AddRange(BytesOf(bank, animation, frame));
                    break;

                case AnimationLoop loop:
                    bytes.Add(LoopStart);
                    bytes.Add((byte)loop.Times);

                    Emit(bytes, bank, animation, loop.Steps);

                    bytes.Add(LoopEnd);
                    break;
            }
        }
    }

    private static void Write(
        StringBuilder text,
        SpriteBank bank,
        SpriteAnimation animation,
        IEnumerable<AnimationStep> steps,
        int depth)
    {
        string indent = new(' ', 4 * depth);

        foreach (AnimationStep step in steps)
        {
            switch (step)
            {
                case AnimationFrame frame:
                    string what = animation.Kind == AnimationKind.Groups ? "group" : "pattern";

                    Line(
                        text,
                        depth,
                        BytesOf(bank, animation, frame),
                        $"{what} {frame.Target}, wait {frame.Wait}{Extras(frame)}");

                    break;

                case AnimationLoop loop:
                    Line(text, depth, [LoopStart, (byte)loop.Times], $"loop x{loop.Times}");

                    Write(text, bank, animation, loop.Steps, depth + 1);

                    Line(text, depth, [LoopEnd], "end of the loop");
                    break;
            }
        }
    }

    /// <summary>
    /// Una línea de datos con su comentario, todos a la misma altura.
    /// </summary>
    /// <remarks>
    /// A la misma altura porque las líneas miden distinto —un fotograma quieto ocupa la mitad
    /// que uno desplazado, y la sangría de los bucles corre lo de dentro—, y con los
    /// comentarios a saltos no hay quien siga la columna.
    /// </remarks>
    private static void Line(StringBuilder text, int depth, byte[] bytes, string comment)
    {
        string data = new string(' ', 4 * depth) + $"{Data}  {string.Join(", ", bytes.Select(Hex))}";

        text.AppendLine($"{data.PadRight(CommentColumn)}; {comment}");
    }

    /// <summary>Donde empieza el comentario: un fotograma desplazado dentro de dos bucles.</summary>
    private const int CommentColumn = 52;

    private static string Extras(AnimationFrame frame)
    {
        var parts = new List<string>();

        if (frame.OffsetX != 0 || frame.OffsetY != 0)
            parts.Add($"at {frame.OffsetX:+#;-#;0},{frame.OffsetY:+#;-#;0}");

        if (frame.Event != 0)
            parts.Add($"event {frame.Event}");

        return parts.Count == 0 ? string.Empty : $", {string.Join(", ", parts)}";
    }

    private static byte[] BytesOf(SpriteBank bank, SpriteAnimation animation, AnimationFrame frame)
    {
        byte target = TargetOf(bank, animation, frame.Target);

        if (frame.OffsetX == 0 && frame.OffsetY == 0 && frame.Event == 0)
            return [Frame, target, (byte)frame.Wait];

        return
        [
            MovedFrame,
            target,
            (byte)frame.Wait,
            (byte)(sbyte)frame.OffsetY,
            (byte)(sbyte)frame.OffsetX,
            (byte)frame.Event,
        ];
    }

    private static byte TargetOf(SpriteBank bank, SpriteAnimation animation, int target)
    {
        if (animation.Kind != AnimationKind.Groups)
            return (byte)Math.Clamp(target, 0, byte.MaxValue);

        int at = bank.OrdinalOfGroup(target);

        return at < 0 ? NoGroup : (byte)at;
    }
}
