using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Una entrada de la paleta del MSX.
/// </summary>
/// <remarks>
/// Las componentes se guardan en el formato nativo del VDP: 3 bits cada una, 0-7.
/// El V9938 del MSX2 maneja sus 512 colores con exactamente este formato (9 bits).
/// </remarks>
public sealed class PaletteColor : ObservableObject
{
    public const int MinComponent = 0;
    public const int MaxComponent = 7;

    // Siempre el mismo brush: al mutar su Color se repinta solo todo lo que lo tenga
    // enlazado, sin que nadie tenga que enterarse del cambio.
    private readonly SolidColorBrush _brush;

    private string _name;
    private int _red;
    private int _green;
    private int _blue;

    public PaletteColor(int index, string name, int red, int green, int blue)
    {
        Index = index;
        Hex = index.ToString("X1");

        _name = name;
        _red = Clamp(red);
        _green = Clamp(green);
        _blue = Clamp(blue);
        _brush = new SolidColorBrush(Compose());
    }

    /// <summary>Ha cambiado alguna componente de este color.</summary>
    public event Action<PaletteColor>? Changed;

    /// <summary>Índice en la paleta, 0-15.</summary>
    public int Index { get; }

    /// <summary>Dígito hexadecimal del índice, "0" a "F".</summary>
    public string Hex { get; }

    /// <summary>
    /// El índice 0 no es un color: la línea del sprite no se dibuja y se ve el fondo.
    /// Su RGB no se puede editar porque no se llegaría a usar nunca.
    /// </summary>
    public bool IsTransparent => Index == 0;

    public bool IsEditable => !IsTransparent;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>Componente roja en el formato del MSX, 0-7.</summary>
    public int Red
    {
        get => _red;
        set => SetComponent(ref _red, value, nameof(Red));
    }

    /// <summary>Componente verde en el formato del MSX, 0-7.</summary>
    public int Green
    {
        get => _green;
        set => SetComponent(ref _green, value, nameof(Green));
    }

    /// <summary>Componente azul en el formato del MSX, 0-7.</summary>
    public int Blue
    {
        get => _blue;
        set => SetComponent(ref _blue, value, nameof(Blue));
    }

    public Color Color => _brush.Color;

    public IBrush Brush => _brush;

    /// <summary>Las tres componentes, un dígito hexadecimal cada una. Es lo que se guarda en fichero.</summary>
    public string HexRgb => $"{_red:X1}{_green:X1}{_blue:X1}";

    public PaletteColor Clone() => new(Index, _name, _red, _green, _blue);

    public void SetComponents(int red, int green, int blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    public override string ToString() => $"{Hex} {Name}";

    private void SetComponent(ref int field, int value, string propertyName)
    {
        int clamped = Clamp(value);
        if (field == clamped)
            return;

        field = clamped;
        _brush.Color = Compose();

        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(HexRgb));

        Changed?.Invoke(this);
    }

    private Color Compose() => IsTransparent
        ? Colors.Transparent
        : Color.FromRgb(Expand(_red), Expand(_green), Expand(_blue));

    private static int Clamp(int component) => Math.Clamp(component, MinComponent, MaxComponent);

    /// <summary>
    /// De los 3 bits del MSX (0-7) a los 8 de pantalla, redondeando al entero más
    /// próximo. Truncar daba 72 donde la paleta canónica del MSX tiene 73.
    /// </summary>
    private static byte Expand(int component) => (byte)(((component * 255) + 3) / 7);
}
