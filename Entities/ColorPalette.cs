using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.Entities;

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

    /// <summary>
    /// El color con el que se ve un índice, con el 0 resuelto contra el fondo.
    /// </summary>
    /// <remarks>
    /// El código de color 0 del VDP es transparente y deja ver el color del borde, y eso
    /// vale igual para la tabla de colores de los sprites que para la de los tiles: es
    /// la misma tabla de códigos. Vive aquí y no en un renderizador porque es una regla
    /// de la paleta, no de lo que se esté dibujando.
    /// </remarks>
    public Color Resolve(int index, Color background) => index == 0 ? background : GetColor(index);

    /// <inheritdoc cref="Resolve(int, Color)"/>
    public IBrush ResolveBrush(int index, IBrush background) => index == 0 ? background : GetBrush(index);

    /// <summary>Colores elegibles como fondo del lienzo y de las miniaturas: del 1 al F.</summary>
    public IReadOnlyList<PaletteColor> BackgroundChoices => _backgroundChoices;

    /// <summary>
    /// Fondo con el que arranca un editor: el color más oscuro de la paleta.
    /// </summary>
    /// <remarks>
    /// Un editor no puede fijar el fondo en el índice 1 y darlo por negro. Lo es en la
    /// paleta estándar del MSX, pero en una generada a partir de un png el 1 es el primer
    /// color de la imagen, y entonces todo lo que use el código 0 —que es justo lo que
    /// estaba vacío— se vería de ese color.
    /// </remarks>
    public int DefaultBackgroundIndex
    {
        get
        {
            PaletteColor darkest = _backgroundChoices[0];

            foreach (PaletteColor color in _backgroundChoices)
            {
                // A igual oscuridad gana el índice menor: en la estándar, el negro del 1.
                if (Darkness(color) < Darkness(darkest))
                    darkest = color;
            }

            return darkest.Index;
        }
    }

    public int Count => _colors.Length;

    public PaletteColor this[int index] => _colors[index];

    public Color GetColor(int index) => _colors[index].Color;

    public IBrush GetBrush(int index) => _colors[index].Brush;

    /// <summary>La paleta fija del MSX, de sólo lectura.</summary>
    public static ColorPalette CreateMsxStandard() => new(
        StandardName,
        isReadOnly: true,
        MsxColors.Select((c, i) => new PaletteColor(i, c.Name, c.R, c.G, c.B, isLocked: true)));

    /// <summary>
    /// Se puede arrastrar esa entrada, o soltar algo encima.
    /// </summary>
    /// <remarks>
    /// El 0 no: no es un color sino «no pintes aquí», y llevárselo al 5 no movería un
    /// color de sitio, cambiaría lo que significan las dos ranuras.
    /// </remarks>
    public bool CanSwap(int index) =>
        !IsReadOnly && index > 0 && index < _colors.Length;

    /// <summary>
    /// Intercambia dos colores de ranura. Devuelve si se ha hecho.
    /// </summary>
    /// <remarks>
    /// Para cuando ya llevas medio juego de tiles dibujado y te das cuenta de que los
    /// colores tenían que estar en otros índices, para que salgan los OR de los sprites.
    /// Esto mueve los colores; los dibujos que los usaban se quedan apuntando al índice
    /// de antes y hay que reajustarlos aparte.
    /// </remarks>
    public bool Swap(int one, int other)
    {
        if (one == other || !CanSwap(one) || !CanSwap(other))
            return false;

        // Por una copia: sin ella, el segundo se quedaría con lo que le acaba de dejar
        // el primero y los dos acabarían del mismo color.
        PaletteColor kept = _colors[one].Clone();

        _colors[one].TakeFrom(_colors[other]);
        _colors[other].TakeFrom(kept);

        return true;
    }

    /// <summary>
    /// Una copia editable, con sus propios colores.
    /// </summary>
    /// <remarks>
    /// Los nombres se copian, porque los ha escrito el usuario y describen lo que él
    /// quiso. Los de la paleta del MSX no: ésos son de la máquina —«Medium green» es el
    /// color 2 del TMS9918, no una etiqueta— y en una paleta propia el índice 2 acaba
    /// siendo cualquier otra cosa. Una copia de la del MSX arranca sin nombres.
    /// </remarks>
    public ColorPalette Clone(string name) => new(
        name,
        isReadOnly: false,
        _colors.Select(color => color.Clone(withName: !IsReadOnly)));

    public override string ToString() => Name;

    /// <summary>Lo oscuro que es un color, sumando sus tres componentes del VDP.</summary>
    private static int Darkness(PaletteColor color) => color.Red + color.Green + color.Blue;

    private void OnColorChanged(PaletteColor color) => ColorsChanged?.Invoke(this);
}
