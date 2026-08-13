using System.Globalization;
using System.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.Localization;

/// <summary>
/// Los textos del programa en el idioma elegido.
/// </summary>
/// <remarks>
/// <para>
/// Uno solo para toda la aplicación y con indizador en vez de una propiedad por texto: así
/// cambiar de idioma es avisar de que ha cambiado <c>Item[]</c> y todos los enlaces se
/// releen solos, sin reiniciar ni reconstruir las vistas.
/// </para>
/// <para>
/// Un texto que falte sale con su clave a la vista y no en blanco ni con una excepción:
/// un hueco sin traducir tiene que cantar, no esconderse.
/// </para>
/// </remarks>
public sealed partial class Localizer : ObservableObject
{
    /// <summary>Los idiomas a los que está traducido.</summary>
    /// <remarks>
    /// El español es el neutro del fichero de recursos, así que va sin código de cultura.
    /// </remarks>
    public static readonly IReadOnlyList<LanguageChoice> Languages =
    [
        new("es", "Español"),
        new("en", "English"),
        new("ca", "Català"),
    ];

    private static readonly ResourceManager Resources =
        new("MSX_GameTools.Localization.Strings", typeof(Localizer).Assembly);

    private CultureInfo _culture = Default();

    /// <summary>El único que hay. Los enlaces del XAML apuntan aquí.</summary>
    public static Localizer Instance { get; } = new();

    /// <summary>El texto de esa clave, o la clave misma si falta.</summary>
    public string this[string key] => Resources.GetString(key, _culture) ?? key;

    /// <summary>Código del idioma en uso: «es», «en» o «ca».</summary>
    public string Language
    {
        get => _culture.TwoLetterISOLanguageName;
        set
        {
            if (Language == value || !Languages.Any(choice => choice.Code == value))
                return;

            _culture = new CultureInfo(value);

            OnPropertyChanged();

            // Lo que relee todos los textos enlazados: es el nombre que usa un enlace a
            // un indizador para enterarse de que su valor puede haber cambiado.
            OnPropertyChanged("Item[]");
        }
    }

    /// <summary>
    /// El idioma del sistema si lo hablamos, y español si no.
    /// </summary>
    /// <remarks>
    /// Español y no inglés porque es el idioma en el que está escrito el programa y sus
    /// comentarios: si algo se queda sin traducir, encaja con lo que hay alrededor.
    /// </remarks>
    private static CultureInfo Default()
    {
        string system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        return new CultureInfo(Languages.Any(choice => choice.Code == system) ? system : "es");
    }
}

/// <summary>Un idioma al que se puede cambiar, con su nombre en él mismo.</summary>
public sealed record LanguageChoice(string Code, string Name);
