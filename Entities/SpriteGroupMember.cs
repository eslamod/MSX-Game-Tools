using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.Entities;

/// <summary>
/// Un sprite dentro de un grupo: qué patrón usa, dónde se coloca respecto al grupo y
/// con qué atributos se dibuja.
/// </summary>
/// <remarks>
/// Los colores son suyos, no del patrón. Se siembran del patrón al crear el miembro
/// y a partir de ahí son independientes: es lo que permite usar el mismo dibujo dos
/// veces con colores distintos, que es de lo que va todo esto.
/// </remarks>
public partial class SpriteGroupMember : ObservableObject
{
    public const int MinOffset = -15;
    public const int MaxOffset = 15;

    private int _patternIndex;
    private int _offsetX;
    private int _offsetY;

    /// <summary>
    /// Si este plano se dibuja en la composición.
    /// </summary>
    /// <remarks>
    /// Ayuda de edición, no un atributo del hardware: en el VDP un plano o está o no
    /// está. Por eso no se guarda en el banco ni sale en la exportación, y al abrir un
    /// banco todos los planos se ven.
    /// </remarks>
    [ObservableProperty]
    private bool _isVisible = true;

    public SpriteGroupMember(int patternIndex, Sprite pattern)
    {
        _patternIndex = patternIndex;

        Rows = new SpriteAttributeRow[Sprite.Rows];

        for (int row = 0; row < Sprite.Rows; row++)
        {
            Rows[row] = new SpriteAttributeRow { Color = pattern.ArraySpriteRows[row].Color };

            // Cambiar el color o el CC de una línea cambia cómo se ve el grupo, y el
            // grupo sólo escucha a sus miembros: hay que reemitirlo desde aquí.
            Rows[row].PropertyChanged += (_, _) => OnPropertyChanged(nameof(Rows));
        }
    }

    /// <summary>Posición del patrón en el banco.</summary>
    public int PatternIndex
    {
        get => _patternIndex;
        set => SetProperty(ref _patternIndex, value);
    }

    /// <summary>Desplazamiento horizontal respecto al grupo, en pixeles.</summary>
    public int OffsetX
    {
        get => _offsetX;
        set => SetProperty(ref _offsetX, Clamp(value));
    }

    /// <summary>Desplazamiento vertical respecto al grupo, en pixeles.</summary>
    public int OffsetY
    {
        get => _offsetY;
        set => SetProperty(ref _offsetY, Clamp(value));
    }

    /// <summary>Los atributos de las 16 líneas del sprite.</summary>
    public SpriteAttributeRow[] Rows { get; }

    /// <summary>Vuelve a sembrar los colores desde el patrón indicado.</summary>
    public void CopyColorsFrom(Sprite pattern)
    {
        for (int row = 0; row < Sprite.Rows; row++)
            Rows[row].Color = pattern.ArraySpriteRows[row].Color;
    }

    private static int Clamp(int offset) => Math.Clamp(offset, MinOffset, MaxOffset);
}
