namespace MSX_GameTools.Tests;

/// <summary>
/// Dónde está el ensamblador que se va a ejecutar.
/// </summary>
/// <remarks>
/// <para>
/// Por convención y no por ruta absoluta: manda la variable de entorno si la hay, si no lo que
/// sale de compilar el submódulo, y si tampoco, el <c>PATH</c>. Con la ruta a pelo la prueba
/// sólo corría en una máquina, y encima allí contra un binario de otro checkout, de la época de
/// Mono, que ya no era el del submódulo.
/// </para>
/// </remarks>
internal static class Assembler
{
    /// <summary>
    /// El ejecutable de ese ensamblador, o <c>null</c> si aquí no está.
    /// </summary>
    /// <param name="environment">
    /// De dónde se leen las variables de entorno. Se puede cambiar para probarlo sin tocar las
    /// del proceso, que son de todos y las pruebas corren en paralelo.
    /// </param>
    /// <exception cref="FileNotFoundException">
    /// La variable nombra un fichero que no está. Se cae en vez de seguir buscando: si te
    /// equivocas al escribirla, ensamblar con otro binario dejaría la prueba en verde diciendo
    /// que probó lo que no probó.
    /// </exception>
    public static string? Find(string name, Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;

        string? said = environment(VariableFor(name));

        if (!string.IsNullOrWhiteSpace(said))
        {
            return File.Exists(said)
                ? said
                : throw new FileNotFoundException(
                    $"{VariableFor(name)} apunta a «{said}», que no existe.", said);
        }

        return Places(name).FirstOrDefault(File.Exists) ?? OnPath(name);
    }

    /// <summary>
    /// Ensambla ese fichero y deja el binario donde se diga. Devuelve lo que haya dicho.
    /// </summary>
    /// <remarks>
    /// Aquí y no en cada prueba porque la forma de nombrar la salida cambia de uno a otro, y
    /// con dos copias el día que una cambiara la otra seguiría ensamblando lo de antes. Se
    /// ejecuta en la carpeta del fuente, que es donde están los ficheros que se trae.
    /// </remarks>
    public static string Run(string tool, string name, string source, string binary)
    {
        // asMSX no lleva destino en la línea de órdenes: lo saca del .filename de dentro,
        // así que el binario que se le pida tiene que ser el que nombre el fuente.
        string arguments = name switch
        {
            "sjasmplus" => $"--nologo --raw=\"{binary}\" \"{source}\"",
            "asMSX" => $"\"{source}\"",
            _ => $"\"{source}\" \"{binary}\"",
        };

        var run = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(tool, arguments)
            {
                WorkingDirectory = Path.GetDirectoryName(source)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;

        string said = run.StandardOutput.ReadToEnd() + run.StandardError.ReadToEnd();

        run.WaitForExit();

        return said;
    }

    /// <summary>La variable que dice dónde está ese: <c>SASSX</c>, <c>PASMO</c>…</summary>
    public static string VariableFor(string name) => name.ToUpperInvariant();

    /// <summary>Qué contarle a quien no lo tenga, cuando la prueba se salta.</summary>
    public static string HowToGetIt(string name) => name == "sasSX"
        ? $"compila el submódulo con «dotnet build tools/sass-MSX -c Release», ponlo en el PATH "
          + $"o di dónde está en {VariableFor(name)}"
        : $"ponlo en el PATH o di dónde está en {VariableFor(name)}";

    // ------------------------------------------------------------------ dónde se mira

    /// <summary>Los sitios fijos, en orden.</summary>
    private static IEnumerable<string> Places(string name)
    {
        // El csproj del submódulo quita el nombre del framework de la ruta de salida, así que
        // el binario cae directo en bin/<configuración>.
        if (name == "sasSX")
        {
            foreach (string configuration in new[] { "Release", "Debug" })
            {
                yield return Path.Combine(
                    Root(), "tools", "sass-MSX", "sass", "bin", configuration, FileFor(name));
            }
        }

        // Los de fuera, tal como están en la máquina donde se midió qué directiva acepta cada
        // uno. En cualquier otra salen del PATH o de su variable.
        foreach (string local in Elsewhere(name))
            yield return local;
    }

    /// <summary>Los que no se compilan aquí, donde estaban al medirlos.</summary>
    private static string[] Elsewhere(string name) => name switch
    {
        "sjasmplus" => [@"D:\utils\MSx\sjasmplus-1.24.0.win\sjasmplus.exe"],
        "pasmo" => [@"D:\utils\MSx\pasmo-0.5.3\pasmo.exe"],

        // El .win es el ejecutable de Windows sin renombrar, que es como se distribuye;
        // Windows lo ejecuta igual, que mira la cabecera del PE y no la extensión.
        "asMSX" => [@"D:\utils\MSX\asMSX\bin\asmsx.win", @"D:\utils\MSX\asMSX\bin\asmsx.exe"],
        _ => [],
    };

    /// <summary>El primero del <c>PATH</c> que se llame así.</summary>
    private static string? OnPath(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Combine(folder.Trim('"'), FileFor(name)))
            .FirstOrDefault(File.Exists);

    /// <summary>El apphost de .NET lleva extensión en Windows y no la lleva fuera.</summary>
    private static string FileFor(string name) =>
        OperatingSystem.IsWindows() ? name + ".exe" : name;

    /// <summary>
    /// La raíz del repo, subiendo desde donde corren las pruebas hasta encontrar el proyecto.
    /// </summary>
    /// <remarks>
    /// Buscándola y no con una ruta relativa contada a mano: entre <c>bin</c>, la
    /// configuración y el framework, el número de escalones cambia según cómo se compile.
    /// </remarks>
    private static string Root()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory);
             folder is not null;
             folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "MSX_GameTools.csproj")))
                return folder.FullName;
        }

        return AppContext.BaseDirectory;
    }
}
