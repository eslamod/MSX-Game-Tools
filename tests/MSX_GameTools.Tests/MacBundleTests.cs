using System.Xml;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Headless.XUnit;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// The Info.plist of the Mac bundle, against the program it wraps and against what the READMEs
/// promise.
/// </summary>
/// <remarks>
/// The bundle is only put together by the release workflow, on a Mac, and nothing here builds
/// it. What can be held from here is that its Info.plist names things as they are.
/// </remarks>
public class MacBundleTests
{
    /// <summary>
    /// The bundle runs the file that publishing produces, which is named after the assembly.
    /// </summary>
    /// <remarks>If the two differ, macOS finds nothing to run and the program does not open.</remarks>
    [AvaloniaFact]
    public void El_bundle_de_mac_arranca_el_ejecutable_que_sale_al_publicar()
    {
        Assert.Equal(typeof(App).Assembly.GetName().Name, Plist("CFBundleExecutable"));
    }

    /// <summary>
    /// The program is called the same in the application menu as in the bundle.
    /// </summary>
    /// <remarks>
    /// The menu bar takes the name of the bundle, and About, Hide and Quit the one of the
    /// application: two names for one program if they differ.
    /// </remarks>
    [AvaloniaFact]
    public void El_menu_de_mac_llama_al_programa_como_el_bundle()
    {
        Assert.Equal(Plist("CFBundleName"), Application.Current!.Name);
    }

    /// <summary>The READMEs promise the macOS that the bundle asks for.</summary>
    [AvaloniaFact]
    public void Los_readme_prometen_el_macos_que_pide_el_bundle()
    {
        string major = Plist("LSMinimumSystemVersion").Split('.')[0];

        Assert.Contains($"macOS {major} or later", File.ReadAllText(Path.Combine(RepoRoot(), "README.md")));
        Assert.Contains($"macOS {major} o posterior", File.ReadAllText(Path.Combine(RepoRoot(), "README.es.md")));
    }

    /// <summary>A value of the Info.plist, by its key.</summary>
    private static string Plist(string key)
    {
        // The DOCTYPE of every plist names Apple's DTD, which there is no need to fetch.
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };

        using XmlReader reader = XmlReader.Create(
            Path.Combine(RepoRoot(), "Packaging", "macOS", "Info.plist"), settings);

        XElement name = XDocument.Load(reader).Root!.Element("dict")!
            .Elements("key")
            .Single(one => one.Value == key);

        return name.ElementsAfterSelf().First().Value;
    }

    /// <summary>The root of the repository.</summary>
    private static string RepoRoot()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);

        while (folder is not null
               && !File.Exists(Path.Combine(folder.FullName, "MSX_GameTools.csproj")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
            ?? throw new DirectoryNotFoundException("The root of the repository was not found.");
    }
}
