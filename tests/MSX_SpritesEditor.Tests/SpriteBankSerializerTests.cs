using System.Text.Json;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.Services;
using Xunit;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// Formato de fichero del banco: patrones como máscaras de bits, grupos con sus
/// atributos, y la paleta embebida.
/// </summary>
public class SpriteBankSerializerTests
{
    [Fact]
    public void Los_patrones_se_guardan_como_mascaras_de_bits()
    {
        SpriteBank bank = new(SpriteBank.SpriteType.MSX2, "Prueba");

        // Columna 0 encendida: bit mas significativo de los 16.
        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;
        // Columna 15: el menos significativo.
        bank.SpritesList[0].ArraySpriteRows[1].ArrayColumns[15] = true;
        // Las cuatro de la izquierda.
        for (int column = 0; column < 4; column++)
            bank.SpritesList[0].ArraySpriteRows[2].ArrayColumns[column] = true;

        using JsonDocument document = JsonDocument.Parse(Serialize(bank));
        JsonElement rows = document.RootElement.GetProperty("patterns")[0].GetProperty("rows");

        Assert.Equal("8000", rows[0].GetString());
        Assert.Equal("0001", rows[1].GetString());
        Assert.Equal("F000", rows[2].GetString());
        Assert.Equal("0000", rows[3].GetString());
    }

    [Fact]
    public void Los_colores_del_patron_van_uno_por_linea_en_hexadecimal()
    {
        SpriteBank bank = new(SpriteBank.SpriteType.MSX2, "Prueba");
        bank.SpritesList[0].ArraySpriteRows[0].Color = 10;
        bank.SpritesList[0].ArraySpriteRows[1].Color = 0;

        using JsonDocument document = JsonDocument.Parse(Serialize(bank));

        Assert.Equal("A0FFFFFFFFFFFFFF", document.RootElement.GetProperty("patterns")[0].GetProperty("colors").GetString());
    }

    [Fact]
    public void La_paleta_va_embebida()
    {
        SpriteBank bank = new(SpriteBank.SpriteType.MSX2, "Prueba");
        ColorPalette palette = new PaletteLibrary().Add("Nocturna");

        using JsonDocument document = JsonDocument.Parse(SpriteBankSerializer.Serialize(bank, palette, 1));
        JsonElement embedded = document.RootElement.GetProperty("palette");

        Assert.Equal("Nocturna", embedded.GetProperty("name").GetString());
        Assert.Equal(ColorPalette.Size, embedded.GetProperty("colors").GetArrayLength());
    }

    [Fact]
    public void Los_bits_de_linea_solo_se_escriben_cuando_estan_activos()
    {
        SpriteBank bank = new(SpriteBank.SpriteType.MSX2, "Prueba");
        SpriteGroup group = bank.NewGroup(0)!;
        group.Members[0].Rows[2].CombineColor = true;

        using JsonDocument document = JsonDocument.Parse(Serialize(bank));
        JsonElement lines = document.RootElement
            .GetProperty("groups")[0]
            .GetProperty("members")[0]
            .GetProperty("lines");

        Assert.True(lines[2].GetProperty("cc").GetBoolean());
        Assert.False(lines[0].TryGetProperty("cc", out _));
        Assert.False(lines[2].TryGetProperty("ic", out _));
    }

