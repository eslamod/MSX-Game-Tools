using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.Services;
using Xunit;

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// Volcado a las tablas del VDP. Los valores esperados salen del manual del V9938:
/// los cuatro cuartos de un 16x16 y el byte EC CC IC 0 cccc de la tabla de colores.
/// </summary>
public class SpriteBankExporterTests
{
    [Fact]
    public void Cada_patron_ocupa_32_bytes()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");
        bank.NewSprite();
        bank.NewSprite();

        Assert.Equal(3 * SpriteBankExporter.PatternBytes, SpriteBankExporter.PatternsToBinary(bank).Length);
    }

    [Fact]
    public void La_mitad_izquierda_va_entera_antes_que_la_derecha()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;   // arriba izquierda
        bank.SpritesList[0].ArraySpriteRows[15].ArrayColumns[7] = true;  // abajo izquierda
        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[8] = true;   // arriba derecha
        bank.SpritesList[0].ArraySpriteRows[15].ArrayColumns[15] = true; // abajo derecha

        byte[] bytes = SpriteBankExporter.PatternsToBinary(bank);

        // Cuartos #0 y #1 (izquierda) en los 16 primeros bytes, #2 y #3 en los 16 siguientes.
        Assert.Equal(0x80, bytes[0]);
        Assert.Equal(0x01, bytes[15]);
        Assert.Equal(0x80, bytes[16]);
        Assert.Equal(0x01, bytes[31]);
    }

    [Fact]
    public void El_byte_de_color_lleva_EC_CC_IC_y_el_color()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");
        SpriteGroup group = bank.NewGroup(0)!;

        group.Members[0].Rows[0].Color = 5;
        group.Members[0].Rows[1].Color = 5;
        group.Members[0].Rows[1].EarlyClock = true;
        group.Members[0].Rows[2].Color = 5;
        group.Members[0].Rows[2].CombineColor = true;
        group.Members[0].Rows[3].Color = 5;
        group.Members[0].Rows[3].InhibitCollision = true;

        byte[] bytes = SpriteBankExporter.GroupsToBinary(bank);

        // El primer byte es el numero de sprites del grupo.
        Assert.Equal(1, bytes[0]);

        Assert.Equal(0x05, bytes[1]); // sin bits
        Assert.Equal(0x85, bytes[2]); // EC
        Assert.Equal(0x45, bytes[3]); // CC
        Assert.Equal(0x25, bytes[4]); // IC
    }

    [Fact]
    public void El_bit_4_del_byte_de_color_es_siempre_cero()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");
        SpriteGroup group = bank.NewGroup(0)!;

        foreach (SpriteAttributeRow row in group.Members[0].Rows)
        {
            row.Color = 15;
            row.EarlyClock = true;
            row.CombineColor = true;
            row.InhibitCollision = true;
        }

        byte[] bytes = SpriteBankExporter.GroupsToBinary(bank);

        Assert.All(bytes.Skip(1).Take(Sprite.Rows), b => Assert.Equal(0, b & 0x10));
        Assert.Equal(0xEF, bytes[1]);
    }

    [Fact]
    public void Un_miembro_msx2_son_16_bytes_de_color_mas_Y_X_y_patron()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");
        bank.NewSprite();
        bank.NewSprite();

        SpriteGroup group = bank.NewGroup(2)!;
        group.Members[0].OffsetY = -4;
        group.Members[0].OffsetX = 3;

        byte[] bytes = SpriteBankExporter.GroupsToBinary(bank);

        Assert.Equal(1 + SpriteBankExporter.Msx2MemberBytes, bytes.Length);
        Assert.Equal(0xFC, bytes[17]); // Y = -4 en complemento a dos
        Assert.Equal(0x03, bytes[18]); // X = 3
        Assert.Equal(8, bytes[19]);    // patron 2 x 4
    }

    [Fact]
    public void Un_miembro_msx1_son_Y_X_patron_y_color()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX, "Bicho");
        bank.NewSprite();

        SpriteGroup group = bank.NewGroup(1)!;
        group.Members[0].OffsetY = 2;
        group.Members[0].OffsetX = -1;

        foreach (SpriteAttributeRow row in group.Members[0].Rows)
        {
            row.Color = 6;
            row.EarlyClock = true;
        }

        byte[] bytes = SpriteBankExporter.GroupsToBinary(bank);

        // En modo 1 no hay tabla de colores: el color va en el cuarto byte.
        Assert.Equal(1 + SpriteBankExporter.Msx1MemberBytes, bytes.Length);
        Assert.Equal(1, bytes[0]);
        Assert.Equal(0x02, bytes[1]);
        Assert.Equal(0xFF, bytes[2]); // -1
        Assert.Equal(4, bytes[3]);    // patron 1 x 4
        Assert.Equal(0x86, bytes[4]); // EC + color 6
    }

    [Fact]
    public void Cada_grupo_declara_cuantos_sprites_tiene()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        SpriteGroup one = bank.NewGroup(0)!;
        one.Add(new SpriteGroupMember(0, bank.SpritesList[0]));
        one.Add(new SpriteGroupMember(0, bank.SpritesList[0]));

        bank.NewGroup(0);

        byte[] bytes = SpriteBankExporter.GroupsToBinary(bank);

        // Sin este byte no se podria recorrer el fichero con grupos de distinto tamano.
        Assert.Equal(3, bytes[0]);
        Assert.Equal(1, bytes[1 + (3 * SpriteBankExporter.Msx2MemberBytes)]);
        Assert.Equal(2 + (4 * SpriteBankExporter.Msx2MemberBytes), bytes.Length);
    }

    [Fact]
    public void El_ensamblador_lleva_los_mismos_bytes_que_el_binario()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");
        bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0] = true;
        bank.NewGroup(0);

        AssertSameBytes(SpriteBankExporter.PatternsToBinary(bank), SpriteBankExporter.PatternsToAssembler(bank));
        AssertSameBytes(SpriteBankExporter.GroupsToBinary(bank), SpriteBankExporter.GroupsToAssembler(bank));
    }

    [Fact]
    public void El_ensamblador_lleva_etiquetas_por_patron_y_por_grupo()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Sprite test 1");
        bank.NewGroup(0);

        Assert.Contains("sprite_test_1_patterns:", SpriteBankExporter.PatternsToAssembler(bank));
        Assert.Contains("sprite_test_1_pattern_0:", SpriteBankExporter.PatternsToAssembler(bank));
        Assert.Contains("sprite_test_1_groups:", SpriteBankExporter.GroupsToAssembler(bank));
        Assert.Contains("sprite_test_1_group_0:", SpriteBankExporter.GroupsToAssembler(bank));
    }

    /// <summary>
    /// Sin estas etiquetas no hay forma de saber dónde acaban los datos: ni el fichero
    /// de patrones ni el de grupos llevan cuántos elementos traen.
    /// </summary>
    [Fact]
    public void El_ensamblador_marca_donde_acaba_cada_bloque()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Sprite test 1");
        bank.NewGroup(0);

        string patterns = SpriteBankExporter.PatternsToAssembler(bank);
        string groups = SpriteBankExporter.GroupsToAssembler(bank);

        Assert.Contains("sprite_test_1_patterns_end:", patterns);
        Assert.Contains("sprite_test_1_groups_end:", groups);

        // Y al final del todo, que si no el tamaño que se calcule con ellas sale corto.
        Assert.EndsWith($"sprite_test_1_patterns_end:{Environment.NewLine}", patterns);
        Assert.EndsWith($"sprite_test_1_groups_end:{Environment.NewLine}", groups);
    }

    [Theory]
    [InlineData("Sprite test 1", "sprite_test_1")]
    [InlineData("Bicho: nivel 3/4", "bicho__nivel_3_4")]
    [InlineData("3 enemigos", "s3_enemigos")]
    [InlineData("   ", "sprites")]
    public void La_etiqueta_sale_de_un_nombre_valido(string bankName, string expected)
        => Assert.Equal(expected, SpriteBankExporter.LabelOf(bankName));

    /// <summary>Extrae los bytes de las lineas db y los compara con el binario.</summary>
    private static void AssertSameBytes(byte[] binary, string assembler)
    {
        List<byte> fromText = [];

        foreach (string line in assembler.Split(Environment.NewLine))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith(SpriteBankExporter.DataDirective, StringComparison.Ordinal))
                continue;

            string values = trimmed[SpriteBankExporter.DataDirective.Length..].Split(';')[0];

            foreach (string value in values.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string hex = value.Trim();

                Assert.StartsWith(SpriteBankExporter.HexPrefix, hex, StringComparison.Ordinal);
                fromText.Add(Convert.ToByte(hex[SpriteBankExporter.HexPrefix.Length..], 16));
            }
        }

        Assert.Equal(binary, fromText);
    }
}
