using Avalonia.Media;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// La paleta estándar del MSX: los 16 colores fijos del VDP TMS9918, que son también
/// los que carga por defecto el V9938 del MSX2.
/// </summary>
public class ColorPalette
{
    /// <summary>Número de colores de la paleta.</summary>
    public const int Size = 16;

    // Componentes en el formato nativo del MSX, 3 bits (0-7) por canal.
    private static readonly (string Name, byte R, byte G, byte B)[] MsxColors =
    [
        ("Transparent",  0, 0, 0),
        ("Black",        0, 0, 0),
        ("Medium green", 1, 6, 1),
        ("Light green",  3, 7, 3),
        ("Dark blue",    1, 1, 7),
        ("Light blue",   2, 3, 7),
        ("Dark red",     5, 1, 1),
        ("Cyan",         2, 6, 7),
        ("Medium red",   7, 1, 1),
        ("Light red",    7, 3, 3),
        ("Dark yellow",  6, 6, 1),
        ("Light yellow", 6, 6, 4),
        ("Dark green",   1, 4, 1),
        ("Magenta",      6, 2, 5),
        ("Gray",         5, 5, 5),
        ("White",        7, 7, 7),
    ];

    private readonly PaletteColor[] _colors;
    private readonly PaletteColor[] _backgroundChoices;

    public ColorPalette()
    {
        _colors = new PaletteColor[MsxColors.Length];

        for (int i = 0; i < MsxColors.Length; i++)
        {
            (string name, byte r, byte g, byte b) = MsxColors[i];
            _colors[i] = new PaletteColor(i, name, r, g, b, isTransparent: i == 0);
        }

        // El 0 no sirve de fondo: dejaría el editor entero invisible.
        _backgroundChoices = _colors[1..];
    }

    /// <summary>Los 16 colores, en orden de índice.</summary>
    public IReadOnlyList<PaletteColor> Colors => _colors;

    /// <summary>Colores elegibles como fondo del lienzo y de las miniaturas: del 1 al F.</summary>
    public IReadOnlyList<PaletteColor> BackgroundChoices => _backgroundChoices;

    public int Count => _colors.Length;

    public PaletteColor this[int index] => _colors[index];

    public Color GetColor(int index) => _colors[index].Color;

    public IBrush GetBrush(int index) => _colors[index].Brush;
}
