using System.Text.Json;
using System.Text.Json.Serialization;
using MSX_SpritesEditor.Entities;

namespace MSX_SpritesEditor.Services;

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
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(ColorPalette palette)
    {
        var file = new PaletteFile(
            FormatVersion,
            palette.Name,
            [.. palette.Colors.Select(color => new PaletteColorFile(
                color.HexRgb,
                string.IsNullOrWhiteSpace(color.Name) ? null : color.Name))]);

        return JsonSerializer.Serialize(file, Options);
    }

    /// <exception cref="PaletteFormatException">El contenido no es una paleta válida.</exception>
    public static ColorPalette Deserialize(string json)
    {
        PaletteFile? file;

        try
        {
            file = JsonSerializer.Deserialize<PaletteFile>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new PaletteFormatException("El fichero no contiene JSON válido.", exception);
        }

        if (file is null)
            throw new PaletteFormatException("El fichero está vacío.");

        if (file.Version > FormatVersion)
        {
            throw new PaletteFormatException(
                $"El fichero usa la versión {file.Version} del formato y esta versión del editor sólo entiende hasta la {FormatVersion}.");
        }

        int count = file.Colors?.Count ?? 0;
        if (count != ColorPalette.Size)
            throw new PaletteFormatException($"Una paleta son {ColorPalette.Size} colores, y el fichero trae {count}.");

        var colors = new List<PaletteColor>(ColorPalette.Size);

        for (int index = 0; index < ColorPalette.Size; index++)
        {
            PaletteColorFile entry = file.Colors![index];
            (int red, int green, int blue) = ParseRgb(entry.Rgb, index);

            // Un nombre guardado en un fichero es deliberado, no heredado de otra
            // paleta: no se descarta al cambiar el color.
            colors.Add(new PaletteColor(
                index,
                entry.Name ?? string.Empty,
                red,
                green,
                blue,
                nameIsInherited: false));
        }

        string name = string.IsNullOrWhiteSpace(file.Name) ? "Paleta sin nombre" : file.Name;

        return new ColorPalette(name, isReadOnly: false, colors);
    }

    private static (int Red, int Green, int Blue) ParseRgb(string? rgb, int index)
    {
        if (rgb is null || rgb.Length != 3)
        {
            throw new PaletteFormatException(
                $"El color {index:X1} debe traer tres dígitos hexadecimales, uno por componente; trae «{rgb}».");
        }

        return (ParseComponent(rgb[0], index), ParseComponent(rgb[1], index), ParseComponent(rgb[2], index));
    }

    private static int ParseComponent(char digit, int index)
    {
        int value = digit switch
        {
            >= '0' and <= '9' => digit - '0',
            >= 'a' and <= 'f' => digit - 'a' + 10,
            >= 'A' and <= 'F' => digit - 'A' + 10,
            _ => throw new PaletteFormatException($"El color {index:X1} tiene un dígito que no es hexadecimal: «{digit}»."),
        };

        if (value > PaletteColor.MaxComponent)
        {
            throw new PaletteFormatException(
                $"El color {index:X1} tiene una componente fuera del rango del MSX (0-7): «{digit}».");
        }

        return value;
    }

    private sealed record PaletteFile(int Version, string? Name, IReadOnlyList<PaletteColorFile>? Colors);

    private sealed record PaletteColorFile(string? Rgb, string? Name);
}
