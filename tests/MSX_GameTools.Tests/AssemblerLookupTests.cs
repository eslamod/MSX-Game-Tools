using Avalonia.Headless.XUnit;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Cómo se encuentra el ensamblador con el que se validan los exportadores.
/// </summary>
/// <remarks>
/// Estaba puesto con una ruta absoluta, así que fuera de una máquina la prueba de los
/// exportadores se saltaba sin decir nada, y dentro de ella ensamblaba con un binario de otro
/// checkout, de 2022 y de la época de Mono, en vez de con el del submódulo.
/// </remarks>
public class AssemblerLookupTests
{
    /// <summary>
    /// Lo que salga de compilar el submódulo, que es lo que se quiere probar.
    /// </summary>
    /// <remarks>
    /// Release antes que Debug: teniendo los dos, se prueba con el que se distribuye.
    /// </remarks>
    [AvaloniaFact]
    public void Sin_variable_sale_el_del_submodulo()
    {
        string[] built =
        [
            .. new[] { "Release", "Debug" }
                .Select(configuration => Path.Combine(
                    RepoRoot(), "tools", "sass-MSX", "sass", "bin", configuration,
                    Executable("sasSX")))
                .Where(File.Exists),
        ];

        Assert.SkipWhen(
            built.Length == 0, "sasSX sin compilar: dotnet build tools/sass-MSX -c Release");

        Assert.Equal(built[0], Assembler.Find("sasSX", _ => null));
    }

    /// <summary>
    /// Y si dicen dónde está, manda eso.
    /// </summary>
    /// <remarks>
    /// Es lo que permite probar contra otra versión sin tocar el árbol, y lo que hace que
    /// funcione en una máquina que lo tenga en otro sitio.
    /// </remarks>
    [AvaloniaFact]
    public void La_variable_de_entorno_manda()
    {
        string folder = Directory.CreateTempSubdirectory("otro").FullName;

        try
        {
            string mine = Path.Combine(folder, Executable("sasSX"));

            File.WriteAllText(mine, "no es un ensamblador, pero está");

            Assert.Equal(mine, Assembler.Find("sasSX", _ => mine));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// Si la variable apunta a lo que no hay, se para.
    /// </summary>
    /// <remarks>
    /// Seguir buscando ensamblaría con otro binario y la prueba quedaría en verde diciendo que
    /// probó lo que no probó, que es justo el fallo del que viene todo esto.
    /// </remarks>
    [AvaloniaFact]
    public void Si_la_variable_apunta_a_lo_que_no_hay_se_para()
    {
        string missing = Path.Combine(Path.GetTempPath(), "no-esta-aqui-sasSX.exe");

        var complaint = Assert.Throws<FileNotFoundException>(
            () => Assembler.Find("sasSX", _ => missing));

        Assert.Contains(missing, complaint.Message);
    }

    /// <summary>De lo que no hay en ninguna parte no se inventa una ruta.</summary>
    [AvaloniaFact]
    public void De_lo_que_no_hay_no_se_inventa_una_ruta()
    {
        Assert.Null(Assembler.Find("ensamblador-que-no-existe", _ => null));
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>El nombre del fichero, con extensión sólo en Windows.</summary>
    private static string Executable(string name) =>
        OperatingSystem.IsWindows() ? name + ".exe" : name;

    /// <summary>
    /// La raíz del repo, contada aquí a propósito.
    /// </summary>
    /// <remarks>
    /// Repetida y no sacada de <see cref="Assembler"/>: si la prueba usara la misma cuenta que
    /// lo que prueba, daría igual dónde mirase, siempre coincidirían.
    /// </remarks>
    private static string RepoRoot()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);

        while (folder is not null
               && !File.Exists(Path.Combine(folder.FullName, "MSX_GameTools.csproj")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
            ?? throw new DirectoryNotFoundException("No se encontró la raíz del repo.");
    }
}
