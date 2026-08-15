using Avalonia;
using Avalonia.Styling;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Lleva la variante elegida a Avalonia.
/// </summary>
/// <remarks>
/// La traducción va aparte de la aplicación para poder probarla sin aplicación montada:
/// <see cref="Apply"/> toca estado global del proceso —la variante es de toda la
/// aplicación, no de una ventana— y eso en un test se queda puesto para el siguiente.
/// </remarks>
public static class AppTheme
{
    /// <summary>
    /// La variante azulada, que hereda de la oscura.
    /// </summary>
    /// <remarks>
    /// La clave tiene que ser la misma que la del diccionario de <c>AppColors.axaml</c>.
    /// Lo que no se encuentre con esta clave se busca en la heredada, y eso vale también
    /// para los recursos de Fluent, que sólo conocen la clara y la oscura.
    /// </remarks>
    public static ThemeVariant Blue { get; } = new("Blue", ThemeVariant.Dark);

    /// <inheritdoc cref="Blue"/>
    public static ThemeVariant Orange { get; } = new("Orange", ThemeVariant.Dark);

    /// <inheritdoc cref="Blue"/>
    public static ThemeVariant LightBlue { get; } = new("LightBlue", ThemeVariant.Light);

    /// <inheritdoc cref="Blue"/>
    public static ThemeVariant LightOrange { get; } = new("LightOrange", ThemeVariant.Light);

    /// <summary>La variante de Avalonia que corresponde a la elegida.</summary>
    /// <remarks>
    /// «Del sistema» es <see cref="ThemeVariant.Default"/>: Avalonia mira entonces lo que
    /// diga el escritorio. No es lo mismo que Light, aunque hoy se vean igual en una
    /// máquina configurada en claro.
    /// </remarks>
    public static ThemeVariant ToAvalonia(AppThemeVariant variant) => variant switch
    {
        AppThemeVariant.Light => ThemeVariant.Light,
        AppThemeVariant.Dark => ThemeVariant.Dark,
        AppThemeVariant.Blue => Blue,
        AppThemeVariant.Orange => Orange,
        AppThemeVariant.LightBlue => LightBlue,
        AppThemeVariant.LightOrange => LightOrange,
        _ => ThemeVariant.Default,
    };

    /// <summary>Deja puesta la variante en la aplicación, si hay una.</summary>
    public static void Apply(AppThemeVariant variant)
    {
        if (Application.Current is { } application)
            application.RequestedThemeVariant = ToAvalonia(variant);
    }
}