    [Fact]
    public void Una_ida_y_vuelta_conserva_el_banco_entero()
    {
        SpriteBank original = new(SpriteBank.SpriteType.MSX2, "Prueba");
        original.NewSprite();
        original.NewSprite();

        original.SpritesList[1].ArraySpriteRows[5].ArrayColumns[3] = true;
        original.SpritesList[1].ArraySpriteRows[5].ArrayColumns[12] = true;
        original.SpritesList[1].ArraySpriteRows[5].Color = 7;

        SpriteGroup group = original.NewGroup(1)!;
        group.Name = "Bicho";
        group.Members[0].OffsetX = -5;
        group.Members[0].OffsetY = 11;
        group.Members[0].Rows[0].Color = 3;
        group.Members[0].Rows[0].CombineColor = true;
        group.Members[0].Rows[0].EarlyClock = true;

        var second = new SpriteGroupMember(2, original.SpritesList[2]);
        group.Add(second);
        second.OffsetX = 1;

        ColorPalette palette = new PaletteLibrary().Add("Nocturna");
        palette[3].SetComponents(7, 0, 5);

        LoadedSpriteBank loaded = SpriteBankSerializer.Deserialize(
            SpriteBankSerializer.Serialize(original, palette, backgroundColorIndex: 4));

        Assert.Equal("Prueba", loaded.Bank.Name);
        Assert.Equal(SpriteBank.SpriteType.MSX2, loaded.Bank.Type);
        Assert.Equal(4, loaded.BackgroundColorIndex);
        Assert.Equal(3, loaded.Bank.SpritesList.Count);

        Assert.True(loaded.Bank.SpritesList[1].ArraySpriteRows[5].ArrayColumns[3]);
        Assert.True(loaded.Bank.SpritesList[1].ArraySpriteRows[5].ArrayColumns[12]);
        Assert.False(loaded.Bank.SpritesList[1].ArraySpriteRows[5].ArrayColumns[4]);
        Assert.Equal(7, loaded.Bank.SpritesList[1].ArraySpriteRows[5].Color);

        SpriteGroup loadedGroup = Assert.Single(loaded.Bank.Groups);
        Assert.Equal("Bicho", loadedGroup.Name);
        Assert.Equal(2, loadedGroup.Members.Count);
        Assert.Equal(1, loadedGroup.Members[0].PatternIndex);
        Assert.Equal(-5, loadedGroup.Members[0].OffsetX);
        Assert.Equal(11, loadedGroup.Members[0].OffsetY);
        Assert.Equal(3, loadedGroup.Members[0].Rows[0].Color);
        Assert.True(loadedGroup.Members[0].Rows[0].CombineColor);
        Assert.True(loadedGroup.Members[0].Rows[0].EarlyClock);
        Assert.False(loadedGroup.Members[0].Rows[0].InhibitCollision);
        Assert.Equal(2, loadedGroup.Members[1].PatternIndex);
        Assert.Equal(1, loadedGroup.Members[1].OffsetX);

        Assert.Equal("Nocturna", loaded.Palette.Name);
        Assert.Equal("705", loaded.Palette[3].HexRgb);
    }

    [Fact]
    public void Un_banco_msx1_conserva_su_tipo()
    {
        SpriteBank bank = new(SpriteBank.SpriteType.MSX, "Uno");

        LoadedSpriteBank loaded = SpriteBankSerializer.Deserialize(Serialize(bank));

        Assert.Equal(SpriteBank.SpriteType.MSX, loaded.Bank.Type);
    }

    [Theory]
    [InlineData("no soy json", "JSON válido")]
    [InlineData("{}", "la paleta")]
    public void Un_fichero_mal_formado_se_rechaza_con_un_motivo(string json, string expectedFragment)
    {
        var exception = Assert.Throws<FileFormatException>(() => SpriteBankSerializer.Deserialize(json));

        Assert.Contains(expectedFragment, exception.Message);
    }

    [Fact]
    public void Un_tipo_de_banco_desconocido_se_rechaza()
    {
        string json = Serialize(new SpriteBank(SpriteBank.SpriteType.MSX2, "Prueba"))
            .Replace("\"MSX2\"", "\"MSX9\"", StringComparison.Ordinal);

        var exception = Assert.Throws<FileFormatException>(() => SpriteBankSerializer.Deserialize(json));

        Assert.Contains("MSX9", exception.Message);
    }

    [Fact]
    public void Una_linea_de_patron_con_digitos_de_mas_se_rechaza()
    {
        string json = Serialize(new SpriteBank(SpriteBank.SpriteType.MSX2, "Prueba"))
            .Replace("\"0000\"", "\"00000\"", StringComparison.Ordinal);

        var exception = Assert.Throws<FileFormatException>(() => SpriteBankSerializer.Deserialize(json));

        Assert.Contains("4 dígitos", exception.Message);
    }

    [Fact]
    public void Un_grupo_que_apunta_a_un_patron_inexistente_se_rechaza()
    {
        SpriteBank bank = new(SpriteBank.SpriteType.MSX2, "Prueba");
        bank.NewGroup(0);

        string json = Serialize(bank).Replace("\"pattern\": 0", "\"pattern\": 40", StringComparison.Ordinal);

        var exception = Assert.Throws<FileFormatException>(() => SpriteBankSerializer.Deserialize(json));

        Assert.Contains("patrón 40", exception.Message);
    }

    [Fact]
    public void Un_fichero_de_una_version_mas_nueva_se_rechaza()
    {
        string json = Serialize(new SpriteBank(SpriteBank.SpriteType.MSX2, "Prueba"))
            .Replace("\"version\": 1", $"\"version\": {SpriteBankSerializer.FormatVersion + 1}", StringComparison.Ordinal);

        var exception = Assert.Throws<FileFormatException>(() => SpriteBankSerializer.Deserialize(json));

        Assert.Contains("versión", exception.Message);
    }

    private static string Serialize(SpriteBank bank) =>
        SpriteBankSerializer.Serialize(bank, ColorPalette.CreateMsxStandard(), 1);
}
