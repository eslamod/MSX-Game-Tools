using System.Text.Json;

namespace MSX_GameTools.Services;

/// <summary>Qué guarda un fichero del editor.</summary>
public enum EditorFileKind
{
    SpriteBank,
    TileSet,
    Map,
    Palette,
}

/// <summary>
/// Qué hay dentro de un fichero del editor, mirándolo.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que Abrir sea uno solo. Antes había cuatro entradas de menú —cargar banco,
/// cargar juego de tiles, cargar mapa, cargar paleta— y había que acertar con la de tu
/// fichero; si te equivocabas te decía que no era válido aunque estuviera perfecto. Eso no
/// es una pregunta que haya que hacerle a nadie: el programa puede mirarlo.
/// </para>
/// <para>
/// Se distingue por una propiedad de primer nivel, que hoy es única en cada formato. Un
/// juego de tiles lleva paleta dentro, pero anidada, no arriba; y las filas de un mapa se
/// llaman <c>tiles</c> pero cuelgan de sus capas. Si algún día dos formatos coincidieran
/// arriba, tocaría escribir de qué es cada fichero en él mismo.
/// </para>
/// </remarks>
public static class EditorFile
{
    /// <summary>
    /// La propiedad que delata a cada formato.
    /// </summary>
    /// <remarks>
    /// El orden no significa nada: hoy ninguna de las cuatro sale en más de un formato, y
    /// quien lo vigila es la prueba de que cada uno se reconoce como lo que es. Si algún
    /// día dos coincidieran, esto dejaría de bastar y habría que escribir de qué es cada
    /// fichero dentro de él; poner una antes que otra sólo escondería el problema.
    /// </remarks>
    private static readonly (string Property, EditorFileKind Kind)[] Signs =
    [
        ("patterns", EditorFileKind.SpriteBank),
        ("tiles", EditorFileKind.TileSet),
        ("layers", EditorFileKind.Map),
        ("colors", EditorFileKind.Palette),
    ];

    /// <summary>Qué es, o <c>null</c> si no lo reconoce.</summary>
    public static EditorFileKind? KindOf(string json)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach ((string property, EditorFileKind kind) in Signs)
            {
                if (document.RootElement.TryGetProperty(property, out _))
                    return kind;
            }
        }

        return null;
    }
}
