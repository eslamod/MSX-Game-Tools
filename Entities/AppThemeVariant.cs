using System.Text.Json.Serialization;

namespace MSX_GameTools.Entities;

/// <summary>
/// Con qué variante se pinta la interfaz.
/// </summary>
/// <remarks>
/// Se guarda por nombre y no por número —de ahí el convertidor—: el fichero de ajustes se
/// lee a mano cuando algo va mal, y un <c>2</c> ahí no dice nada.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<AppThemeVariant>))]
public enum AppThemeVariant
{
    /// <summary>La que diga el sistema operativo.</summary>
    System,

    Light,

    Dark,

    /// <summary>
    /// Oscura con los grises virados a azul.
    /// </summary>
    /// <remarks>
    /// Hereda de la oscura: lo que no redefina —los colores semánticos, los avisos— sale
    /// de allí, así que teñir una variante cuesta los colores que de verdad cambian y no
    /// la lista entera.
    /// </remarks>
    Blue,
}
