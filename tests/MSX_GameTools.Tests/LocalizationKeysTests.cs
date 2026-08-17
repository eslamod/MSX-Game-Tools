using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using MSX_GameTools.Localization;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Que toda clave que se pide exista de verdad en los tres idiomas.
/// </summary>
/// <remarks>
/// <para>
/// El localizador devuelve la clave cuando no la encuentra, que es lo correcto —mejor un
/// «ImportSheetTitle» en pantalla que reventar— pero también lo que hace que una clave que
/// falta no se note hasta que alguien abre ese panel y lo lee.
/// </para>
/// <para>
/// Nace de once entradas que se colaron <b>dentro</b> de otra en el fichero de recursos. El XML
/// seguía siendo válido y el fichero cargaba, pero el lector sólo mira las hijas de la raíz, así
/// que el formulario entero salía con los nombres de las claves. Comparar los tres idiomas entre
/// sí no lo habría cazado: los tres estaban rotos igual.
/// </para>
/// </remarks>
public class LocalizationKeysTests
{
    /// <summary>Las claves de las vistas: <c>{l:Localize LoQueSea}</c>.</summary>
    private static readonly Regex InViews = new(
        @"\{l:Localize\s+(?<key>\w+)\s*\}", RegexOptions.Compiled);

    /// <summary>
    /// Y las del código: <c>Text["LoQueSea"]</c>, <c>Localizer.Instance["LoQueSea"]</c> y las
    /// dos formas de <c>Format</c>.
    /// </summary>
    private static readonly Regex InCode = new(
        @"(?:Text|Localizer\.Instance)\s*(?:\[\s*""(?<key>\w+)""\s*\]|\.Format\(\s*""(?<key2>\w+)"")",
        RegexOptions.Compiled);

    [AvaloniaTheory]
    [InlineData("es")]
    [InlineData("en")]
    [InlineData("ca")]
    public void Toda_clave_que_se_pide_existe(string language)
    {
        string before = Localizer.Instance.Language;

        try
        {
            Localizer.Instance.Language = language;

            List<string> missing = [.. Keys().Where(Missing).Distinct().Order()];

            Assert.True(missing.Count == 0, $"En {language} faltan: {string.Join(", ", missing)}");
        }
        finally
        {
            Localizer.Instance.Language = before;
        }
    }

    /// <summary>
    /// Sin traducir es que el localizador devuelve la clave tal cual.
    /// </summary>
    /// <remarks>
    /// Se descarta el caso en que la traducción sea de verdad la propia clave, que no lo es
    /// ninguna: los textos llevan espacios, tildes o dos puntos.
    /// </remarks>
    private static bool Missing(string key) => Localizer.Instance[key] == key;

    private static IEnumerable<string> Keys()
    {
        foreach (string file in Files("*.axaml", "Views"))
        {
            foreach (Match match in InViews.Matches(File.ReadAllText(file)))
                yield return match.Groups["key"].Value;
        }

        foreach (string file in Files("*.cs", "ViewModels", "Services", "Views"))
        {
            foreach (Match match in InCode.Matches(File.ReadAllText(file)))
            {
                yield return match.Groups["key"].Success
                    ? match.Groups["key"].Value
                    : match.Groups["key2"].Value;
            }
        }
    }

    private static IEnumerable<string> Files(string pattern, params string[] folders)
    {
        string root = Root();

        return folders
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.GetFiles(folder, pattern, SearchOption.AllDirectories));
    }

    /// <summary>La carpeta del proyecto, subiendo desde donde corren las pruebas.</summary>
    private static string Root()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);

        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "MSX_GameTools.csproj")))
            folder = folder.Parent;

        Assert.NotNull(folder);

        return folder.FullName;
    }
}
