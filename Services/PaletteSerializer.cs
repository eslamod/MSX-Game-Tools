using System.Text.Json;
using System.Text.Json.Serialization;
using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Formato de fichero de las paletas: JSON, con cada color en sus 9 bits nativos como
/// tres dígitos hexadecimales (uno por componente, 0-7).
/// </summary>
/// <remarks>
/// Ejemplo de una entrada: <c>{ "rgb": "161", "name": "Medium green" }</c>. El índice
/// del color es su posición en la lista, que siempre son 16.
/// </remarks>
public static class PaletteSerializer
{
    /// <summary>Versión del formato. Un fichero más nuevo se rechaza en vez de leerse a medias.</summary>
    /// <remarks>
    /// Sigue en 1 después de añadir <c>inherited</c>: es un campo opcional que quien no lo
    /// entienda puede ignorar sin leer nada mal —se queda con el comportamiento de antes,
    /// que era tratar todos los nombres como escritos—. Subirla haría que una versión
    /// anterior del editor rechazara ficheros que sabe abrir perfectamente.
    /// </remarks>
    public const int FormatVersion = 1;

    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(ColorPalette palette) =>
        JsonSerializer.Serialize(ToFile(palette), Options);

    /// <exception cref="FileFormatException">El contenido no es una paleta válida.</exception>
    public static ColorPalette Deserialize(string json)
    {
        PaletteFile? file;

        try
        {
            file = JsonSerializer.Deserialize<PaletteFile>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new FileFormatException("El fichero no contiene JSON válido.", exception);
        }

        if (file is null)
            throw new FileFormatException("El fichero está vacío.");

        return FromFile(file);
    }

    /// <summary>La paleta como objeto del fichero, para poder embeberla en otros.</summary>
    internal static PaletteFile ToFile(ColorPalette palette) => new(
        FormatVersion,
        palette.Name,
        [.. palette.Colors.Select(color => new PaletteColorFile(
            color.HexRgb,
            string.IsNullOrWhiteSpace(color.Name) ? null : color.Name,
            color.HasInheritedName && !string.IsNullOrWhiteSpace(color.Name) ? true : null))]);

    internal static ColorPalette FromFile(PaletteFile file)
    {
        if (file.Version > FormatVersion)
        {
            throw new FileFormatException(
                $"La paleta usa la versión {file.Version} del formato y esta versión del editor sólo entiende hasta la {FormatVersion}.");
        }

        int count = file.Colors?.Count ?? 0;
        if (count != ColorPalette.Size)
            throw new FileFormatException($"Una paleta son {ColorPalette.Size} colores, y el fichero trae {count}.");

        var colors = new List<PaletteColor>(ColorPalette.Size);

        for (int index = 0; index < ColorPalette.Size; index++)
        {
            PaletteColorFile entry = file.Colors![index];
            (int red, int green, int blue) = ParseRgb(entry.Rgb, index);

            // La marca viaja en el fichero para que la paleta se comporte igual antes y
            // después de guardar: un nombre heredado de la MSX se descarta al cambiar el
            // color, y uno escrito por el usuario se respeta. Sin el campo —los ficheros
            // de antes— se da por escrito, que es lo que hacían al abrirse.
            colors.Add(new PaletteColor(
                index,
                entry.Name ?? string.Empty,
                red,
                green,
                blue,
                nameIsInherited: entry.Inherited ?? false));
        }

        string name = string.IsNullOrWhiteSpace(file.Name) ? "Paleta sin nombre" : file.Name;

        return new ColorPalette(name, isReadOnly: false, colors);
    }

    private static (int Red, int Green, int Blue) ParseRgb(string? rgb, int index)
    {
        if (rgb is null || rgb.Length != 3)
        {
            throw new FileFormatException(
                $"El color {index:X1} debe traer tres dígitos hexadecimales, uno por componente; trae «{rgb}».");
        }

        return (ParseComponent(rgb[0], index), ParseComponent(rgb[1], index), ParseComponent(rgb[2], index));
    }

    private static int ParseComponent(char digit, int index)
    {
        int value = HexDigit(digit)
                    ?? throw new FileFormatException($"El color {index:X1} tiene un dígito que no es hexadecimal: «{digit}».");

        if (value > PaletteColor.MaxComponent)
        {
            throw new FileFormatException(
                $"El color {index:X1} tiene una componente fuera del rango del MSX (0-7): «{digit}».");
        }

        return value;
    }

    /// <summary>Valor de un dígito hexadecimal, o <c>null</c> si no lo es.</summary>
    internal static int? HexDigit(char digit) => digit switch
    {
        >= '0' and <= '9' => digit - '0',
        >= 'a' and <= 'f' => digit - 'a' + 10,
        >= 'A' and <= 'F' => digit - 'A' + 10,
        _ => null,
    };

    internal sealed record PaletteFile(int Version, string? Name, IReadOnlyList<PaletteColorFile>? Colors);

    /// <param name="Inherited">
    /// El nombre viene heredado de la paleta de la que se copió y no lo eligió el usuario,
    /// así que se descarta en cuanto se cambia el color. Se omite cuando no lo es.
    /// </param>
    internal sealed record PaletteColorFile(string? Rgb, string? Name, bool? Inherited = null);
}
