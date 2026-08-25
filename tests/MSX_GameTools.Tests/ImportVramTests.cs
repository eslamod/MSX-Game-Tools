using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El formulario de traer un volcado de VRAM.
/// </summary>
/// <remarks>
/// Lo que se comprueba aquí es la parte que no está en el importador: que cargar los registros
/// rellene los campos en vez de importar por su cuenta, que los sitios que se ofrecen sean los
/// legales del modo, y que aceptar deje abierto lo que se pidió.
/// </remarks>
public class ImportVramTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("vram").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>
    /// Cargar los registros rellena los campos, no importa por su cuenta.
    /// </summary>
    /// <remarks>
    /// Es la forma acordada: el fichero es la autoridad y los desplegables son la vista y el
    /// volante. Así se ve lo detectado antes de aceptar y se puede corregir. Los registros son
    /// los de Knightmare, que tiene los patrones y los colores al revés de lo corriente.
    /// </remarks>
    [AvaloniaFact]
    public async Task Los_registros_rellenan_los_campos()
    {
        var dialogs = new TestDialogService { OpenPath = Written("registros.bin", Knightmare()) };
        var main = new MainWindowViewModel(dialogs);

        ImportVramViewModel form = Form(main);

        Assert.Equal(0x0000, form.Patterns);
        Assert.Equal(0x2000, form.Colors);

        await form.LoadRegistersCommand.ExecuteAsync(null);

        Assert.Equal(VdpRegisters.ScreenMode.Graphic2, form.Mode);
        Assert.Equal(0x2000, form.Patterns);
        Assert.Equal(0x0000, form.Colors);
        Assert.Equal(0x1800, form.SpritePatterns);
        Assert.True(form.BigSprites);

        // Y se dice de dónde salieron, que si no no hay forma de saber si se acertaron.
        Assert.Contains("registros.bin", form.Detected);
    }

    /// <summary>
    /// Un fichero que no es un volcado de registros se rechaza y se dice.
    /// </summary>
    /// <remarks>
    /// Por el comando y no llamando a Apply a mano: es el formulario quien tiene que decidir
    /// que ese fichero no vale, y una prueba que sólo llame a Apply cuando ya se sabe que vale
    /// no comprueba nada de eso.
    /// </remarks>
    [AvaloniaFact]
    public async Task Un_fichero_que_no_son_registros_no_toca_nada()
    {
        var dialogs = new TestDialogService { OpenPath = Written("corto.bin", new byte[8]) };
        var main = new MainWindowViewModel(dialogs);

        ImportVramViewModel form = Form(main);

        await form.LoadRegistersCommand.ExecuteAsync(null);

        Assert.Equal(0x0000, form.Patterns);
        Assert.Equal(0x2000, form.Colors);

        // Y lo dice, que si no parece que se ha cargado y no ha pasado nada.
        Assert.Contains(VdpRegisters.Count.ToString(), form.Detected);
    }

    /// <summary>
    /// Los sitios que se ofrecen son los legales del modo.
    /// </summary>
    /// <remarks>
    /// En GRAPHIC 2 y 3 esas dos tablas sólo pueden empezar en 0000H o 2000H, porque los bits
    /// bajos de R#4 y R#3 son una máscara sobre los tercios y no parte de la dirección. En
    /// screen 1 sí se mueven de 2 KB en 2 KB.
    /// </remarks>
    [AvaloniaFact]
    public void Los_sitios_que_se_ofrecen_son_los_del_modo()
    {
        ImportVramViewModel form = Form(new MainWindowViewModel());

        Assert.Equal([0x0000, 0x2000], form.TableChoices);

        form.Mode = VdpRegisters.ScreenMode.Graphic1;

        Assert.Equal(8, form.TableChoices.Count);
        Assert.Contains(0x1800, form.TableChoices);
    }

    /// <summary>Aceptar deja abiertos los juegos de tiles y el banco.</summary>
    [AvaloniaFact]
    public void Aceptar_abre_lo_que_se_pidio()
    {
        var main = new MainWindowViewModel();
        ImportVramViewModel form = Form(main);

        int before = main.Tabs.Count;

        form.AcceptImportCommand.Execute(null);

        // Un juego de tiles y un banco de sprites, y el formulario se cierra.
        Assert.Equal(before + 2, main.Tabs.Count);
        Assert.Null(main.RightPanViewModel);
    }

    /// <summary>Y sin marcar nada no se puede aceptar.</summary>
    [AvaloniaFact]
    public void Sin_marcar_nada_no_se_acepta()
    {
        ImportVramViewModel form = Form(new MainWindowViewModel());

        form.WantsTiles = false;
        form.WantsSprites = false;

        Assert.False(form.CanAccept);
        Assert.False(form.AcceptImportCommand.CanExecute(null));
    }

    /// <summary>
    /// El formulario sale por el ViewLocator, con sus enlaces vivos.
    /// </summary>
    /// <remarks>
    /// Un nombre mal escrito en el XAML no lo caza nadie más: el modelo de vista puede estar
    /// perfecto y el panel salir vacío.
    /// </remarks>
    [AvaloniaFact]
    public void El_panel_sale_montado()
    {
        var main = new MainWindowViewModel();

        main.RightPanViewModel = Form(main);

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };

        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Single(window.GetVisualDescendants().OfType<ImportVramView>());

        window.Close();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un formulario sobre una VRAM con un juego de tiles de verdad dentro.</summary>
    private static ImportVramViewModel Form(MainWindowViewModel main)
    {
        var tileSet = new TileSet("Bosque");

        tileSet.ListOfTiles[0].ArrayTileRows[0].ArrayPattern[0] = true;

        var vram = new byte[16384];
        var layout = new VramImporter.VramLayout();

        for (int third = 0; third < 3; third++)
        {
            TileSetExporter.PatternsToBinary(tileSet)
                .CopyTo(vram, layout.Patterns + (third * VramImporter.ThirdBytes));

            TileSetExporter.ColorsToBinary(tileSet)
                .CopyTo(vram, layout.Colors + (third * VramImporter.ThirdBytes));
        }

        return new ImportVramViewModel(main, "volcado.bin", vram);
    }

    private string Written(string name, byte[] bytes)
    {
        string path = Path.Combine(_folder, name);

        File.WriteAllBytes(path, bytes);

        return path;
    }

    /// <summary>Los registros del volcado de Knightmare, tal cual.</summary>
    private static byte[] Knightmare()
    {
        var dump = new byte[VdpRegisters.Count];

        new byte[] { 0x02, 0xE2, 0x0E, 0x7F, 0x07, 0x76, 0x03, 0xE0 }.CopyTo(dump, 0);

        return dump;
    }
}
