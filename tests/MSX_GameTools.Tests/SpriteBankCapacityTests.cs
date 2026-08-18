using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Bancos de más de 64 patrones.
/// </summary>
/// <remarks>
/// Los 64 son de la tabla de patrones de la VRAM, no del banco. Un juego que tenga los patrones
/// en ROM y vaya redefiniendo los que necesita cada animación puede tener muchos más: 256 son
/// 8 KB, que en una MegaROM no es nada. El editor no lo impide, lo avisa.
/// </remarks>
public class SpriteBankCapacityTests
{
    [Fact]
    public void Un_banco_tiene_los_patrones_que_se_le_pidan()
    {
        Assert.Equal(64, new SpriteBank(SpriteBank.SpriteType.MSX, "A").SpritesList.Count);
        Assert.Equal(256, new SpriteBank(SpriteBank.SpriteType.MSX, "A", 256).SpritesList.Count);
    }

    /// <summary>Un tamaño que no está en la lista se queda en el de siempre.</summary>
    /// <remarks>
    /// No es capricho: los índices de patrón viajan en un byte y los tamaños son los que el
    /// editor sabe leer de un fichero. Uno cualquiera pasaría por aquí y reventaría al abrirlo.
    /// </remarks>
    [Fact]
    public void Un_tamano_que_no_existe_se_queda_en_sesenta_y_cuatro()
    {
        Assert.Equal(64, new SpriteBank(SpriteBank.SpriteType.MSX, "A", 100).Capacity);
    }

    /// <summary>El tamaño viaja en el fichero y vuelve igual.</summary>
    [AvaloniaFact]
    public void El_tamano_va_y_vuelve_en_el_fichero()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos", 128);
        bank.SpritesList[100].ArraySpriteRows[0].ArrayColumns[0] = true;

        ColorPalette palette = ColorPalette.CreateMsxStandard();

        LoadedSpriteBank back = SpriteBankSerializer.Deserialize(
            SpriteBankSerializer.Serialize(bank, palette, 1, []));

        Assert.Equal(128, back.Bank.Capacity);
        Assert.Equal(128, back.Bank.SpritesList.Count);

        // Y el patrón 100, que en un banco de 64 no existiría, sigue donde estaba.
        Assert.True(back.Bank.SpritesList[100].ArraySpriteRows[0].ArrayColumns[0]);
    }

    /// <summary>
    /// Un fichero sin tamaño es de 64, que es lo único que había antes.
    /// </summary>
    /// <remarks>
    /// Los bancos que ya existen se abren sin tocar nada. Si el tamaño ausente se leyera como
    /// cero, o como el mayor, cambiarían de tamaño al abrirlos y nadie lo habría pedido.
    /// </remarks>
    [AvaloniaFact]
    public void Un_fichero_sin_tamano_es_de_sesenta_y_cuatro()
    {
        string json = SpriteBankSerializer.Serialize(
            new SpriteBank(SpriteBank.SpriteType.MSX, "Viejo"), ColorPalette.CreateMsxStandard(), 1, []);

        // Como lo escribia la version 2: sin capacidad ninguna.
        json = json.Replace("\"capacity\": 64,", string.Empty).Replace("\"capacity\":64,", string.Empty);

        Assert.DoesNotContain("capacity", json);
        Assert.Equal(64, SpriteBankSerializer.Deserialize(json).Bank.Capacity);
    }

    /// <summary>
    /// El formulario avisa en cuanto se pasa de lo que cabe en la VRAM.
    /// </summary>
    /// <remarks>
    /// Al lado del selector y no en un diálogo: es una decisión de cómo está hecho el juego, no
    /// un error que haya que confirmar.
    /// </remarks>
    [AvaloniaFact]
    public void El_formulario_avisa_al_pasar_de_lo_que_cabe_en_vram()
    {
        var main = new MainWindowViewModel();
        main.AddSpriteBankCommand.Execute(null);

        var form = (EditSpriteBankViewModel)main.RightPanViewModel!;

        Assert.Equal(SpriteBank.MaxSprites, form.Capacity);
        Assert.False(form.IsOverVram);

        form.Capacity = 256;

        Assert.True(form.IsOverVram);
    }

    /// <summary>Y el banco que sale lleva el tamaño que se eligió.</summary>
    [AvaloniaFact]
    public void El_banco_creado_lleva_el_tamano_elegido()
    {
        var main = new MainWindowViewModel();
        main.AddSpriteBankCommand.Execute(null);

        var form = (EditSpriteBankViewModel)main.RightPanViewModel!;
        form.Name = "Bichos";
        form.Capacity = 128;
        form.AcceptSpriteBankCommand.Execute(null);

        SpritesEditorViewModel editor = main.Tabs.OfType<SpritesEditorViewModel>().Last();

        Assert.Equal(128, editor.SpritesBank.Capacity);
    }
}
