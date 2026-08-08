using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Una línea de la tabla de colores de un sprite del V9938: el color y los tres bits
/// de comportamiento.
/// </summary>
/// <remarks>
/// Se guardan los cuatro campos aunque de momento sólo se editen el color y CC: son
/// el mismo byte en el hardware, y añadirlos después obligaría a cambiar el formato
/// de fichero. En MSX1 no hay tabla de colores y el color es de todo el sprite, así
/// que las 16 líneas comparten valor.
/// </remarks>
public partial class SpriteAttributeRow : ObservableObject
{
    /// <summary>Índice en la paleta, 0-15.</summary>
    [ObservableProperty]
    private int _color;

    /// <summary>CC: el color de esta línea se combina con OR con el sprite de mayor prioridad.</summary>
    [ObservableProperty]
    private bool _combineColor;

    /// <summary>IC: esta línea no participa en la detección de colisiones.</summary>
    [ObservableProperty]
    private bool _inhibitCollision;

    /// <summary>EC: esta línea se dibuja 32 pixeles a la izquierda.</summary>
    [ObservableProperty]
    private bool _earlyClock;

    public SpriteAttributeRow Clone() => new()
    {
        Color = Color,
        CombineColor = CombineColor,
        InhibitCollision = InhibitCollision,
        EarlyClock = EarlyClock,
    };
}
