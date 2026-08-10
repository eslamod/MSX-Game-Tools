namespace MSX_SpritesEditor.Entities;

/// <summary>
/// Ajustes de presentación que se mantienen al cambiar de pestaña.
/// </summary>
/// <remarks>
/// <para>
/// El TabControl reconstruye la vista cada vez que se cambia de pestaña, así que sin
/// guardar esto en algún sitio el zoom volvía a X1 constantemente.
/// </para>
/// <para>
/// Es un objeto del espacio de trabajo y no un estático, aunque un estático bastaría
/// para la aplicación: con estáticos el zoom que deja un test se lo encuentra el
/// siguiente, y los tests pasan a depender del orden en que se ejecuten.
/// </para>
/// <para>
/// Uno solo para todas las pestañas, no uno por banco: lo que se quiere es seguir
/// trabajando con el zoom que estabas usando, no que cada elemento recuerde el suyo.
/// </para>
/// </remarks>
public sealed class EditorPreferences
{
    /// <summary>Índice del zoom del lienzo de sprites, 0-2.</summary>
    public int SpriteCanvasZoom { get; set; }

    /// <summary>Factor del zoom de las miniaturas de sprites: 1, 2, 3 o 4.</summary>
    public int SpriteThumbnailZoom { get; set; } = 1;

    /// <inheritdoc cref="SpriteCanvasZoom"/>
    public int TileCanvasZoom { get; set; }

    /// <inheritdoc cref="SpriteThumbnailZoom"/>
    public int TileThumbnailZoom { get; set; } = 1;
}
