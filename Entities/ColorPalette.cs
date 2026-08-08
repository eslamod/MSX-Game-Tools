using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Una paleta de 16 colores con nombre. La paleta estándar del MSX (los colores fijos
/// del TMS9918, que son los que carga por defecto el V9938) va marcada de sólo lectura
/// para que siempre quede una referencia de los colores reales de la máquina.
/// </summary>
public sealed class ColorPalette : ObservableObject
{
    public const int Size = 16;

    /// <summary>Nombre de la paleta estándar, que no se puede editar ni eliminar.</summary>
    public const string StandardName = "MSX";

    // Componentes en el formato nativo del MSX, 3 bits (0-7) por canal.
    private static readonly (string Name, int R, int G, int B)[] MsxColors =
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

    private string _name;

    public ColorPalette(string name, bool isReadOnly, IEnumerable<PaletteColor> colors)
    {
        _name = name;
        IsReadOnly = isReadOnly;
        _colors = [.. colors];

        if (_colors.Length != Size)
            throw new ArgumentException($"Una paleta son {Size} colores.", nameof(colors));

        // El 0 no sirve de fondo: dejaría el editor entero invisible.
        _backgroundChoices = _colors[1..];

        foreach (PaletteColor color in _colors)
            color.Changed += OnColorChanged;
    }

    /// <summary>Ha cambiado alguno de los colores de la paleta.</summary>
    public event Action<ColorPalette>? ColorsChanged;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsReadOnly { get; }

    /// <summary>Los 16 colores, en orden de índice.</summary>
    public IReadOnlyList<PaletteColor> Colors => _colors;

    /// <summary>Colores elegibles como fondo del lienzo y de las miniaturas: del 1 al F.</summary>
    public IReadOnlyList<PaletteColor> BackgroundChoices => _backgroundChoices;

    public int Count => _colors.Length;

    public PaletteColor this[int index] => _colors[index];

    public Color GetColor(int index) => _colors[index].Color;

    public IBrush GetBrush(int index) => _colors[index].Brush;

    /// <summary>La paleta fija del MSX, de sólo lectura.</summary>
    public static ColorPalette CreateMsxStandard() => new(
        StandardName,
        isReadOnly: true,
        MsxColors.Select((c, i) => new PaletteColor(i, c.Name, c.R, c.G, c.B)));

    /// <summary>Una copia editable, con sus propios colores.</summary>
    public ColorPalette Clone(string name) => new(
        name,
        isReadOnly: false,
        _colors.Select(color => color.Clone()));

    public override string ToString() => Name;

    private void OnColorChanged(PaletteColor color) => ColorsChanged?.Invoke(this);
}
