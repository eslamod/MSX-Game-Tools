using System.Text.Json;
using System.Text.Json.Serialization;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Qué clase de documento es un elemento del proyecto.
/// </summary>
/// <remarks>
/// El orden importa y es el de carga: un mapa necesita su juego de tiles abierto para
/// poder dibujarse, así que los juegos van primero.
/// </remarks>
public enum ProjectItemKind
{
    TileSet,
    SpriteBank,
    Map,
}

/// <summary>Un documento del proyecto, con la ruta de su fichero relativa al proyecto.</summary>
public sealed record ProjectItem(ProjectItemKind Kind, string Path);

/// <summary>Lo que agrupa un proyecto.</summary>
public sealed record Project(
    string Name,
    IReadOnlyList<ProjectItem> Items,
    IReadOnlyList<ColorPalette> Palettes,
    IReadOnlyList<BackgroundImageRef> Backgrounds);

/// <summary>
/// Formato del fichero de proyecto: un índice de lo que hay y dónde está.
/// </summary>
/// <remarks>
/// <para>
/// Índice y no un fichero con todo dentro. Cada juego de tiles, banco y mapa sigue en su
/// propio fichero, que es lo que permite compartir un juego entre dos proyectos, ver un
/// diff legible de lo que cambió, y que Guardar de un documento suelto siga significando
/// algo. Un mapa de 1024x1024 embebido convertiría el proyecto en varios megas que hay
/// que reescribir enteros para mover un tile.
/// </para>
/// <para>
/// Las rutas van relativas al proyecto y con barras normales, para que la carpeta se
/// pueda mover de sitio y llevarse a otra máquina.
/// </para>
/// <para>
/// Las paletas sí van dentro, y no es una contradicción: cada documento ya guarda la suya
/// en su fichero, y esto es el catálogo del que se eligen. Sin él se perderían las que se
/// han creado y todavía no usa nadie. Ocupan 32 bytes.
/// </para>
/// </remarks>
public static class ProjectSerializer
{
    public const int FormatVersion = 1;

    /// <summary>La extensión propia, para distinguirlo de los documentos.</summary>
    public const string Extension = ".msxproj";

    private static readonly JsonSerializerOptions Options = new(PaletteSerializer.Options)
    {
        // Los tipos por su nombre y no por su número: un índice se lee a mano y se
        // corrige a mano, y «map» dice lo que es y «2» no.
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(Project project) =>
        JsonSerializer.Serialize(ToFile(project), Options);

    /// <exception cref="FileFormatException">El contenido no es un proyecto válido.</exception>
    public static Project Deserialize(string json)
    {
        ProjectFile? file;

        try
        {
            file = JsonSerializer.Deserialize<ProjectFile>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new FileFormatException("El fichero no contiene JSON válido.", exception);
        }

        if (file is null)
            throw new FileFormatException("El fichero está vacío.");

        if (file.Version > FormatVersion)
        {
            throw new FileFormatException(
                $"El proyecto usa la versión {file.Version} del formato y esta versión del editor sólo entiende hasta la {FormatVersion}.");
        }

        return new Project(
            string.IsNullOrWhiteSpace(file.Name) ? "Unnamed project" : file.Name,
            [.. (file.Items ?? []).Select(ReadItem)],
            [.. (file.Palettes ?? []).Select(PaletteSerializer.FromFile)],
            [.. (file.Backgrounds ?? [])
                .Where(image => !string.IsNullOrWhiteSpace(image.Path))
                .Select(image => new BackgroundImageRef(image.Path!, image.CellSize))]);
    }

    private static ProjectFile ToFile(Project project) => new(
        FormatVersion,
        project.Name,
        [.. project.Items.Select(item => new ItemFile(item.Kind, Portable(item.Path)))],
        [.. project.Palettes.Select(PaletteSerializer.ToFile)],
        [.. project.Backgrounds.Select(image => new ReferenceFile(image.Path, image.CellSize))]);

    private static ProjectItem ReadItem(ItemFile file)
    {
        if (string.IsNullOrWhiteSpace(file.Path))
            throw new FileFormatException($"El proyecto trae un elemento de tipo {file.Kind} sin fichero.");

        return new ProjectItem(file.Kind, Local(file.Path));
    }

    /// <summary>Con barras normales, que valen en todas partes y sobreviven al copiar.</summary>
    private static string Portable(string path) => path.Replace('\\', '/');

    /// <summary>Y de vuelta a las de esta máquina.</summary>
    private static string Local(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    private sealed record ProjectFile(
        int Version,
        string? Name,
        IReadOnlyList<ItemFile>? Items,
        IReadOnlyList<PaletteSerializer.PaletteFile>? Palettes,
        IReadOnlyList<ReferenceFile>? Backgrounds);

    private sealed record ItemFile(ProjectItemKind Kind, string? Path);

    private sealed record ReferenceFile(string? Path, int CellSize);
}
