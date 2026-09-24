using System.Text;

namespace MSX_GameTools.Services;

/// <summary>
/// The name a document's files take, and the one its labels take inside them.
/// </summary>
/// <remarks>
/// <para>
/// The same one for both on purpose: a map called "Nivel 1" comes out as nivel_1.asm with
/// <c>nivel_1_map:</c> inside it, so the file and what it carries are called the same thing.
/// The panel proposes it and the exporters write it.
/// </para>
/// <para>
/// On its own and not inside the sprite bank exporter, which is where it grew: all four
/// documents name their files with it now, and over there the one thing it could not do was
/// belong to any of them.
/// </para>
/// </remarks>
public static class AsmLabel
{
    /// <summary>
    /// What comes out when nothing of the name survives.
    /// </summary>
    /// <remarks>
    /// The same word <c>CleanFileName</c> uses for the same case. It used to be "sprites",
    /// from when only a bank came through here: a map with no name proposed sprites.bin.
    /// </remarks>
    public const string Unnamed = "unnamed";

    /// <summary>A name that works as a file name and as an assembler label.</summary>
    public static string Of(string name)
    {
        var label = new StringBuilder();

        foreach (char character in name.ToLowerInvariant())
            label.Append(char.IsAsciiLetterOrDigit(character) ? character : '_');

        string result = label.ToString().Trim('_');

        if (result.Length == 0)
            return Unnamed;

        // Una etiqueta no puede empezar por dígito. La letra es una cualquiera —viene de
        // cuando esto sólo nombraba bancos de sprites—, pero cambiarla ahora le movería la
        // etiqueta a quien tenga un documento que empiece por número.
        return char.IsAsciiDigit(result[0]) ? $"s{result}" : result;
    }
}
