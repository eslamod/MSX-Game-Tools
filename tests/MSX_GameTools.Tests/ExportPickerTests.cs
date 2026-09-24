using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Con qué filtro abre el selector cada exportación.
/// </summary>
/// <remarks>
/// Salían con el de los ficheros del editor, que es el que trae por omisión el servicio de
/// diálogos: al exportar a ensamblador el selector proponía «tiles.asm» y a la vez no
/// enseñaba ni un .asm, sólo los json. Se comprueba contra la extensión que propone el
/// propio comando, así que una exportación nueva entra sola.
/// </remarks>
public class ExportPickerTests
{
    [AvaloniaFact]
    public async Task Cada_exportacion_ofrece_el_formato_que_va_a_escribir()
    {
        // Sin ruta que devolver, cada comando se para justo después de preguntar: se mira
        // cómo se abrió el selector sin llegar a escribir nada en el disco.
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);
        var wrong = new List<string>();

        // Los documentos que preguntan desde su panel abren el selector con el botón de
        // elegir destino, no con una entrada del menú.
        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        await CheckPanel(main.ExportSpriteBankCommand, ExportFormat.Binary);
        await CheckPanel(main.ExportSpriteBankCommand, ExportFormat.Assembler);

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        await CheckPanel(main.ExportTileSetCommand, ExportFormat.Binary);
        await CheckPanel(main.ExportTileSetCommand, ExportFormat.Assembler);
        await CheckPanel(main.ExportTileSetCommand, ExportFormat.Png);

        main.OpenMap(new TileMap("Nivel 1", 8, 8), tiles);

        await CheckPanel(main.ExportMapCommand, ExportFormat.Binary);
        await CheckPanel(main.ExportMapCommand, ExportFormat.Assembler);
        await CheckPanel(main.ExportMapCommand, ExportFormat.Csv);

        // La paleta es la de la barra, no la del documento de delante: se exporta con
        // cualquier pestaña abierta.
        await CheckPanel(main.ExportPaletteCommand, ExportFormat.Binary);
        await CheckPanel(main.ExportPaletteCommand, ExportFormat.Assembler);

        Assert.Empty(wrong);

        async Task CheckPanel(IRelayCommand open, ExportFormat format)
        {
            open.Execute(null);

            var form = (ExportViewModel)main.RightPanViewModel!;

            form.Format = form.Formats.Single(choice => choice.Format == format);

            await Check(form.BrowseCommand);
        }

        async Task Check(IAsyncRelayCommand command)
        {
            int asked = dialogs.SaveCalls;

            await command.ExecuteAsync(null);

            // Si el comando se hubiera parado antes de preguntar, lo anotado sería de la
            // llamada anterior y esto valdría para nada.
            Assert.Equal(asked + 1, dialogs.SaveCalls);

            string extension = Path.GetExtension(dialogs.LastSuggestedFileName ?? string.Empty);

            if (FilterFor(extension) != dialogs.LastSavePickerKind)
                wrong.Add($"{dialogs.LastSuggestedFileName}: {dialogs.LastSavePickerKind}");
        }
    }

    /// <summary>El filtro que le toca a una extensión, o <c>null</c> si no es de exportar.</summary>
    private static PickerFileKind? FilterFor(string extension) => extension switch
    {
        ".asm" => PickerFileKind.Assembler,
        ".bin" => PickerFileKind.Binary,
        ".csv" => PickerFileKind.Csv,
        ".png" => PickerFileKind.Image,
        _ => null,
    };
}
