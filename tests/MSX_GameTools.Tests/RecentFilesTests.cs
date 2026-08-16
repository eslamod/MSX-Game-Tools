using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// La lista de lo último abierto: qué entra, en qué orden y qué sobrevive a cerrar.
/// </summary>
/// <remarks>
/// Nace de tener que cargar el mismo proyecto veinte veces seguidas mientras se prueba
/// algo. Por eso lo que importa no es sólo que guarde, sino que guarde <em>ya</em> y que no
/// se llene de repetidos: una lista de diez con el mismo fichero ocho veces no ahorra nada.
/// </remarks>
public class RecentFilesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxrec-{Guid.NewGuid():N}");

    public RecentFilesTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private SettingsStore Store => new(_folder);

    [Fact]
    public void El_ultimo_abierto_va_primero()
    {
        var recent = new RecentFiles();

        recent.Add(File("uno.msxtiles"), RecentKind.TileSet);
        recent.Add(File("dos.msxmap"), RecentKind.Map);

        Assert.Equal(["dos.msxmap", "uno.msxtiles"], recent.Items.Select(item => item.Name));
    }

    /// <summary>
    /// Volver a abrir algo que ya estaba lo sube, no lo duplica.
    /// </summary>
    /// <remarks>
    /// Es lo que hace útil una lista corta. Quien está probando abre los mismos tres
    /// ficheros una y otra vez; sin esto, a la cuarta vuelta la lista son diez copias de lo
    /// mismo y lo que abriste ayer ya no está.
    /// </remarks>
    [Fact]
    public void Repetir_sube_en_vez_de_duplicar()
    {
        var recent = new RecentFiles();

        recent.Add(File("uno.msxtiles"), RecentKind.TileSet);
        recent.Add(File("dos.msxmap"), RecentKind.Map);
        recent.Add(File("uno.msxtiles"), RecentKind.TileSet);

        Assert.Equal(2, recent.Items.Count);
        Assert.Equal("uno.msxtiles", recent.Items[0].Name);
    }

    /// <summary>Dos formas de nombrar el mismo fichero son el mismo fichero.</summary>
    /// <remarks>
    /// En Windows el disco no distingue mayúsculas, así que abrir por el selector y abrir
    /// escribiendo la carpeta a mano dan la misma cosa con otra cara.
    /// </remarks>
    [Fact]
    public void La_misma_ruta_escrita_de_otra_forma_no_se_repite()
    {
        var recent = new RecentFiles();

        recent.Add(File("uno.msxtiles"), RecentKind.TileSet);
        recent.Add(File("UNO.MSXTILES"), RecentKind.TileSet);
        recent.Add(Path.Combine(_folder, ".", "uno.msxtiles"), RecentKind.TileSet);

        Assert.Single(recent.Items);
    }

    [Fact]
    public void Pasado_el_tope_se_cae_el_mas_viejo()
    {
        var recent = new RecentFiles();

        foreach (int number in Enumerable.Range(1, RecentFiles.Capacity + 3))
            recent.Add(File($"m{number}.msxmap"), RecentKind.Map);

        Assert.Equal(RecentFiles.Capacity, recent.Items.Count);
        Assert.Equal("m13.msxmap", recent.Items[0].Name);
        Assert.DoesNotContain(recent.Items, item => item.Name == "m1.msxmap");
    }

    /// <summary>
    /// Lo que viene del fichero de ajustes pasa por las mismas reglas.
    /// </summary>
    /// <remarks>
    /// Un settings.json se puede editar a mano, o venir de una versión que guardaba de otra
    /// forma. Si se copiara tal cual, un fichero con veinte entradas repetidas dejaría el
    /// menú igual de inservible que si no hubiera reglas.
    /// </remarks>
    [Fact]
    public void Lo_guardado_se_limpia_al_cargarlo()
    {
        var recent = new RecentFiles();

        recent.Reset([
            new RecentItem(File("uno.msxtiles"), RecentKind.TileSet),
            new RecentItem(File("uno.msxtiles"), RecentKind.TileSet),
            new RecentItem(File("dos.msxmap"), RecentKind.Map),
        ]);

        Assert.Equal(2, recent.Items.Count);
        Assert.Equal("uno.msxtiles", recent.Items[0].Name);
    }

    [Fact]
    public void Los_recientes_van_y_vuelven_por_el_fichero()
    {
        var recent = new RecentFiles();

        recent.Add(File("uno.msxtiles"), RecentKind.TileSet);
        recent.Add(File("dos.msxpal"), RecentKind.Palette);

        Assert.True(Store.Save(new Settings("es", new EditorPreferences(), [.. recent.Items])));

        Settings read = Store.Load();

        Assert.Equal(2, read.Recent.Count);
        Assert.Equal("dos.msxpal", read.Recent[0].Name);
        Assert.Equal(RecentKind.Palette, read.Recent[0].Kind);
    }

    /// <summary>
    /// Un fichero de ajustes de antes de los recientes sigue cargando.
    /// </summary>
    /// <remarks>
    /// El campo se añadió sin subir la versión del formato, así que lo escrito antes no lo
    /// trae. Perder el idioma y el zoom por eso sería cambiar una comodidad nueva por dos
    /// ajustes que ya funcionaban.
    /// </remarks>
    [Fact]
    public void Unos_ajustes_de_antes_de_los_recientes_siguen_valiendo()
    {
        System.IO.File.WriteAllText(
            Store.Path,
            """{ "version": 1, "language": "ca", "zoom": { "mapTileZoom": 4 } }""");

        Settings read = Store.Load();

        Assert.Equal("ca", read.Language);
        Assert.Equal(4, read.Preferences.MapTileZoom);
        Assert.Empty(read.Recent);
    }

    /// <summary>Abrir un fichero lo apunta, y con lo que es.</summary>
    [AvaloniaFact]
    public async Task Abrir_un_documento_lo_apunta()
    {
        string path = File("bosque.msxtiles");
        System.IO.File.WriteAllText(path, TileSetSerializer.Serialize(new TileSet("Bosque"), ColorPalette.CreateMsxStandard()));

        var dialogs = new TestDialogService { OpenPath = path };
        var main = new MainWindowViewModel(dialogs, Store);

        await main.OpenCommand.ExecuteAsync(null);

        RecentItem only = Assert.Single(main.Recent.Items);

        Assert.Equal("bosque.msxtiles", only.Name);
        Assert.Equal(RecentKind.TileSet, only.Kind);
    }

    /// <summary>
    /// Lo apuntado se escribe en el acto, sin esperar a cerrar la ventana.
    /// </summary>
    /// <remarks>
    /// Es el caso para el que se hizo esto: sesiones de probar y volver a cargar, que
    /// acaban a lo bruto. Guardando sólo al salir por la puerta, se perdería justo cuando
    /// hace falta.
    /// </remarks>
    [AvaloniaFact]
    public async Task Lo_apuntado_se_escribe_sin_esperar_a_cerrar()
    {
        string path = File("bosque.msxtiles");
        System.IO.File.WriteAllText(path, TileSetSerializer.Serialize(new TileSet("Bosque"), ColorPalette.CreateMsxStandard()));

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = path }, Store);

        await main.OpenCommand.ExecuteAsync(null);

        // Sin SaveSettings ni cerrar nada: se lee el fichero tal y como está ahora mismo.
        Assert.Single(Store.Load().Recent);
    }

    /// <summary>Un reciente se vuelve a abrir por el mismo camino que uno a mano.</summary>
    [AvaloniaFact]
    public async Task Un_reciente_se_vuelve_a_abrir()
    {
        string path = File("bosque.msxtiles");
        System.IO.File.WriteAllText(path, TileSetSerializer.Serialize(new TileSet("Bosque"), ColorPalette.CreateMsxStandard()));

        var main = new MainWindowViewModel(new TestDialogService(), Store);

        main.Recent.Add(path, RecentKind.TileSet);

        await main.OpenRecentCommand.ExecuteAsync(main.Recent.Items[0]);

        // Que sea un editor de tiles y no otra cosa: se comprueba que llegó al mismo sitio
        // que habría llegado abriéndolo a mano, no sólo que se abrió algo.
        PanelBaseViewModel opened = Assert.Single(main.Tabs);

        Assert.IsType<TileSetEditorViewModel>(opened);
        Assert.StartsWith("Bosque", opened.Header);
    }

    /// <summary>
    /// Un reciente que ya no está se dice y se quita.
    /// </summary>
    /// <remarks>
    /// Dejarlo puesto sólo sirve para que vuelva a fallar mañana. Y lo normal no es que se
    /// haya roto nada: es que se movió o se renombró.
    /// </remarks>
    [AvaloniaFact]
    public async Task Un_reciente_que_ya_no_esta_se_quita_de_la_lista()
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs, Store);

        main.Recent.Add(File("volado.msxmap"), RecentKind.Map);

        await main.OpenRecentCommand.ExecuteAsync(main.Recent.Items[0]);

        Assert.Empty(main.Recent.Items);
        Assert.Contains(dialogs.Messages, message => message.Contains("volado.msxmap"));
        Assert.Empty(main.Tabs);
    }

    /// <summary>
    /// Abrir un proyecto apunta el proyecto, no las veinte piezas que trae.
    /// </summary>
    /// <remarks>
    /// Quien abre un proyecto ha abierto una cosa. Apuntar cada uno de sus documentos
    /// llenaría el tope de diez de una sola vez y borraría todo lo demás.
    /// </remarks>
    [AvaloniaFact]
    public async Task Un_proyecto_se_apunta_entero_y_no_por_piezas()
    {
        string tiles = File("bosque.msxtiles");
        System.IO.File.WriteAllText(tiles, TileSetSerializer.Serialize(new TileSet("Bosque"), ColorPalette.CreateMsxStandard()));

        string project = File("juego.msxproj");
        System.IO.File.WriteAllText(project, ProjectSerializer.Serialize(new Project(
            "Juego",
            [new ProjectItem(ProjectItemKind.TileSet, "bosque.msxtiles")],
            [],
            [])));

        var main = new MainWindowViewModel(new TestDialogService { OpenPath = project }, Store);

        await main.OpenProjectCommand.ExecuteAsync(null);

        RecentItem only = Assert.Single(main.Recent.Items);

        Assert.Equal("juego.msxproj", only.Name);
        Assert.Equal(RecentKind.Project, only.Kind);
    }

    /// <summary>
    /// Las entradas del menú llevan de verdad su comando y su fichero.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es la mitad que no se ve desde el ViewModel. Las entradas salen de una lista, así
    /// que su comando no se escribe en cada una: se pone una vez en el tema del contenedor
    /// y desde allí hay que subir hasta el DataContext de la ventana, atravesando el
    /// desplegable. Si ese enlace no resuelve, el menú aparece entero y con sus nombres, y
    /// al pulsar no pasa nada.
    /// </para>
    /// <para>
    /// Por eso se abre el menú de verdad en vez de mirar la plantilla: las entradas no
    /// existen hasta que se abre.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Las_entradas_del_menu_llevan_su_comando()
    {
        var main = new MainWindowViewModel(new TestDialogService(), Store);

        main.Recent.Add(File("uno.msxtiles"), RecentKind.TileSet);

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 720 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        MenuItem file = Find(window, "MenuFile");

        file.Open();
        Dispatcher.UIThread.RunJobs();

        MenuItem recent = Find(file, "MenuRecent");

        recent.Open();
        Dispatcher.UIThread.RunJobs();

        MenuItem entry = Assert.Single(recent.GetLogicalDescendants().OfType<MenuItem>());

        Assert.Equal("uno.msxtiles", entry.Header);

        Assert.True(
            entry.Command is not null,
            "La entrada del reciente no tiene comando: el menú saldría bien y no haría nada.");

        Assert.Same(main.Recent.Items[0], entry.CommandParameter);
        Assert.True(entry.Command!.CanExecute(entry.CommandParameter));

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>La entrada de menú que lleva ese texto, en el idioma que esté puesto.</summary>
    private static MenuItem Find(ILogical root, string key) =>
        root.GetLogicalDescendants()
            .OfType<MenuItem>()
            .First(item => (item.Header as string) == Localizer.Instance[key]);

    private string File(string name) => Path.Combine(_folder, name);
}
