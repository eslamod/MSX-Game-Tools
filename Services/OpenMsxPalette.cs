using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Lee la paleta tal y como la escupe la consola de openMSX.
/// </summary>
/// <remarks>
/// <para>
/// Son dieciséis parejas <c>índice:RGB</c> sueltas por el texto, con el índice en un dígito
/// hexadecimal y las tres componentes en un dígito cada una, de 0 a 7, que es como las guarda
/// el V9938: tres bits por componente.
/// </para>
/// <code>
/// 0:114  4:225  8:445  c:236
/// 1:300  5:222  9:447  d:333
/// 2:510  6:700  a:770  e:777
/// 3:740  7:117  b:467  f:000
/// </code>
/// <para>
/// Cómo estén repartidas por las líneas da igual: se leen las parejas y no las filas, así que
/// vale pegar la salida tal cual, en cuatro columnas o en una.
/// </para>
/// <para>
/// <b>El color 0 se lee y se guarda</b> aunque el editor no lo enseñe. Ahí el 0 no es un color
/// sino «no pintes aquí», así que no se ve en ninguna parte; pero el juego sí carga sus dos
/// bytes al escribir la paleta, y la exportación los saca. Tirándolo, la paleta exportada no
/// sería la del juego.
/// </para>
/// </remarks>
public static class OpenMsxPalette
{
    /// <summary>Lo más grande que puede valer una componente: tres bits.</summary>
    public const int MaxComponent = PaletteColor.MaxComponent;

    /// <summary>
    /// La paleta que trae el texto.
    /// </summary>
    /// <exception cref="FileFormatException">
    /// Si falta algún color, si sobra, o si algo no tiene la forma que debe.
    /// </exception>
    public static ColorPalette Read(string text, string name)
    {
        var colors = new PaletteColor?[ColorPalette.Size];

        foreach (string token in text.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            (int index, int red, int green, int blue) = Parse(token);

            if (colors[index] is not null)
                throw new FileFormatException($"El color {index:x} está dos veces.");

            colors[index] = new PaletteColor(index, string.Empty, red, green, blue);
        }

        int[] missing = [.. Enumerable
            .Range(0, ColorPalette.Size)
            .Where(index => colors[index] is null)];

        if (missing.Length > 0)
        {
            throw new FileFormatException(
                $"Faltan colores de la paleta: {string.Join(", ", missing.Select(at => $"{at:x}"))}.");
        }

        return new ColorPalette(name, isReadOnly: false, colors!);
    }

    /// <summary>Una pareja <c>índice:RGB</c>.</summary>
    private static (int Index, int Red, int Green, int Blue) Parse(string token)
    {
        string[] halves = token.Split(':');

        if (halves.Length != 2 || halves[0].Length != 1 || halves[1].Length != 3)
        {
            throw new FileFormatException(
                $"«{token}» no es un color de openMSX: se esperaba algo como «3:740».");
        }

        if (!int.TryParse(
                halves[0],
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out int index))
        {
            throw new FileFormatException($"«{halves[0]}» no es un número de color.");
        }

        return (index, Component(token, halves[1][0]), Component(token, halves[1][1]),
            Component(token, halves[1][2]));
    }

    private static int Component(string token, char digit)
    {
        int value = digit - '0';

        // De 0 a 7 y no de 0 a 9: son tres bits. Un 8 o un 9 delatan que eso no es una paleta
        // de openMSX, y colarlo daria un color que en la maquina no existe.
        if (value is < 0 or > MaxComponent)
        {
            throw new FileFormatException(
                $"«{token}» tiene un {digit}, y las componentes van de 0 a {MaxComponent}.");
        }

        return value;
    }
}
