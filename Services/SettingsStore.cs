using System.Text.Json;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;

namespace MSX_GameTools.Services;

/// <summary>Lo que el programa recuerda de una sesión a la siguiente.</summary>
public sealed record Settings(string Language, EditorPreferences Preferences);

/// <summary>
/// Los ajustes del usuario, guardados fuera del proyecto.
/// </summary>
/// <remarks>
/// <para>
/// Van en la carpeta del usuario y no junto al proyecto: el idioma y el zoom son de quien
/// usa el programa, no del juego que está haciendo. Compartir un proyecto no debería
/// cambiarle el idioma a nadie.
/// </para>
/// <para>
/// Nada de esto puede impedir arrancar. Si el fichero no está, está a medias o la carpeta
/// no se deja escribir, se sigue con los valores de siempre y en silencio: unos ajustes
/// perdidos no son un error que merezca un diálogo antes de haber visto la ventana.
/// </para>
/// </remarks>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";

    /// <summary>La carpeta bajo la del usuario. Con espacios, que es un nombre para leer.</summary>
    public const string FolderName = "MSX Game Tools";

    private const int FormatVersion = 1;

    /// <param name="folder">
    /// Dónde vive el fichero. Sin él, la carpeta de siempre del usuario; los tests pasan
    /// una temporal para no tocar los ajustes de verdad de quien ejecuta las pruebas.
    /// </param>
    public SettingsStore(string? folder = null) =>
        Path = System.IO.Path.Combine(folder ?? DefaultFolder(), FileName);

    public string Path { get; }

    public static string DefaultFolder() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);

    /// <summary>
    /// Deja puesto el idioma guardado.
    /// </summary>
    /// <remarks>
    /// Se llama antes de construir nada. Hay textos que se resuelven una sola vez, al
    /// crearse el objeto que los lleva —los cajones del árbol del proyecto—, así que con el
    /// idioma puesto más tarde salen en el del arranque anterior mientras los menús, que
    /// son enlaces, sí cambian. Media ventana en cada idioma.
    /// </remarks>
    public void ApplyLanguage() => Localizer.Instance.Language = Load().Language;

    /// <summary>Lee los ajustes, o los de partida si no hay ninguno que valga.</summary>
    public Settings Load()
    {
        try
        {
            SettingsFile? file = JsonSerializer.Deserialize<SettingsFile>(
                File.ReadAllText(Path), PaletteSerializer.Options);

            if (file is null || file.Version > FormatVersion)
                return Fresh();

            return new Settings(file.Language ?? string.Empty, file.Zoom ?? new EditorPreferences());
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or JsonException
                                              or NotSupportedException)
        {
            return Fresh();
        }
    }

    /// <summary>Escribe los ajustes. Devuelve si se pudo.</summary>
    public bool Save(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);

            File.WriteAllText(
                Path,
                JsonSerializer.Serialize(
                    new SettingsFile(FormatVersion, settings.Language, settings.Preferences),
                    PaletteSerializer.Options));

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Sin idioma: que lo decida quien sepa cuál habla la máquina.</summary>
    private static Settings Fresh() => new(string.Empty, new EditorPreferences());

    private sealed record SettingsFile(int Version, string? Language, EditorPreferences? Zoom);
}
