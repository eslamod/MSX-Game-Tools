using Avalonia.Media;

namespace MSX_GameTools.Entities;

/// <summary>
/// Lo que un lienzo de pintado necesita saber de lo que está editando.
/// </summary>
/// <remarks>
/// El lienzo no sabe si detrás hay un sprite de 16x16 con un color por línea o un tile
/// de 8x8 con dos. Sólo pregunta si un pixel está encendido, lo enciende o lo apaga, y
/// pide con qué pincel dibujarlo. Todo lo demás —de dónde sale el color, qué miniatura
/// hay que repintar, qué se recompone al soltar el ratón— es de quien implemente esto.
/// </remarks>
public interface IPixelSurface
{
    /// <summary>Lado de la rejilla en pixeles. 16 en un sprite, 8 en un tile.</summary>
    int Size { get; }

    bool IsSet(int x, int y);

    /// <summary>
    /// Enciende o apaga el pixel. Es responsabilidad de la implementación mantener al
    /// día lo que dependa de él, como la miniatura.
    /// </summary>
    void Set(int x, int y, bool on);

    /// <summary>Pincel con el que se ve ese pixel ahora mismo, encendido o apagado.</summary>
    IBrush BrushAt(int x, int y);

    /// <summary>
    /// Ha terminado un trazo. Se avisa al soltar el ratón y no por pixel porque lo que
    /// cuelga de aquí (recomponer los grupos que usen el patrón) es caro.
    /// </summary>
    void EndStroke();
}
