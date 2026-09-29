using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// The fixed-width typeface of hex values and columns of numbers.
/// </summary>
/// <remarks>
/// The views used to name Consolas, which only Windows has. Here that looked fine, because
/// this machine has it; on macOS and Linux the text fell back to a proportional font and the
/// columns stopped lining up. What these tests hold is that the typeface comes from one
/// resource with a fixed-width font for each system, and that no view goes back to naming its
/// own. Whether Menlo and monospace resolve on a Mac and on Linux cannot be seen from here.
/// </remarks>
public class CodeFontTests
{
    /// <summary>Every character as wide as the next, which is what lines the columns up.</summary>
    [AvaloniaFact]
    public void La_letra_de_codigo_es_de_ancho_fijo()
    {
        var font = (FontFamily)Application.Current!.FindResource("CodeFont")!;

        double narrow = Width("iiiiiiii", font);
        double wide = Width("WWWWWWWW", font);

        Assert.True(
            Math.Abs(narrow - wide) < 0.01,
            $"Eight i measure {narrow} and eight W measure {wide}: {font} is not fixed width");
    }

    /// <summary>The views take it from the resource, and none names a font of its own.</summary>
    [AvaloniaFact]
    public void Ninguna_vista_nombra_una_letra_por_su_cuenta()
    {
        var named = new List<string>();

        foreach (string file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "Views"), "*.axaml"))
        {
            string[] lines = File.ReadAllLines(file);

            for (int i = 0; i < lines.Length; i++)
            {
                // A font written out, not a {StaticResource} or a {DynamicResource}.
                if (Regex.IsMatch(lines[i], @"FontFamily=""[^{]"))
                    named.Add($"{Path.GetFileName(file)}:{i + 1}");
            }
        }

        Assert.True(named.Count == 0, "Views naming their own font: " + string.Join(", ", named));
    }

    private static double Width(string text, FontFamily font) =>
        new TextLayout(text, new Typeface(font), 12, Brushes.Black).Width;

    /// <summary>The root of the repository, where the views are.</summary>
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
