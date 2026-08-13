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

    /// <summary>
    /// Un objeto por clave pedida, para poder avisarles al cambiar de idioma.
    /// </summary>
    /// <remarks>
    /// Concurrente porque esto es único para todo el proceso: en la aplicación lo pide
    /// siempre el hilo de la interfaz, pero las pruebas montan ventanas en paralelo y un
    /// diccionario normal escrito a la vez desde dos sitios se queda girando al leerlo.
    /// </remarks>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, LocalizedText> _texts = new();

    private CultureInfo _culture = Default();

    /// <summary>El único que hay. Los enlaces del XAML apuntan aquí.</summary>
    public static Localizer Instance { get; } = new();

    /// <summary>El texto de esa clave, o la clave misma si falta.</summary>
    public string this[string key] => Resources.GetString(key, _culture) ?? key;

    /// <summary>
    /// El texto de esa clave con los huecos rellenos.
    /// </summary>
    /// <remarks>
    /// Los huecos son sólo para datos —un nombre, un número, una ruta—, nunca para trozos
    /// de frase. «No se pudo guardar el {0}» con el tipo por fuera sólo funciona en
    /// español y de casualidad, porque los tres tipos son masculinos; en cuanto uno es
    /// femenino o el idioma pide otro artículo, no hay forma de traducirlo. Cuando la
    /// frase cambia según de qué hable, se escribe entera una vez por caso.
    /// </remarks>
    public string Format(string key, params object?[] values) =>
        string.Format(_culture, this[key], values);

    /// <summary>
    /// El texto de esa clave como algo a lo que un enlace se puede quedar escuchando.
    /// </summary>
    /// <remarks>
    /// Un objeto por clave, con una propiedad normal, en vez de enlazar al indizador de
    /// aquí: Avalonia no reevalúa un enlace a indizador por mucho que se avise de que ha
    /// cambiado, así que los menús se quedaban en el idioma de antes. Se reutiliza el
    /// mismo objeto para la misma clave, que son unos pocos cientos en todo el programa.
    /// </remarks>
    public LocalizedText Bind(string key) => _texts.GetOrAdd(key, static k => new LocalizedText(k));

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

            // Lo que relee lo que hay en pantalla: cada texto avisa por su cuenta.
            foreach (LocalizedText text in _texts.Values)
                text.Refresh();
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

/// <summary>
/// Un texto traducido que avisa cuando cambia el idioma.
/// </summary>
/// <remarks>
/// Vive dentro del <see cref="Localizer"/>, que es único para todo el proceso, así que
/// todo lo que se enlace aquí queda enraizado desde un objeto que no muere nunca. Vale
/// para lo que dura lo que la ventana —los menús, los paneles— y no para cosas que van y
/// vienen: enlazar aquí cada nodo de un árbol que se reconstruye deja los controles
/// viejos vivos para siempre.
/// </remarks>
public sealed partial class LocalizedText(string key) : ObservableObject
{
    public string Value => Localizer.Instance[key];

    internal void Refresh() => OnPropertyChanged(nameof(Value));
}
