using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// La directiva de datos con la que se exporta, que se elige una vez y se guarda.
/// </summary>
/// <remarks>
/// No hay una grafía que valga para los cuatro ensambladores probados: sasSX exige el punto de
/// <c>.db</c> y pasmo lo rechaza. Por eso se elige en las preferencias en vez de preguntarlo en
/// cada exportación.
/// </remarks>
public class AsmStyleTests
{
    /// <summary>La directiva elegida es la que sale en el fichero.</summary>
    [AvaloniaTheory]
    [InlineData(AsmStyle.Dotted)]
    [InlineData(AsmStyle.Plain)]
    [InlineData("defb")]
    public void La_directiva_elegida_es_la_que_sale(string data)
    {
        var tileSet = new TileSet("Bosque");

        string asm = TileSetExporter.PatternsToAssembler(tileSet, new AsmStyle(data));

        Assert.Contains($"    {data}  ", asm);
    }

    /// <summary>
    /// En blanco vuelve a la de siempre.
    /// </summary>
    /// <remarks>
    /// Se puede escribir a mano, así que se puede dejar vacía. Un ajuste a medias no debería
    /// producir líneas sin directiva, que no ensamblan en ninguna parte.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void En_blanco_vuelve_a_la_de_siempre(string? data)
    {
        Assert.Equal(AsmStyle.Dotted, AsmStyle.Of(data).Data);

        Assert.Contains(
            $"    {AsmStyle.Dotted}  ",
            TileSetExporter.PatternsToAssembler(new TileSet("Bosque"), AsmStyle.Of(data)));
    }

    /// <summary>Y los espacios de alrededor no acaban en el fichero.</summary>
    [AvaloniaFact]
    public void Los_espacios_de_alrededor_se_quitan()
    {
        Assert.Equal("db", AsmStyle.Of("  db  ").Data);
    }

    /// <summary>
    /// Todos los exportadores hacen caso a la misma elección.
    /// </summary>
    /// <remarks>
    /// Son siete y cada uno emite lo suyo; con que uno se quedara con la de siempre, el juego
    /// exportado no ensamblaría entero y el fallo saldría en un fichero y no en otro.
    /// </remarks>
    [AvaloniaFact]
    public void Todos_los_exportadores_hacen_caso()
    {
        var style = new AsmStyle("XX");
        var tileSet = new TileSet("Bosque");
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        bank.NewGroup(0);
        bank.Animations.Add(new SpriteAnimation("Andar"));
        tileSet.Blocks.Add(new TileBlock("Bloque"));

        (string Who, string Asm)[] all =
        [
            ("tiles patrones", TileSetExporter.PatternsToAssembler(tileSet, style)),
            ("tiles colores", TileSetExporter.ColorsToAssembler(tileSet, style)),
            ("tiles atributos", TileSetExporter.AttributesToAssembler(tileSet, style)),
            ("supertiles", SuperTileExporter.ToAssembler(tileSet, style)),
            ("sprites patrones", SpriteBankExporter.PatternsToAssembler(bank, style)),
            ("sprites grupos", SpriteBankExporter.GroupsToAssembler(bank, style)),
            ("animaciones", SpriteAnimationExporter.ToAssembler(bank, style)),
            ("mapa", MapExporter.ToAssembler(new TileMap("Mapa", 4, 4), style)),
            ("paleta", PaletteExporter.ToAssembler(ColorPalette.CreateMsxStandard(), style)),
        ];

        foreach ((string who, string asm) in all)
        {
            Assert.True(asm.Contains("    XX  "), $"{who} no usa la directiva elegida");
            Assert.False(
                asm.Contains($"    {AsmStyle.Dotted}  "), $"{who} sigue con la de siempre");
        }
    }

    /// <summary>La elección se guarda con los demás ajustes y vuelve al arrancar.</summary>
    /// <remarks>
    /// Es lo que hace que sea «una vez y te olvidas»: sin guardarse habría que volver a
    /// ponerla en cada sesión, que es peor que preguntarlo al exportar.
    /// </remarks>
    [AvaloniaFact]
    public void La_eleccion_se_guarda_de_una_sesion_a_otra()
    {
        string folder = Directory.CreateTempSubdirectory("ajustes").FullName;

        try
        {
            var store = new SettingsStore(folder);
            var preferences = new EditorPreferences { AsmData = AsmStyle.Plain };

            store.Save(new Settings("es", preferences, []));

            Assert.Equal(AsmStyle.Plain, store.Load().Preferences.AsmData);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Y desde el formulario de preferencias llega a la exportación.</summary>
    [AvaloniaFact]
    public async Task Desde_las_preferencias_llega_a_la_exportacion()
    {
        var main = new MainWindowViewModel(new TestDialogService());
        var form = new EditPreferencesViewModel(main) { AsmData = AsmStyle.Plain };

        await form.AcceptPreferencesCommand.ExecuteAsync(null);

        Assert.Equal(AsmStyle.Plain, main.Preferences.AsmStyle.Data);
    }
}
