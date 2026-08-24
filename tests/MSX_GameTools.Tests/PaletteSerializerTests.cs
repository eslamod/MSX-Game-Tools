using System.Text.Json;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using Xunit;
using Avalonia.Headless.XUnit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El formato de fichero: JSON con cada color en sus 9 bits nativos, tres dígitos
/// hexadecimales (uno por componente, 0-7).
/// </summary>
public class PaletteSerializerTests
{
    [AvaloniaFact]
    public void Guarda_las_componentes_como_tres_digitos_hexadecimales()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        using JsonDocument document = JsonDocument.Parse(PaletteSerializer.Serialize(palette));
        JsonElement colors = document.RootElement.GetProperty("colors");

        Assert.Equal(ColorPalette.Size, colors.GetArrayLength());
        Assert.Equal("000", colors[0].GetProperty("rgb").GetString());
        Assert.Equal("161", colors[2].GetProperty("rgb").GetString()); // Medium green
        Assert.Equal("777", colors[15].GetProperty("rgb").GetString()); // White
        Assert.Equal("Medium green", colors[2].GetProperty("name").GetString());
    }

    [AvaloniaFact]
    public void Guarda_el_nombre_y_la_version_del_formato()
    {
        ColorPalette palette = new PaletteLibrary().Add("Nocturna");

        using JsonDocument document = JsonDocument.Parse(PaletteSerializer.Serialize(palette));

        Assert.Equal("Nocturna", document.RootElement.GetProperty("name").GetString());
        Assert.Equal(PaletteSerializer.FormatVersion, document.RootElement.GetProperty("version").GetInt32());
    }

    [AvaloniaFact]
    public void Una_ida_y_vuelta_conserva_nombre_y_colores()
    {
        ColorPalette original = new PaletteLibrary().Add("Nocturna");
        original[3].SetComponents(7, 0, 5);
        original[3].Name = "piel";

        ColorPalette copy = PaletteSerializer.Deserialize(PaletteSerializer.Serialize(original));

        Assert.Equal("Nocturna", copy.Name);
        Assert.False(copy.IsReadOnly);
        Assert.Equal(ColorPalette.Size, copy.Count);

        for (int index = 0; index < ColorPalette.Size; index++)
        {
            Assert.Equal(original[index].Red, copy[index].Red);
            Assert.Equal(original[index].Green, copy[index].Green);
            Assert.Equal(original[index].Blue, copy[index].Blue);
            Assert.Equal(original[index].Name, copy[index].Name);
        }
    }

    /// <summary>
    /// Un nombre que llega a un fichero lo ha escrito el usuario, y se respeta siempre.
    /// </summary>
    /// <remarks>
    /// Los nombres de la paleta del MSX no salen de ella —una copia arranca sin nombres—,
    /// así que todo lo que aparezca en un fichero es deliberado. No hay nada que
    /// distinguir ni que guardar aparte.
    /// </remarks>
    [AvaloniaFact]
    public void Un_nombre_escrito_aguanta_la_ida_y_vuelta_y_los_cambios_de_color()
    {
        ColorPalette original = new PaletteLibrary().Add("Nocturna");
        original[3].Name = "Verde del bosque";

        ColorPalette copy = PaletteSerializer.Deserialize(PaletteSerializer.Serialize(original));

        Assert.Equal("Verde del bosque", copy[3].Name);

        copy[3].Red = 0;

        Assert.Equal("Verde del bosque", copy[3].Name);
    }

    [AvaloniaFact]
    public void Un_color_sin_nombre_no_ocupa_sitio_en_el_fichero()
    {
        ColorPalette palette = new PaletteLibrary().Add("Nocturna");
        palette[3].Red = 0; // descarta el nombre heredado

        string json = PaletteSerializer.Serialize(palette);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement color = document.RootElement.GetProperty("colors")[3];

        Assert.False(color.TryGetProperty("name", out _));
        Assert.Equal(string.Empty, PaletteSerializer.Deserialize(json)[3].Name);
    }

    [AvaloniaTheory]
    [InlineData("no soy json", "JSON válido")]
    [InlineData("{}", "16 colores")]
    [InlineData("""{"version":1,"name":"x","colors":[{"rgb":"000"}]}""", "16 colores")]
    public void Un_fichero_mal_formado_se_rechaza_con_un_motivo(string json, string expectedFragment)
    {
        var exception = Assert.Throws<FileFormatException>(() => PaletteSerializer.Deserialize(json));

        Assert.Contains(expectedFragment, exception.Message);
    }

    [AvaloniaFact]
    public void Una_componente_fuera_del_rango_del_msx_se_rechaza()
    {
        // La F es hexadecimal válida pero el MSX sólo llega al 7.
        string json = BuildJson(colorAt3: """{"rgb":"F00"}""");

        var exception = Assert.Throws<FileFormatException>(() => PaletteSerializer.Deserialize(json));

        Assert.Contains("0-7", exception.Message);
        Assert.Contains("color 3", exception.Message);
    }

    [AvaloniaFact]
    public void Un_digito_que_no_es_hexadecimal_se_rechaza()
    {
        string json = BuildJson(colorAt3: """{"rgb":"1Z1"}""");

        var exception = Assert.Throws<FileFormatException>(() => PaletteSerializer.Deserialize(json));

        Assert.Contains("hexadecimal", exception.Message);
    }

    [AvaloniaFact]
    public void Un_rgb_de_longitud_equivocada_se_rechaza()
    {
        string json = BuildJson(colorAt3: """{"rgb":"1234"}""");

        Assert.Throws<FileFormatException>(() => PaletteSerializer.Deserialize(json));
    }

    [AvaloniaFact]
    public void Un_fichero_de_una_version_mas_nueva_se_rechaza()
    {
        string json = BuildJson(colorAt3: """{"rgb":"111"}""", version: PaletteSerializer.FormatVersion + 1);

        var exception = Assert.Throws<FileFormatException>(() => PaletteSerializer.Deserialize(json));

        Assert.Contains("versión", exception.Message);
    }

    [AvaloniaFact]
    public void Una_paleta_sin_nombre_recibe_uno_por_defecto()
    {
        string json = BuildJson(colorAt3: """{"rgb":"111"}""", name: "");

        Assert.Equal("Unnamed palette", PaletteSerializer.Deserialize(json).Name);
    }

    /// <summary>16 colores en negro, salvo el 3 que se sustituye por lo que se pase.</summary>
    private static string BuildJson(string colorAt3, int version = 1, string name = "Prueba")
    {
        string[] colors = [.. Enumerable.Repeat("""{"rgb":"000"}""", ColorPalette.Size)];
        colors[3] = colorAt3;

        return $$"""{"version":{{version}},"name":"{{name}}","colors":[{{string.Join(",", colors)}}]}""";
    }
}
