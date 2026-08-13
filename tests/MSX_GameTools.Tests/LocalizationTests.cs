using System.Collections;
using System.Globalization;
using System.Resources;
using Avalonia.Headless.XUnit;
using MSX_GameTools.Localization;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Los textos traducidos y el cambio de idioma en caliente.
/// </summary>
/// <remarks>
/// El <see cref="Localizer"/> es único para todo el programa, así que estas pruebas lo
/// dejan como estaba al terminar: si no, el idioma que deja una se lo encuentra la
/// siguiente y empiezan a depender del orden.
/// </remarks>
public class LocalizationTests : IDisposable
{
    private readonly string _before = Localizer.Instance.Language;

    public void Dispose() => Localizer.Instance.Language = _before;

    [AvaloniaFact]
    public void Cada_idioma_da_su_texto()
    {
        Localizer.Instance.Language = "es";

        Assert.Equal("Archivo", Localizer.Instance["MenuFile"]);

        Localizer.Instance.Language = "en";

        Assert.Equal("File", Localizer.Instance["MenuFile"]);

        Localizer.Instance.Language = "ca";

        Assert.Equal("Fitxer", Localizer.Instance["MenuFile"]);
    }

    /// <summary>Es lo que hace que los enlaces del XAML se relean sin reiniciar.</summary>
    [AvaloniaFact]
    public void Cambiar_de_idioma_avisa_de_que_todos_los_textos_cambian()
    {
        Localizer.Instance.Language = "es";

        var changed = new List<string?>();

        Localizer.Instance.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Localizer.Instance.Language = "en";

        Assert.Contains("Item[]", changed);
    }

    [AvaloniaFact]
    public void Un_idioma_que_no_hablamos_se_ignora()
    {
        Localizer.Instance.Language = "es";
        Localizer.Instance.Language = "de";

        Assert.Equal("es", Localizer.Instance.Language);
    }

    /// <summary>Un hueco sin traducir tiene que cantar, no salir en blanco.</summary>
    [AvaloniaFact]
    public void Un_texto_que_falta_sale_con_su_clave()
    {
        Assert.Equal("EstoNoExiste", Localizer.Instance["EstoNoExiste"]);
    }

    /// <summary>
    /// Los tres ficheros tienen que traer las mismas claves.
    /// </summary>
    /// <remarks>
    /// Preguntando por el <see cref="Localizer"/> esto no se puede comprobar: cuando a un
    /// idioma le falta una clave, <c>ResourceManager</c> devuelve la del neutro, así que
    /// una traducción olvidada sale en español y parece que está. Hay que mirar el fichero
    /// de cada idioma por su cuenta, sin dejarle caer al de al lado.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("ca")]
    public void Ningun_idioma_se_deja_textos_por_traducir(string language)
    {
        IReadOnlyList<string> neutral = [.. KeysOf(CultureInfo.InvariantCulture)];
        IReadOnlyList<string> translated = [.. KeysOf(new CultureInfo(language))];

        Assert.NotEmpty(neutral);
        Assert.Empty(neutral.Except(translated));

        // Y al revés: una clave que sobre es una que se quitó del neutro y se olvidó aquí.
        Assert.Empty(translated.Except(neutral));
    }

    /// <summary>
    /// Las claves que trae ese idioma y sólo ese.
    /// </summary>
    /// <remarks>
    /// Con <c>tryParents: false</c>, que es lo que impide que las que falten se rellenen
    /// con las del neutro y la comprobación deje de comprobar nada.
    /// </remarks>
    private static IEnumerable<string> KeysOf(CultureInfo culture) =>
        new ResourceManager("MSX_GameTools.Localization.Strings", typeof(Localizer).Assembly)
            .GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?.Cast<DictionaryEntry>()
            .Select(entry => (string)entry.Key)
        ?? [];
}
