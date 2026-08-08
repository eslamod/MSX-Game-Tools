using Avalonia.Media;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Una entrada de la paleta del MSX.
/// </summary>
/// <remarks>
/// Las componentes se guardan en el formato nativo del VDP: 3 bits cada una, 0-7.
/// El V9938 del MSX2 maneja sus 512 colores con exactamente este formato (9 bits),
/// así que guardarlas así y expandirlas a 8 bits sólo para pintar es lo que permitirá
/// más adelante editar la paleta sin rehacer nada.
/// </remarks>
public sealed class PaletteColor
{
    public PaletteColor(int index, string name, byte red, byte green, byte blue, bool isTransparent)
    {
        Index = index;
        Name = name;
        Red = red;
        Green = green;
        Blue = blue;
        IsTransparent = isTransparent;

        Color = isTransparent
            ? Colors.Transparent
            : Color.FromRgb(Expand(red), Expand(green), Expand(blue));

        Brush = new SolidColorBrush(Color);
        Hex = index.ToString("X1");
    }

    /// <summary>Índice en la paleta, 0-15.</summary>
    public int Index { get; }

    public string Name { get; }

    /// <summary>Componente roja en el formato del MSX, 0-7.</summary>
    public byte Red { get; }

    /// <summary>Componente verde en el formato del MSX, 0-7.</summary>
    public byte Green { get; }

    /// <summary>Componente azul en el formato del MSX, 0-7.</summary>
    public byte Blue { get; }

    /// <summary>
    /// El índice 0 no es un color: la línea del sprite no se dibuja y se ve el fondo.
    /// </summary>
    public bool IsTransparent { get; }

    public Color Color { get; }

    public IBrush Brush { get; }

    /// <summary>Dígito hexadecimal del índice, "0" a "F".</summary>
    public string Hex { get; }

    public override string ToString() => $"{Hex} {Name}";

    /// <summary>
    /// De los 3 bits del MSX (0-7) a los 8 de pantalla, redondeando al entero más
    /// próximo. Truncar daba 72 donde la paleta canónica del MSX tiene 73.
    /// </summary>
    private static byte Expand(byte component) => (byte)(((component * 255) + 3) / 7);
}
