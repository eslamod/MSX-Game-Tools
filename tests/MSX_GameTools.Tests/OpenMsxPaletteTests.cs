using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// La paleta tal y como la escupe la consola de openMSX.
/// </summary>
/// <remarks>
/// Dieciséis parejas <c>índice:RGB</c> con las componentes de 0 a 7, que es como las guarda el
/// V9938: tres bits cada una.
/// </remarks>
public class OpenMsxPaletteTests
{
    /// <summary>La salida de openMSX, en las cuatro columnas en que sale y con su cola de espacios.</summary>
    private const string Dumped = """
        0:114  4:225  8:445  c:236
        1:300  5:222  9:447  d:333
        2:510  6:700  a:770  e:777
        3:740  7:117  b:467  f:000
        """;

    /// <summary>Cada pareja va a su sitio, y las componentes en su orden.</summary>
    [AvaloniaFact]
    public void Cada_color_va_a_su_indice()
    {
        ColorPalette palette = OpenMsxPalette.Read(Dumped, "Juego");

        Assert.Equal("Juego", palette.Name);
        Assert.Equal(ColorPalette.Size, palette.Count);

        // El 3 es «740»: rojo a tope, verde a cuatro y azul a cero.
        Assert.Equal(7, palette[3].Red);
        Assert.Equal(4, palette[3].Green);
        Assert.Equal(0, palette[3].Blue);

        // Y el c, que va con letra: «236».
        Assert.Equal(2, palette[12].Red);
        Assert.Equal(3, palette[12].Green);
        Assert.Equal(6, palette[12].Blue);
    }

    /// <summary>
    /// Cómo estén repartidas las parejas por las líneas da igual.
    /// </summary>
    /// <remarks>
    /// Se leen las parejas y no las filas, así que vale pegar la salida en cuatro columnas
    /// como sale, o en una, o todo seguido.
    /// </remarks>
    [AvaloniaFact]
    public void El_reparto_por_lineas_da_igual()
    {
        ColorPalette columns = OpenMsxPalette.Read(Dumped, "Juego");
        ColorPalette single = OpenMsxPalette.Read(
            string.Join(' ', Dumped.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)),
            "Juego");

        for (int index = 0; index < ColorPalette.Size; index++)
            Assert.Equal(columns[index].HexRgb, single[index].HexRgb);
    }

    /// <summary>
    /// El color 0 se lee y se guarda aunque no se vea.
    /// </summary>
    /// <remarks>
    /// En el editor el 0 no es un color sino «no pintes aquí», así que no aparece en ninguna
    /// parte. Pero el juego carga sus dos bytes al escribir la paleta y la exportación los
    /// saca: tirándolo, la paleta exportada no sería la del juego.
    /// </remarks>
    [AvaloniaFact]
    public void El_color_cero_se_guarda_aunque_no_se_vea()
    {
        ColorPalette palette = OpenMsxPalette.Read(Dumped, "Juego");

        Assert.Equal("114", palette[0].HexRgb);

        // Y sale por la exportación, que es para lo que se guarda.
        byte[] bytes = PaletteExporter.ToBinary(palette);

        Assert.Equal(0x14, bytes[0]);   // 0RRR0BBB
        Assert.Equal(0x01, bytes[1]);   // 00000GGG
    }

    /// <summary>Una componente de 8 o de 9 no es de un MSX, y se dice.</summary>
    /// <remarks>
    /// Son tres bits: de 0 a 7. Colando un 8 saldría un color que en la máquina no existe, y
    /// el fallo se vería mucho más tarde y en otro sitio.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("890")]
    [InlineData("078")]
    public void Una_componente_de_mas_de_siete_se_rechaza(string bad)
    {
        FileFormatException failed = Assert.Throws<FileFormatException>(
            () => OpenMsxPalette.Read(Dumped.Replace("0:114", $"0:{bad}"), "Juego"));

        Assert.Contains("0 a 7", failed.Message);
    }

    /// <summary>Y si falta algún color se dice cuál, en vez de rellenarlo por lo bajo.</summary>
    /// <remarks>
    /// Rellenar sería peor: saldría una paleta con pinta de buena y un color cambiado, sin
    /// nada que lo delatara hasta verlo en la máquina.
    /// </remarks>
    [AvaloniaFact]
    public void Si_falta_un_color_se_dice_cual()
    {
        FileFormatException failed = Assert.Throws<FileFormatException>(
            () => OpenMsxPalette.Read(Dumped.Replace("a:770", string.Empty), "Juego"));

        Assert.EndsWith(": a.", failed.Message);
    }

    /// <summary>Un fichero que no es una paleta se rechaza diciendo qué se esperaba.</summary>
    [AvaloniaFact]
    public void Un_fichero_que_no_es_una_paleta_se_rechaza()
    {
        FileFormatException failed = Assert.Throws<FileFormatException>(
            () => OpenMsxPalette.Read("hola qué tal", "Juego"));

        Assert.Contains("3:740", failed.Message);
    }

    /// <summary>Y un color repetido también, que es una salida a medias pegada dos veces.</summary>
    [AvaloniaFact]
    public void Un_color_repetido_se_rechaza()
    {
        FileFormatException failed = Assert.Throws<FileFormatException>(
            () => OpenMsxPalette.Read(Dumped + " 3:000", "Juego"));

        Assert.Equal("El color 3 está dos veces.", failed.Message);
    }

    /// <summary>Desde el menú, la paleta entra en la biblioteca y se queda puesta.</summary>
    [AvaloniaFact]
    public async Task Desde_el_menu_la_paleta_entra_en_la_biblioteca()
    {
        string folder = Directory.CreateTempSubdirectory("palette").FullName;
        string path = Path.Combine(folder, "Knightmare.txt");

        try
        {
            await File.WriteAllTextAsync(path, Dumped);

            var dialogs = new TestDialogService { OpenPath = path };
            var main = new MainWindowViewModel(dialogs);

            int before = main.Palettes.Palettes.Count;

            await main.ImportOpenMsxPaletteCommand.ExecuteAsync(null);

            Assert.Equal(before + 1, main.Palettes.Palettes.Count);
            Assert.Equal("Knightmare", main.Palettes.ActivePalette.Name);
            Assert.Equal("740", main.Palettes.ActivePalette[3].HexRgb);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Y lo que no se puede leer se dice, sin dejar media paleta puesta.</summary>
    [AvaloniaFact]
    public async Task Lo_que_no_se_puede_leer_se_dice()
    {
        string folder = Directory.CreateTempSubdirectory("palette").FullName;
        string path = Path.Combine(folder, "cualquiera.txt");

        try
        {
            await File.WriteAllTextAsync(path, "esto no es una paleta");

            var dialogs = new TestDialogService { OpenPath = path };
            var main = new MainWindowViewModel(dialogs);

            int before = main.Palettes.Palettes.Count;

            await main.ImportOpenMsxPaletteCommand.ExecuteAsync(null);

            Assert.Equal(before, main.Palettes.Palettes.Count);
            Assert.Single(dialogs.Messages);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
