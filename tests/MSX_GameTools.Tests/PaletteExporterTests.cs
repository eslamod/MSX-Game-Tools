using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// La paleta en el formato del registro de paleta del V9938: dos bytes por color, el
/// rojo en el nibble alto del primero, el azul en el bajo y el verde en el segundo.
/// </summary>
public class PaletteExporterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxpx-{Guid.NewGuid():N}");

    public PaletteExporterTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [AvaloniaFact]
    public void Son_32_bytes_para_los_16_colores()
        => Assert.Equal(32, PaletteExporter.ToBinary(ColorPalette.CreateMsxStandard()).Length);

    [AvaloniaFact]
    public void El_primer_byte_lleva_rojo_y_azul_y_el_segundo_el_verde()
    {
        ColorPalette palette = new PaletteLibrary().Add("Prueba");
        palette[5].SetComponents(red: 7, green: 2, blue: 3);

        byte[] bytes = PaletteExporter.ToBinary(palette);

        Assert.Equal(0x73, bytes[5 * 2]);
        Assert.Equal(0x02, bytes[(5 * 2) + 1]);
    }

    /// <summary>
    /// Cada componente son tres bits y ocupa su sitio sin desbordarse al vecino: si el
    /// rojo se colara en el bit 7 o el azul en el 3, el color saldría mal en la máquina.
    /// </summary>
    [AvaloniaFact]
    public void Los_componentes_no_se_pisan_entre_ellos()
    {
        ColorPalette palette = new PaletteLibrary().Add("Prueba");
        palette[0].SetComponents(red: 7, green: 7, blue: 7);

        byte[] bytes = PaletteExporter.ToBinary(palette);

        Assert.Equal(0x77, bytes[0]);
        Assert.Equal(0x07, bytes[1]);
    }

    [AvaloniaFact]
    public void El_ensamblador_lleva_los_mismos_bytes_que_el_binario()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();
        byte[] binary = PaletteExporter.ToBinary(palette);

        List<byte> fromText = [];

        foreach (string line in PaletteExporter.ToAssembler(palette).Split(Environment.NewLine))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith(AsmStyle.Default.Data, StringComparison.Ordinal))
                continue;

            foreach (string value in trimmed[AsmStyle.Default.Data.Length..]
                         .Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                fromText.Add(Convert.ToByte(value.Trim()[AsmHex.Prefix.Length..], 16));
            }
        }

        Assert.Equal(binary, fromText);
    }

    /// <summary>
    /// Que hace falta un MSX2 no está en los bytes, así que si no lo dice la cabecera,
    /// quien lo cargue en un MSX1 no entenderá por qué no pasa nada.
    /// </summary>
    [AvaloniaFact]
    public void La_cabecera_dice_como_cargarla_y_que_en_MSX1_no_hay_paleta()
    {
        string asm = PaletteExporter.ToAssembler(ColorPalette.CreateMsxStandard());

        Assert.Contains("R#16 = 0", asm);
        Assert.Contains("port 0x9A", asm);
        Assert.Contains("MSX1 has no palette", asm);
        Assert.Contains("msx_palette:", asm);
        Assert.Contains("msx_palette_end:", asm);
    }

    /// <summary>
    /// Del panel de la paleta sale un solo fichero, con el nombre que se elija.
    /// </summary>
    /// <remarks>
    /// Y sin nada abierto: la paleta es la de la barra, no la del documento de delante.
    /// </remarks>
    [AvaloniaFact]
    public async Task Exportar_desde_el_panel_escribe_un_solo_fichero()
    {
        string path = Path.Combine(_folder, "msx_palette.bin");
        var main = new MainWindowViewModel(new TestDialogService());

        await TestExport.PaletteAsync(main, ExportFormat.Binary, path);

        Assert.Equal(
            (string[])["msx_palette.bin"],
            Directory.GetFiles(_folder).Select(Path.GetFileName));

        Assert.Equal(32, new FileInfo(path).Length);
    }
}
