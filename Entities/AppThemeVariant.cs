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
}
