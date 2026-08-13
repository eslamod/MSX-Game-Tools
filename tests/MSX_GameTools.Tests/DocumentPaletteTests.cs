using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// De quién es la paleta con la que se dibuja y se guarda un documento.
/// </summary>
/// <remarks>
/// Es suya. La paleta va embebida en el fichero, así que si el editor serializara la del
/// espacio de trabajo, abrir un segundo juego con otra paleta reescribiría el primero con
/// colores que nunca eligió. La barra de paletas de arriba dice cuál lleva el documento
/// que está delante, y tocarla cambia la de ése y sólo la de ése.
/// </remarks>
public class DocumentPaletteTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxpal-{Guid.NewGuid():N}");

    public DocumentPaletteTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>
    /// Dos juegos abiertos con paletas distintas, y se guarda el primero.
    /// </summary>
    /// <remarks>
    /// Por los comandos y no llamando al serializador a mano: el fallo estaba justo en el
    /// camino de en medio, en qué paleta le pasa el editor al guardar.
    /// </remarks>
    [AvaloniaFact]
    public async Task Guardar_un_juego_escribe_su_paleta_y_no_la_del_otro()
    {
        string bosque = WriteTileSet("Bosque", "Diurna", red: 7);
        string cueva = WriteTileSet("Cueva", "Nocturna", red: 1);

        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        dialogs.OpenPath = bosque;
        await main.OpenCommand.ExecuteAsync(null);

        dialogs.OpenPath = cueva;
        await main.OpenCommand.ExecuteAsync(null);

        // El usuario vuelve a la pestaña del primero y guarda. Ya tiene fichero, así que
        // no se pregunta nada y se escribe encima del suyo.
        main.SelectedTab = main.Tabs[0];
        await main.SaveDocumentCommand.ExecuteAsync(null);

        LoadedTileSet saved = TileSetSerializer.Deserialize(await File.ReadAllTextAsync(bosque));

        Assert.Equal("Diurna", saved.Palette.Name);
        Assert.Equal("700", saved.Palette[3].HexRgb);
    }

    /// <summary>Lo mismo con los bancos, que embeben la paleta por lo mismo.</summary>
    [AvaloniaFact]
    public async Task Guardar_un_banco_escribe_su_paleta_y_no_la_del_otro()
    {
        string heroe = WriteSpriteBank("Heroe", "Diurna", red: 7);
        string bicho = WriteSpriteBank("Bicho", "Nocturna", red: 1);

        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        dialogs.OpenPath = heroe;
        await main.OpenCommand.ExecuteAsync(null);

        dialogs.OpenPath = bicho;
        await main.OpenCommand.ExecuteAsync(null);

        main.SelectedTab = main.Tabs[0];
        await main.SaveDocumentCommand.ExecuteAsync(null);

        LoadedSpriteBank saved = SpriteBankSerializer.Deserialize(await File.ReadAllTextAsync(heroe));

        Assert.Equal("Diurna", saved.Palette.Name);
        Assert.Equal("700", saved.Palette[3].HexRgb);
    }

    /// <summary>
    /// La señal que lo delata en la ventana: abrir un segundo juego no toca al primero, y
    /// su pestaña no puede salir con el asterisco sin que nadie la haya tocado.
    /// </summary>
    [AvaloniaFact]
    public async Task Abrir_un_segundo_juego_no_deja_el_primero_sin_guardar()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        dialogs.OpenPath = WriteTileSet("Bosque", "Diurna", red: 7);
        await main.OpenCommand.ExecuteAsync(null);

        var primero = (TileSetEditorViewModel)main.Tabs[0];

        dialogs.OpenPath = WriteTileSet("Cueva", "Nocturna", red: 1);
        await main.OpenCommand.ExecuteAsync(null);

        Assert.False(primero.IsModified);
        Assert.False(primero.HasUnsavedChanges());
        Assert.Equal("Bosque (TS)", primero.TabLabel);
        Assert.Empty(main.UnsavedDocuments());
    }

    /// <summary>Cada uno sigue dibujando con la suya: el segundo no repinta al primero.</summary>
    [AvaloniaFact]
    public async Task Cada_juego_conserva_los_colores_con_los_que_se_abrio()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        dialogs.OpenPath = WriteTileSet("Bosque", "Diurna", red: 7);
        await main.OpenCommand.ExecuteAsync(null);

        dialogs.OpenPath = WriteTileSet("Cueva", "Nocturna", red: 1);
        await main.OpenCommand.ExecuteAsync(null);

        var primero = (TileSetEditorViewModel)main.Tabs[0];
        var segundo = (TileSetEditorViewModel)main.Tabs[1];

        Assert.Equal("Diurna", primero.ColorPalette.Name);
        Assert.Equal("Nocturna", segundo.ColorPalette.Name);
    }

    /// <summary>
    /// La barra de paletas enseña la del documento que está delante, que es lo que la
    /// hace útil cuando cada uno lleva la suya.
    /// </summary>
    [AvaloniaFact]
    public async Task La_barra_de_paletas_sigue_a_la_pestana_de_delante()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        dialogs.OpenPath = WriteTileSet("Bosque", "Diurna", red: 7);
        await main.OpenCommand.ExecuteAsync(null);

        dialogs.OpenPath = WriteTileSet("Cueva", "Nocturna", red: 1);
        await main.OpenCommand.ExecuteAsync(null);

        Assert.Equal("Nocturna", main.Palettes.ActivePalette.Name);

        main.SelectedTab = main.Tabs[0];

        Assert.Equal("Diurna", main.Palettes.ActivePalette.Name);
    }

    /// <summary>
    /// Y elegir otra en la barra se la pone al documento de delante, sin arrastrar a los
    /// demás: es la forma de cambiarle la paleta a un juego.
    /// </summary>
    [AvaloniaFact]
    public async Task Elegir_una_paleta_en_la_barra_se_la_pone_al_de_delante()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        dialogs.OpenPath = WriteTileSet("Bosque", "Diurna", red: 7);
        await main.OpenCommand.ExecuteAsync(null);

        dialogs.OpenPath = WriteTileSet("Cueva", "Nocturna", red: 1);
        await main.OpenCommand.ExecuteAsync(null);

        var primero = (TileSetEditorViewModel)main.Tabs[0];
        var segundo = (TileSetEditorViewModel)main.Tabs[1];

        // Con el segundo delante, se le pone la paleta del primero.
        main.Palettes.ActivePalette = primero.ColorPalette;

        Assert.Equal("Diurna", segundo.ColorPalette.Name);
        Assert.Equal("Diurna", primero.ColorPalette.Name);

        // Y eso sí es un cambio suyo, que hay que guardar.
        Assert.True(segundo.IsModified);
        Assert.False(primero.IsModified);
    }

    // ------------------------------------------------------------------ la ventana

    /// <summary>
    /// Lo mismo con la ventana montada, cambiando de pestaña y eligiendo en el desplegable
    /// de verdad. Probando el ViewModel suelto no se ejerce el enlace, que es lo que une la
    /// barra con la pestaña de delante.
    /// </summary>
    [AvaloniaFact]
    public async Task El_desplegable_y_las_pestanas_van_juntos_en_la_ventana()
    {
        var dialogs = new TestDialogService { ChooseAnswer = false };
        var main = new MainWindowViewModel(dialogs);

        dialogs.OpenPath = WriteTileSet("Bosque", "Diurna", red: 7);
        await main.OpenCommand.ExecuteAsync(null);

        dialogs.OpenPath = WriteTileSet("Cueva", "Nocturna", red: 1);
        await main.OpenCommand.ExecuteAsync(null);

        var window = new MainWindow { DataContext = main, Width = 1200, Height = 800 };

        window.Show();
        Pump();

        ComboBox combo = window.GetVisualDescendants()
            .OfType<ComboBox>()
            .First(box => ReferenceEquals(box.ItemsSource, main.Palettes.Palettes));

        TabControl tabs = window.GetVisualDescendants().OfType<TabControl>().First();

        Assert.Equal("Nocturna", ((ColorPalette)combo.SelectedItem!).Name);

        // Pasar a la pestaña del primero mueve el desplegable a su paleta.
        tabs.SelectedIndex = 0;
        Pump();

        Assert.Equal("Diurna", ((ColorPalette)combo.SelectedItem!).Name);

        // Y elegir en el desplegable se la cambia a él, que es la vuelta del enlace.
        var primero = (TileSetEditorViewModel)main.Tabs[0];

        combo.SelectedItem = main.Palettes.Palettes.Single(palette => palette.Name == "Nocturna");
        Pump();

        Assert.Equal("Nocturna", primero.ColorPalette.Name);
        Assert.True(primero.IsModified);

        Close(window);
    }

    // ------------------------------------------------------- al crear el documento

    /// <summary>
    /// Los formularios proponen la que enseña la barra, que es de donde salía antes sin
    /// decirlo.
    /// </summary>
    [AvaloniaFact]
    public void Los_formularios_proponen_la_paleta_que_enseña_la_barra()
    {
        var main = new MainWindowViewModel();

        main.Palettes.Import(Palette("Nocturna", red: 1));

        main.AddTileSetCommand.Execute(null);

        Assert.Equal("Nocturna", ((EditTileSetViewModel)main.RightPanViewModel!).Palette.Name);

        main.CloseRightPanel(main.RightPanViewModel);
        main.AddSpriteBankCommand.Execute(null);

        Assert.Equal("Nocturna", ((EditSpriteBankViewModel)main.RightPanViewModel!).Palette.Name);
    }

    [AvaloniaFact]
    public void El_juego_nace_con_la_paleta_elegida_en_el_formulario()
    {
        var main = new MainWindowViewModel();

        ColorPalette chosen = main.Palettes.Import(Palette("Nocturna", red: 1));
        ColorPalette other = main.Palettes.Import(Palette("Diurna", red: 7));

        main.AddTileSetCommand.Execute(null);

        var form = (EditTileSetViewModel)main.RightPanViewModel!;

        // La barra se quedó en la última importada; se elige otra a mano.
        Assert.Same(other, form.Palette);

        form.Name = "Bosque";
        form.Palette = chosen;
        form.AcceptTileSetCommand.Execute(null);

        var tiles = (TileSetEditorViewModel)main.Tabs[0];

        Assert.Same(chosen, tiles.ColorPalette);

        // Y la barra pasa a enseñar la suya, que es la pestaña que queda delante.
        Assert.Same(chosen, main.Palettes.ActivePalette);
    }

    [AvaloniaFact]
    public void El_banco_nace_con_la_paleta_elegida_en_el_formulario()
    {
        var main = new MainWindowViewModel();

        ColorPalette chosen = main.Palettes.Import(Palette("Nocturna", red: 1));
        main.Palettes.Import(Palette("Diurna", red: 7));

        main.AddSpriteBankCommand.Execute(null);

        var form = (EditSpriteBankViewModel)main.RightPanViewModel!;
        form.Name = "Bichos";
        form.Palette = chosen;
        form.AcceptSpriteBankCommand.Execute(null);

        Assert.Same(chosen, ((SpritesEditorViewModel)main.Tabs[0]).ColorPalette);
    }

    /// <summary>
    /// El desplegable del formulario, montado de verdad: un nombre mal escrito en el XAML
    /// dejaría el enlace muerto y el juego seguiría naciendo con la de la barra.
    /// </summary>
    [AvaloniaFact]
    public void El_formulario_de_tileset_tiene_su_desplegable_de_paletas()
    {
        var main = new MainWindowViewModel();

        main.Palettes.Import(Palette("Nocturna", red: 1));
        main.AddTileSetCommand.Execute(null);

        var form = (EditTileSetViewModel)main.RightPanViewModel!;
        var view = new EditTileSetView { DataContext = form };
        var window = new Window { Content = view, Width = 400, Height = 400 };

        window.Show();
        Pump();

        ComboBox combo = view.GetVisualDescendants()
            .OfType<ComboBox>()
            .Single(box => ReferenceEquals(box.ItemsSource, main.Palettes.Palettes));

        Assert.Same(form.Palette, combo.SelectedItem);

        combo.SelectedItem = main.Palettes.Palettes.Single(palette => palette.Name == "Nocturna");
        Pump();

        Assert.Equal("Nocturna", form.Palette.Name);

        window.Close();
        Pump();
    }

    // ------------------------------------------------------------------ utilidades

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    /// <summary>Dos vueltas: la primera deja la pregunta en la cola y la segunda la atiende.</summary>
    private static void Close(Window window)
    {
        window.Close();
        Pump();
        Pump();
    }

    /// <summary>Un fichero de juego de tiles con su paleta dentro.</summary>
    private string WriteTileSet(string name, string paletteName, int red)
    {
        string path = Path.Combine(_folder, $"{name}.json");

        File.WriteAllText(path, TileSetSerializer.Serialize(new TileSet(name), Palette(paletteName, red)));

        return path;
    }

    private string WriteSpriteBank(string name, string paletteName, int red)
    {
        string path = Path.Combine(_folder, $"{name}.json");

        File.WriteAllText(
            path,
            SpriteBankSerializer.Serialize(
                new SpriteBank(name: name), Palette(paletteName, red), backgroundColorIndex: 1));

        return path;
    }

    /// <summary>
    /// Una paleta con nombre y un color distinto, para que la biblioteca no las tome por
    /// la misma al cargar el segundo fichero.
    /// </summary>
    private static ColorPalette Palette(string name, int red)
    {
        ColorPalette palette = new PaletteLibrary().Add(name);

        palette[3].SetComponents(red, 0, 0);

        return palette;
    }
}
