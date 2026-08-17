using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Sacar sólo un trozo de la tabla de patrones.
/// </summary>
/// <remarks>
/// Un juego no siempre carga los 64 de golpe: cada pantalla trae sus bichos, o no caben todos en
/// VRAM a la vez. La exportación de siempre saca el banco entero con sus dos tablas y se queda
/// como estaba; esto es lo otro.
/// </remarks>
public class ExportPatternRangeTests
{
    // ------------------------------------------------------------------ el exportador

    [Fact]
    public void Salen_los_bytes_de_los_patrones_que_se_piden()
    {
        SpriteBank bank = Bank();

        byte[] bytes = SpriteBankExporter.PatternsToBinary(bank, 2, 4);

        Assert.Equal(3 * SpriteBankExporter.PatternBytes, bytes.Length);

        // El primer byte del fichero es el del patrón 2, no el del cero.
        Assert.Equal(SpriteBankExporter.PatternsToBinary(bank)[2 * SpriteBankExporter.PatternBytes], bytes[0]);
    }

    /// <summary>
    /// Las etiquetas guardan el número del banco aunque el fichero empiece por el medio.
    /// </summary>
    /// <remarks>
    /// Los grupos apuntan a números concretos, así que renumerar al exportar los dejaría
    /// mintiendo. Lo que cambia es dónde cae cada uno dentro del fichero, y eso lo dice la
    /// cabecera para poder mirarlo una vez y olvidarse.
    /// </remarks>
    [Fact]
    public void Las_etiquetas_siguen_siendo_las_del_banco()
    {
        string text = SpriteBankExporter.PatternsToAssembler(Bank(), 2, 4);

        Assert.Contains("_pattern_2:", text);
        Assert.Contains("_pattern_4:", text);
        Assert.DoesNotContain("_pattern_0:", text);
        Assert.DoesNotContain("_pattern_5:", text);

        // Y dice de dónde a dónde va y cómo se cuenta desde el principio del fichero.
        Assert.Contains("patterns 2 to 4", text);
        Assert.Contains("(N - 2) * 32", text);
    }

    /// <summary>Al revés vale igual: quien escribe un rango a mano pasa por estados a medias.</summary>
    [Fact]
    public void Los_extremos_al_reves_se_ordenan()
    {
        Assert.Equal(
            SpriteBankExporter.PatternsToBinary(Bank(), 2, 4),
            SpriteBankExporter.PatternsToBinary(Bank(), 4, 2));
    }

    /// <summary>Y sin decir nada sale lo de siempre, hasta el último dibujado.</summary>
    [Fact]
    public void Sin_rango_sale_la_tabla_de_siempre()
    {
        SpriteBank bank = Bank();

        Assert.Equal(
            SpriteBankExporter.PatternsToBinary(bank),
            SpriteBankExporter.PatternsToBinary(bank, 0, SpriteBankExporter.TableLength(bank) - 1));
    }

    // ------------------------------------------------------------------ el formulario

    [AvaloniaFact]
    public void Sin_un_banco_delante_no_se_puede()
    {
        var main = new MainWindowViewModel();
        main.OpenTileSet(new TileSet("Bosque"));

        Assert.False(main.ExportPatternRangeCommand.CanExecute(null));
    }

    /// <summary>
    /// Se abre proponiendo lo que ya saca la exportación de siempre.
    /// </summary>
    /// <remarks>
    /// Quien abre esto para acotar un trozo parte de lo que tenía, no de un banco en blanco.
    /// </remarks>
    [AvaloniaFact]
    public void Se_abre_con_lo_que_hay_dibujado()
    {
        var main = new MainWindowViewModel();
        SpritesEditorViewModel editor = main.OpenSpriteBank(Bank());

        main.ExportPatternRangeCommand.Execute(null);

        var panel = (ExportPatternsViewModel)main.RightPanViewModel!;

        Assert.Equal(0, panel.First);
        Assert.Equal(editor.SpritesBank.LastDrawn(), panel.Last);
    }

    /// <summary>Y dice cuántos son y cuánto ocupan, que es lo que se mira para el hueco.</summary>
    [AvaloniaFact]
    public void Dice_cuantos_son_y_cuanto_ocupan()
    {
        var main = new MainWindowViewModel();
        main.OpenSpriteBank(Bank());
        main.ExportPatternRangeCommand.Execute(null);

        var panel = (ExportPatternsViewModel)main.RightPanViewModel!;

        panel.First = 2;
        panel.Last = 4;

        Assert.Contains("3", panel.SizeLabel);
        Assert.Contains("96", panel.SizeLabel);
    }

    /// <summary>Un banco con los cinco primeros patrones dibujados, cada uno distinto.</summary>
    private static SpriteBank Bank()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");

        for (int index = 0; index < 5; index++)
            bank.SpritesList[index].ArraySpriteRows[0].ArrayColumns[index] = true;

        return bank;
    }
}
