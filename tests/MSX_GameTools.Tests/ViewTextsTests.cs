using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Que no queden textos escritos a mano en las vistas.
/// </summary>
/// <remarks>
/// Traducir es fácil de deshacer sin darse cuenta: se añade un botón con su texto puesto
/// ahí mismo y nadie se entera hasta que alguien usa el programa en otro idioma y ve media
/// ventana en español. Esto lo mira en el XAML, que es donde pasa.
/// </remarks>
public class ViewTextsTests
{
    /// <summary>
    /// Lo que se escribe igual en todos los idiomas y no cuenta.
    /// </summary>
    /// <remarks>
    /// El nombre del programa y una palabra que ya es la misma en los tres. Lo demás lo
    /// decide contar letras.
    /// </remarks>
    private static readonly string[] Same = ["MSX Game Tools", "Zoom"];

    /// <summary>Los atributos que acaban en pantalla.</summary>
    private static readonly Regex Visible = new(
        @"(?<!SizeTo)(Text|Header|Content|Watermark|PlaceholderText|ToolTip\.Tip)=""(?<value>[^""{][^""]*)""",
        RegexOptions.Compiled);

    [AvaloniaFact]
    public void Ninguna_vista_tiene_textos_escritos_a_mano()
    {
        var loose = new List<string>();

        foreach (string file in Directory.GetFiles(ViewsFolder, "*.axaml"))
        {
            foreach (Match match in Visible.Matches(File.ReadAllText(file)))
            {
                string value = match.Groups["value"].Value;

                if (IsWords(value))
                    loose.Add($"{Path.GetFileName(file)}: {value}");
            }
        }

        Assert.Empty(loose);
    }

    /// <summary>
    /// Si eso es texto para leer o un adorno.
    /// </summary>
    /// <remarks>
    /// Por número de letras: «X1», «OR», «&lt;» o « x » son lo mismo en cualquier idioma, y
    /// tres letras seguidas ya son una palabra. Es tosco, pero acierta y no hay que
    /// mantener una lista de excepciones que nadie va a recordar actualizar.
    /// </remarks>
    private static bool IsWords(string value) =>
        value.Count(char.IsLetter) >= 3 && !Same.Contains(value.Trim());

    /// <summary>
    /// Dónde están las vistas, contando desde donde se ejecutan las pruebas.
    /// </summary>
    /// <remarks>
    /// Si algún día se mueven, esto revienta con su motivo en vez de no encontrar ficheros
    /// y dar por buena una comprobación que no ha comprobado nada.
    /// </remarks>
    private static string ViewsFolder
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Views")))
                directory = directory.Parent;

            Assert.True(
                directory is not null,
                $"No se encuentra la carpeta Views subiendo desde {AppContext.BaseDirectory}.");

            return Path.Combine(directory!.FullName, "Views");
        }
    }
}
