using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Saber qué está sin guardar: el asterisco de la pestaña, Guardar sobre el mismo
/// fichero y el aviso al salir.
/// </summary>
/// <remarks>
/// Hay dos señales y hacen cosas distintas. El asterisco es barata y generosa: se pone al
/// tocar cualquier cosa y puede sobrar. Lo que decide si hay trabajo que perder compara el
/// contenido de verdad con lo último que se guardó, así que una marca que falte afea la
/// pestaña pero no pierde nada, y una que sobre no acaba en un aviso falso.
/// </remarks>
public class UnsavedChangesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxsave-{Guid.NewGuid():N}");

    public UnsavedChangesTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // ------------------------------------------------------------------ el asterisco

    /// <summary>
    /// De la pulsación del ratón hasta el texto de la pestaña, que es lo que no se ve
    /// leyendo: el enlace va a TabLabel y no a Header.
    /// </summary>
    [AvaloniaFact]
    public void Pintar_en_el_mapa_pone_el_asterisco_en_la_pestana()
    {
        // Contesta «salir sin guardar», que es lo que hace falta para poder cerrar al final.
        var main = new MainWindowViewModel(new TestDialogService { ChooseAnswer = false });

        main.OpenTileSet(new TileSet("Bosque")).MarkClean();

        NewMap(main, "Nivel 1");

        var window = new MainWindow { DataContext = main, Width = 1200, Height = 800 };

        window.Show();
        Pump();

        Assert.Equal("Nivel 1 (MP)", TabHeader(window, 1));

        MapCanvas canvas = window.GetVisualDescendants().OfType<MapCanvas>().Single();
        Point origin = canvas.TranslatePoint(new Point(0, 0), window)!.Value;

        window.MouseDown(origin + new Point(24, 24), MouseButton.Left);
        Pump();
        window.MouseUp(origin + new Point(24, 24), MouseButton.Left);
        Pump();

        Assert.Equal("Nivel 1 (MP) *", TabHeader(window, 1));

        Close(window);
    }

    [AvaloniaFact]
    public void Un_documento_recien_creado_no_esta_marcado()
    {
        var main = new MainWindowViewModel();

        main.AddTileSetCommand.Execute(null);

        var form = (EditTileSetViewModel)main.RightPanViewModel!;
        form.Name = "Bosque";
        form.AcceptTileSetCommand.Execute(null);

        var tiles = (TileSetEditorViewModel)main.Tabs[0];

        Assert.False(tiles.IsModified);
        Assert.False(tiles.HasUnsavedChanges());
        Assert.Equal("Bosque (TS)", tiles.TabLabel);
    }

    [AvaloniaFact]
    public void Pintar_un_tile_deja_el_juego_sin_guardar()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));
        tiles.MarkClean();

        tiles.PixelSurface.Set(3, 4, true);

        Assert.True(tiles.IsModified);
        Assert.True(tiles.HasUnsavedChanges());
    }

    /// <summary>
    /// Los bloques se guardan dentro del fichero del juego, así que lo que queda sin
    /// guardar es el juego y no el panel de bloques, que ni siquiera tiene pestaña propia.
    /// </summary>
    [AvaloniaFact]
    public void Pintar_un_bloque_deja_sin_guardar_el_juego_de_tiles()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));
        tiles.MarkClean();

        var blocks = new TileBlocksViewModel(tiles);
        blocks.AddBlockCommand.Execute(null);
        blocks.Paint(0, 0);

        Assert.True(tiles.IsModified);
        Assert.True(tiles.HasUnsavedChanges());
        Assert.False(blocks.IsModified);
    }

    // ------------------------------------------------------------------ guardar

    [AvaloniaFact]
    public async Task Guardar_quita_el_asterisco_y_apunta_el_fichero()
    {
        string path = Path.Combine(_folder, "nivel.json");
        var main = new MainWindowViewModel(new TestDialogService { SavePath = path });

        MapEditorViewModel map = MapWithSomethingPainted(main);

        Assert.True(map.IsModified);

        await main.SaveDocumentCommand.ExecuteAsync(null);

        Assert.False(map.IsModified);
        Assert.Equal("Nivel 1 (MP)", map.TabLabel);
        Assert.Equal(path, map.FilePath);
    }

    /// <summary>Guardar a secas es eso: sobre el mismo fichero y sin volver a preguntar.</summary>
    [AvaloniaFact]
    public async Task Guardar_dos_veces_no_vuelve_a_pedir_la_ruta()
    {
        string path = Path.Combine(_folder, "nivel.json");
        var dialogs = new TestDialogService { SavePath = path };
        var main = new MainWindowViewModel(dialogs);

        MapEditorViewModel map = MapWithSomethingPainted(main);

        await main.SaveDocumentCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.SaveCalls);

        map.PickTile(TilePatch.Single(7), "Tile 7");
        map.Paint(5, 5);

        await main.SaveDocumentCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.SaveCalls);
        Assert.False(map.IsModified);

        // Y lo segundo también ha llegado al fichero, no sólo lo primero.
        Assert.Contains("07", File.ReadAllText(path));
    }

    [AvaloniaFact]
    public async Task Guardar_como_pregunta_aunque_ya_tenga_fichero()
    {
        var dialogs = new TestDialogService { SavePath = Path.Combine(_folder, "nivel.json") };
        var main = new MainWindowViewModel(dialogs);

        MapWithSomethingPainted(main);

        await main.SaveDocumentCommand.ExecuteAsync(null);
        await main.SaveDocumentAsCommand.ExecuteAsync(null);

        Assert.Equal(2, dialogs.SaveCalls);
    }

    [AvaloniaFact]
    public async Task Un_mapa_recien_cargado_no_esta_marcado()
    {
        string path = Path.Combine(_folder, "nivel.json");
        var main = new MainWindowViewModel(new TestDialogService { SavePath = path, OpenPath = path });

        main.OpenTileSet(new TileSet("Bosque")).MarkClean();
        MapWithSomethingPainted(main);

        await main.SaveDocumentCommand.ExecuteAsync(null);
        await main.OpenCommand.ExecuteAsync(null);

        MapEditorViewModel loaded = main.Tabs.OfType<MapEditorViewModel>().Last();

        Assert.False(loaded.IsModified);
        Assert.False(loaded.HasUnsavedChanges());
        Assert.Equal(path, loaded.FilePath);
    }

    /// <summary>
    /// Lo importado sí nace sin guardar: trae trabajo dentro y no tiene todavía un fichero
    /// del editor donde estar.
    /// </summary>
    [AvaloniaFact]
    public async Task Un_csv_importado_esta_sin_guardar_desde_el_principio()
    {
        string path = Path.Combine(_folder, "tiled.csv");
        await File.WriteAllTextAsync(path, "1,2,3\n4,5,6\n");

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = path });
        main.OpenTileSet(new TileSet("Bosque")).MarkClean();

        await main.ImportMapCsvCommand.ExecuteAsync(null);

        MapEditorViewModel imported = main.Tabs.OfType<MapEditorViewModel>().Single();

        Assert.True(imported.HasUnsavedChanges());
        Assert.Null(imported.FilePath);
    }

    // ------------------------------------------------------------------ salir

    [AvaloniaFact]
    public async Task Sin_nada_pendiente_no_se_pregunta_nada()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        main.OpenTileSet(new TileSet("Bosque")).MarkClean();

        Assert.True(await main.ConfirmExitAsync());
        Assert.Equal(0, dialogs.ChooseCalls);
    }

    [AvaloniaFact]
    public async Task Salir_sin_guardar_deja_salir_y_no_escribe_nada()
    {
        var dialogs = new TestDialogService { ChooseAnswer = false, SavePath = Path.Combine(_folder, "x.json") };
        var main = new MainWindowViewModel(dialogs);

        MapWithSomethingPainted(main);

        Assert.True(await main.ConfirmExitAsync());
        Assert.Equal(1, dialogs.ChooseCalls);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [AvaloniaFact]
    public async Task Cancelar_el_aviso_no_deja_salir()
    {
        var main = new MainWindowViewModel(new TestDialogService { ChooseAnswer = null });

        MapWithSomethingPainted(main);

        Assert.False(await main.ConfirmExitAsync());
    }

    [AvaloniaFact]
    public async Task Guardar_y_salir_escribe_el_fichero()
    {
        string path = Path.Combine(_folder, "nivel.json");
        var main = new MainWindowViewModel(new TestDialogService { ChooseAnswer = true, SavePath = path });

        MapWithSomethingPainted(main);

        Assert.True(await main.ConfirmExitAsync());
        Assert.True(File.Exists(path));
    }

    /// <summary>Guardar y que se cancele el selector no puede acabar en salir.</summary>
    [AvaloniaFact]
    public async Task Si_se_cancela_el_selector_al_guardar_no_se_sale()
    {
        var main = new MainWindowViewModel(new TestDialogService { ChooseAnswer = true, SavePath = null });

        MapWithSomethingPainted(main);

        Assert.False(await main.ConfirmExitAsync());
    }

    /// <summary>Cerrar la pestaña no cierra el documento: sigue en el árbol y sin guardar.</summary>
    [AvaloniaFact]
    public async Task Una_pestana_cerrada_sigue_contando_al_salir()
    {
        var dialogs = new TestDialogService { ChooseAnswer = false };
        var main = new MainWindowViewModel(dialogs);

        MapEditorViewModel map = MapWithSomethingPainted(main);

        main.CloseTabCommand.Execute(map);

        Assert.DoesNotContain(map, main.Tabs);
        Assert.Contains(map, main.UnsavedDocuments());

        await main.ConfirmExitAsync();

        Assert.Equal(1, dialogs.ChooseCalls);
    }

    /// <summary>
    /// Deshacer hasta el principio deja el mapa como estaba, así que no hay nada que
    /// perder aunque la pestaña siga marcada: el aviso mira el contenido, no la marca.
    /// </summary>
    [AvaloniaFact]
    public async Task Deshacer_hasta_el_principio_no_deja_nada_que_perder()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        MapEditorViewModel map = MapWithSomethingPainted(main);

        map.UndoCommand.Execute(null);

        Assert.True(map.IsModified);
        Assert.False(map.HasUnsavedChanges());
        Assert.True(await main.ConfirmExitAsync());
        Assert.Equal(0, dialogs.ChooseCalls);
    }

    // ------------------------------------------------------------------ la ventana

    [AvaloniaFact]
    public void Cerrar_la_ventana_con_algo_sin_guardar_y_cancelar_la_deja_abierta()
    {
        var main = new MainWindowViewModel(new TestDialogService { ChooseAnswer = null });
        MapWithSomethingPainted(main);

        var window = new MainWindow { DataContext = main };

        window.Show();
        Pump();

        window.Close();
        Pump();

        Assert.True(window.IsVisible);

        // Y ahora se cierra de verdad: una ventana que se queda abierta se queda abierta
        // para todo el proceso, y las pruebas que vinieran detrás la arrastrarían.
        foreach (PanelBaseViewModel document in main.UnsavedDocuments())
            document.MarkClean();

        Close(window);
    }

    [AvaloniaFact]
    public void Cerrar_la_ventana_sin_nada_pendiente_la_cierra()
    {
        var main = new MainWindowViewModel(new TestDialogService { ChooseAnswer = null });
        main.OpenTileSet(new TileSet("Bosque")).MarkClean();

        var window = new MainWindow { DataContext = main };

        window.Show();
        Pump();

        window.Close();
        Pump();

        Assert.False(window.IsVisible);
    }

    // ------------------------------------------------------------------ utilidades

    /// <summary>Un mapa con un tile puesto, por el formulario que usa el usuario.</summary>
    private static MapEditorViewModel MapWithSomethingPainted(MainWindowViewModel main)
    {
        if (main.TileSets.Count == 0)
            main.OpenTileSet(new TileSet("Bosque")).MarkClean();

        MapEditorViewModel map = NewMap(main, "Nivel 1");

        map.PickTile(TilePatch.Single(3), "Tile 3");
        map.Paint(1, 1);

        return map;
    }

    private static MapEditorViewModel NewMap(MainWindowViewModel main, string name)
    {
        main.AddMapCommand.Execute(null);

        var form = (EditMapViewModel)main.RightPanViewModel!;
        form.Name = name;
        form.AcceptMapCommand.Execute(null);

        return main.Tabs.OfType<MapEditorViewModel>().Last();
    }

    /// <summary>Lo que se lee en la cabecera de una pestaña del centro.</summary>
    private static string TabHeader(Window window, int index)
    {
        TabControl tabs = window.GetVisualDescendants().OfType<TabControl>().First();
        Control container = tabs.ContainerFromIndex(index)!;

        return container.GetVisualDescendants().OfType<TextBlock>().First().Text ?? string.Empty;
    }

    /// <summary>
    /// Cierra de verdad. Hacen falta dos vueltas: la primera para el cierre y deja la
    /// pregunta en la cola, y la segunda la atiende y ya cierra.
    /// </summary>
    private static void Close(Window window)
    {
        window.Close();
        Pump();
        Pump();
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
