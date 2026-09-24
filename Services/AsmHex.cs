namespace MSX_GameTools.Services;

/// <summary>
/// Cómo se escribe un byte en el ensamblador que sale de aquí.
/// </summary>
/// <remarks>
/// Aparte y no dentro de un exportador porque lo escriben todos igual: los patrones, los
/// colores, los grupos, la paleta y las tablas de los mapas. Vivía en el del banco de
/// sprites, que fue el primero.
/// </remarks>
public static class AsmHex
{
    /// <summary>
    /// Prefijo hexadecimal de la salida en ensamblador.
    /// </summary>
    /// <remarks>
    /// <c>0x</c> porque es el que documenta sasSX y el único sin ambigüedad: <c>$</c> también
    /// vale, pero ahí mismo significa la dirección actual, y <c>#</c> es prefijo de directiva,
    /// no de número.
    /// </remarks>
    public const string Prefix = "0x";

    /// <summary>Un byte, con dos dígitos siempre.</summary>
    public static string Of(byte value) => $"{Prefix}{value:X2}";
}
