using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Saltarse las celdas vacías al traer sólo la tabla de patrones.
/// </summary>
/// <remarks>
/// <para>
/// Los dos comportamientos son legítimos y por eso se elige. Trayéndolas, el patrón número N es
/// la celda número N de lo que se eligió, y un juego que direccione el patrón por la posición de
/// la celda cuenta con eso. Saltándoselas se ahorran treinta y dos bytes y un hueco del banco
/// por cada celda en blanco, que en una hoja con separación entre figuras es media tabla.
/// </para>
/// <para>
/// En el modo de color no hace falta preguntarlo: una celda sin nada que pintar no tiene ningún
/// plano que colocar, así que ya se salta sola.
/// </para>
/// </remarks>
public class SheetImportSkipEmptyTests
{
    private const int Fondo = unchecked((int)0xFF000000);
    private const int Rojo = unchecked((int)0xFFFF0000);

    /// <summary>Una hoja de cuatro celdas donde la segunda y la cuarta están en blanco.</summary>
    private static int[] Sheet()
    {
        int[] pixels = new int[32 * 8];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Fondo;

        pixels[0] = Rojo;
        pixels[16] = Rojo;

        return pixels;
    }

    private static SheetSelection Four => new(0, 0, 4, 1);

    [Fact]
    public void Sin_decir_nada_las_vacias_gastan_su_patron()
    {
        SheetImport import = SpriteSheetImporter.ImportPatterns(
            Sheet(), new PixelSize(32, 8), 8, Color.FromUInt32(0xFF000000), Four, "Bichos");

        Assert.True(import.Ok);
        Assert.Equal(4, import.Analysis.Patterns);

        // Y la tercera celda, que tiene dibujo, es el patrón 2.
        Assert.True(Drawn(import.Bank!, 2));
        Assert.False(Drawn(import.Bank!, 1));
    }

    /// <summary>
    /// Saltándoselas, sólo se llevan patrón las que tienen dibujo.
    /// </summary>
    /// <remarks>
    /// Y se corren: la tercera celda pasa a ser el patrón 1. Es justo lo que se pierde, y por eso
    /// no es lo de siempre.
    /// </remarks>
    [Fact]
    public void Saltandoselas_solo_gastan_patron_las_que_dibujan()
    {
        SheetImport import = SpriteSheetImporter.ImportPatterns(
            Sheet(), new PixelSize(32, 8), 8, Color.FromUInt32(0xFF000000), Four, "Bichos",
            skipEmpty: true);

        Assert.True(import.Ok);
        Assert.Equal(2, import.Analysis.Patterns);

        Assert.True(Drawn(import.Bank!, 0));
        Assert.True(Drawn(import.Bank!, 1));
        Assert.False(Drawn(import.Bank!, 2));
    }

    /// <summary>
    /// El informe lo dice antes de importar, que es de lo que se trata.
    /// </summary>
    /// <remarks>
    /// Sale de mirar los pixeles: la cuenta de antes daba una celda un patrón sin abrir la hoja,
    /// y con esto el número de la izquierda dejaría de cuadrar con lo que se va a escribir.
    /// </remarks>
    [Fact]
    public void El_informe_cuenta_lo_que_de_verdad_va_a_gastar()
    {
        SheetAnalysis all = SpriteSheetAnalysis.AnalysePatterns(
            Sheet(), new PixelSize(32, 8), 8, Color.FromUInt32(0xFF000000), Four);

        SheetAnalysis some = SpriteSheetAnalysis.AnalysePatterns(
            Sheet(), new PixelSize(32, 8), 8, Color.FromUInt32(0xFF000000), Four, skipEmpty: true);

        Assert.Equal(4, all.Patterns);
        Assert.Equal(2, some.Patterns);

        // Las celdas siguen siendo cuatro; lo que cambia es cuántas traen algo.
        Assert.Equal(4, some.Cells.Count);
        Assert.Equal(2, some.Cells.Count(cell => cell.Planes > 0));
    }

    private static bool Drawn(SpriteBank bank, int pattern) =>
        bank.SpritesList[pattern].ArraySpriteRows.Any(row => row.ArrayColumns.Any(on => on));

    /// <summary>
    /// La casilla se ve siempre, y en el modo de color sale marcada y apagada.
    /// </summary>
    /// <remarks>
    /// Escondiéndola no se aprende que la opción existe ni que en el modo de color ya está
    /// puesta: allí las vacías se saltan de todas formas, porque una celda sin nada que pintar
    /// no tiene ningún plano que colocar. Nace de ir a buscarla y no encontrarla.
    /// </remarks>
    [AvaloniaFact]
    public void La_casilla_dice_lo_que_va_a_pasar_en_los_dos_modos()
    {
        var main = new MainWindowViewModel();
        var form = new ImportSpriteSheetViewModel(
            main, "hoja.png", new int[8 * 8], new PixelSize(8, 8), new Avalonia.Media.Imaging.WriteableBitmap(
                new PixelSize(8, 8), new Vector(96, 96)));

        // Trayendo el color, marcada y sin poder tocarla: alli se saltan siempre.
        Assert.False(form.OnlyPatterns);
        Assert.True(form.SkipsEmpty);

        form.OnlyPatterns = true;

        // Y en el de patrones manda lo que se elija, que arranca apagado.
        Assert.False(form.SkipsEmpty);

        form.SkipsEmpty = true;

        Assert.True(form.SkipEmpty);
    }
}
