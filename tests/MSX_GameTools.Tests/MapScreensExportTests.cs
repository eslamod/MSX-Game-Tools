using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Exportar el mapa partido en las pantallas del juego.
/// </summary>
/// <remarks>
/// Un juego de pantallas fijas dibuja el mapa entero y luego carga una pantalla cada vez que se
/// cruza una puerta. Lo que se comprueba aquí es lo que no se ve leyendo: por dónde corta, qué
/// pantallas no escribe, con qué rellena la del borde y qué se dice al acabar.
/// </remarks>
public class MapScreensExportTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), $"msxscreens-{Guid.NewGuid():N}");

    public MapScreensExportTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static Localizer Text => Localizer.Instance;

    [AvaloniaFact]
    public async Task Sale_un_fichero_por_pantalla_con_su_columna_y_su_fila()
    {
        Opened opened = Open(64, 48, painted: [(0, 0), (32, 0), (0, 24), (32, 24)]);

        ByScreens(opened.Form, ExportFormat.Binary);

        // Por orden de lectura, y columna antes que fila: la que el editor llama «2-1» es la
        // que acaba en _2_1.
        Assert.Equal(
            (string[])["nivel_1_1_1.bin", "nivel_1_2_1.bin", "nivel_1_1_2.bin", "nivel_1_2_2.bin"],
            opened.Form.Files.Select(file => file.Name));

        await AcceptAsync(opened.Form);

        Assert.Equal(
            (string[])["nivel_1_1_1.bin", "nivel_1_1_2.bin", "nivel_1_2_1.bin", "nivel_1_2_2.bin"],
            Directory.GetFiles(_folder).Select(Path.GetFileName).Order());
    }

    /// <summary>
    /// Las pantallas sin nada dibujado no llegan a fichero.
    /// </summary>
    /// <remarks>
    /// En un mapa de pantallas fijas lo normal es que el rectángulo no esté entero —una L, una
    /// cruz, un castillo con sus alas—, y un fichero de 768 ceros por cada hueco del dibujo no
    /// es un mapa, es sitio gastado. Las que sí salen conservan su número.
    /// </remarks>
    [AvaloniaFact]
    public async Task Las_pantallas_sin_nada_dibujado_no_se_escriben()
    {
        Opened opened = Open(64, 48, painted: [(32, 24)]);

        ByScreens(opened.Form, ExportFormat.Binary);
        await AcceptAsync(opened.Form);

        Assert.Equal(
            (string[])["nivel_1_2_2.bin"],
            Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    /// <summary>
    /// La pantalla del borde sale entera, con el tile de relleno donde el mapa ya no llega.
    /// </summary>
    /// <remarks>
    /// Todas las pantallas del juego miden lo mismo, así que el cargador lee siempre el mismo
    /// número de bytes: una pantalla corta le dejaría el resto de la pantalla con lo que
    /// hubiera antes.
    /// </remarks>
    [AvaloniaFact]
    public async Task La_pantalla_del_borde_se_completa_con_el_tile_de_relleno()
    {
        Opened opened = Open(40, 24, painted: [(0, 0), (32, 0)]);

        opened.Map.EmptyTile = 3;

        ByScreens(opened.Form, ExportFormat.Binary);
        await AcceptAsync(opened.Form);

        byte[] edge = await File.ReadAllBytesAsync(Path.Combine(_folder, "nivel_1_2_1.bin"));

        Assert.Equal(32 * 24, edge.Length);

        // Ocho columnas de mapa y las otras veinticuatro de relleno.
        Assert.Equal(7, edge[0]);
        Assert.Equal(3, edge[8]);
        Assert.Equal(3, edge[^1]);
    }

    /// <summary>La cabecera es opcional: todas las pantallas miden lo mismo.</summary>
    [AvaloniaTheory]
    [InlineData(false, 32 * 24)]
    [InlineData(true, (32 * 24) + 4)]
    public async Task La_cabecera_de_cada_pantalla_se_pone_o_no_se_pone(bool header, int bytes)
    {
        Opened opened = Open(32, 24, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary, header);
        await AcceptAsync(opened.Form);

        byte[] screen = await File.ReadAllBytesAsync(Path.Combine(_folder, "nivel_1_1_1.bin"));

        Assert.Equal(bytes, screen.Length);

        if (header)
            Assert.Equal((byte[])[32, 0, 24, 0], screen[..4]);
    }

    /// <summary>
    /// Cada pantalla lleva escrito de qué mapa y de qué trozo salió.
    /// </summary>
    /// <remarks>
    /// Con veinte ficheros en la misma carpeta, el nombre es lo único que queda para saber cuál
    /// es cuál. Y la etiqueta lleva el número dentro, así que dos pantallas se pueden ensamblar
    /// juntas sin chocar.
    /// </remarks>
    [AvaloniaFact]
    public async Task Cada_pantalla_dice_de_que_mapa_y_de_que_trozo_salio()
    {
        Opened opened = Open(64, 48, painted: [(32, 0)]);

        ByScreens(opened.Form, ExportFormat.Assembler);
        await AcceptAsync(opened.Form);

        string screen = await File.ReadAllTextAsync(Path.Combine(_folder, "nivel_1_2_1.asm"));

        Assert.Contains("; Screen 2-1 of Nivel 1 - map columns 32-63, rows 0-23", screen);
        Assert.Contains("nivel_1_2_1_map:", screen);

        // Y sin cabecera, que no se ha pedido.
        Assert.DoesNotContain("; Header:", screen);
    }

    /// <summary>
    /// Una pantalla que parte un supertile por la mitad no deja exportar.
    /// </summary>
    /// <remarks>
    /// Sin decirlo, la lista de ficheros se quedaría vacía y sin explicación: se vería que no va
    /// a salir nada, pero no por qué. Y la respuesta —cambiar el tamaño de la pantalla— está en
    /// la configuración, que es otro panel.
    /// </remarks>
    [AvaloniaFact]
    public async Task Una_pantalla_que_parte_un_supertile_no_deja_exportar()
    {
        Opened opened = Open(32, 24, superTile: 2, painted: [(0, 0)]);

        opened.Main.Preferences.ScreenHeight = 21;

        ByScreens(opened.Form, ExportFormat.Binary);

        Assert.True(opened.Form.HasError);
        Assert.Empty(opened.Form.Files);

        await AcceptAsync(opened.Form);

        Assert.Empty(Directory.GetFiles(_folder));

        // Y el panel sigue abierto, que es lo que hace falta para arreglarlo.
        Assert.NotNull(opened.Main.RightPanViewModel);
    }

    /// <summary>
    /// Por pantallas no se ofrece la ROM de ejemplo.
    /// </summary>
    /// <remarks>
    /// La ROM carga un mapa y lo enseña; una carpeta de pantallas es otro programa —el que va
    /// cambiando de pantalla— y ése no está escrito. Ofrecerla sería una casilla que miente.
    /// </remarks>
    [AvaloniaFact]
    public void Por_pantallas_no_se_ofrece_la_rom_de_ejemplo()
    {
        Opened opened = Open(32, 24, painted: [(0, 0)]);

        opened.Form.Format = Choice(opened.Form, ExportFormat.Binary);

        Assert.True(opened.Form.ShowsExampleRom);

        opened.Form.ByScreens = true;

        Assert.False(opened.Form.ShowsExampleRom);
    }

    /// <summary>
    /// Marcar la casilla convierte el destino en la carpeta que lo contiene, y al revés.
    /// </summary>
    /// <remarks>
    /// Sin convertirlo, una carpeta elegida antes se leería como un fichero —la carpeta sería
    /// la de encima— y las pantallas acabarían un nivel más arriba de donde se dijo.
    /// </remarks>
    [AvaloniaFact]
    public void Marcar_la_casilla_convierte_el_destino_en_carpeta()
    {
        Opened opened = Open(32, 24, painted: [(0, 0)]);

        opened.Form.Format = Choice(opened.Form, ExportFormat.Binary);
        opened.Form.Destination = Path.Combine(_folder, "nivel_1.bin");

        opened.Form.ByScreens = true;

        Assert.Equal(_folder, opened.Form.Destination);
        Assert.Equal("nivel_1_1_1.bin", opened.Form.Files[0].Name);

        opened.Form.ByScreens = false;

        Assert.Equal(Path.Combine(_folder, "nivel_1.bin"), opened.Form.Destination);
        Assert.Equal("nivel_1.bin", opened.Form.Files[0].Name);
    }

    /// <summary>Con csv no hay pantallas que ofrecer, y la casilla puesta se cae.</summary>
    [AvaloniaFact]
    public void Cambiar_a_csv_quita_la_casilla_de_las_pantallas()
    {
        Opened opened = Open(32, 24, painted: [(0, 0)]);

        opened.Form.ByScreens = true;

        Assert.True(opened.Form.ShowsScreens);

        opened.Form.Format = Choice(opened.Form, ExportFormat.Csv);

        Assert.False(opened.Form.ShowsScreens);
        Assert.False(opened.Form.ByScreens);
        Assert.Equal("nivel_1.csv", opened.Form.Files[0].Name);
    }

    /// <summary>
    /// Con muchas pantallas la lista se resume.
    /// </summary>
    /// <remarks>
    /// Trece nombres ya no se leen de un vistazo y cien menos. Lo que hace falta saber —cómo se
    /// llaman y cuántos hay— sigue estando.
    /// </remarks>
    [AvaloniaFact]
    public async Task Con_muchas_pantallas_la_lista_se_resume()
    {
        (int Column, int Row)[] one = [.. Enumerable.Range(0, 13).Select(screen => (screen * 32, 0))];

        Opened opened = Open(13 * 32, 24, painted: one);

        ByScreens(opened.Form, ExportFormat.Binary);

        Assert.Equal(12, opened.Form.Files.Count);
        Assert.Equal(Text.Format("ExportMoreFiles", 1), opened.Form.More);

        await AcceptAsync(opened.Form);

        Assert.Equal(13, Directory.GetFiles(_folder).Length);
    }

    /// <summary>
    /// Al acabar se dice cuántas han salido, cuántas no y con qué se ha rellenado.
    /// </summary>
    /// <remarks>
    /// Las dos cosas que no se ven mirando la carpeta: que faltan ficheros a propósito y que las
    /// pantallas del borde llevan relleno que no estaba en el mapa.
    /// </remarks>
    [AvaloniaFact]
    public async Task Al_acabar_dice_cuantas_han_salido_y_que_las_demas_estaban_vacias()
    {
        Opened opened = Open(40, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary);
        await AcceptAsync(opened.Form);

        string said = Assert.Single(opened.Dialogs.Messages);

        Assert.Contains(Text.Format("ExportedScreensBody", 1, 4), said);
        Assert.Contains(Text["ExportedScreensSkipped"], said);
        Assert.Contains(Text.Format("ExportedScreensPadded", 0), said);
    }

    /// <summary>El botón de al lado pide una carpeta, que es lo que hace falta aquí.</summary>
    [AvaloniaFact]
    public async Task El_boton_de_al_lado_pide_una_carpeta_y_no_un_fichero()
    {
        Opened opened = Open(32, 24, painted: [(0, 0)]);

        opened.Dialogs.FolderPath = _folder;

        ByScreens(opened.Form, ExportFormat.Binary);

        await opened.Form.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(1, opened.Dialogs.FolderCalls);
        Assert.Equal(0, opened.Dialogs.SaveCalls);
        Assert.Equal(_folder, opened.Form.Destination);
    }

    // ------------------------------------------------------------------ una sola pantalla

    /// <summary>
    /// Se puede pedir una pantalla por su número y sale sólo ésa.
    /// </summary>
    /// <remarks>
    /// Es para cuando se toca una habitación y hay que volver a escribirla: las otras veinte ya
    /// están, y reescribirlas todas convierte el cambio de una en veinte ficheros con fecha
    /// nueva.
    /// </remarks>
    [AvaloniaFact]
    public async Task Se_puede_pedir_una_sola_pantalla_por_su_numero()
    {
        Opened opened = Open(64, 48, painted: [(0, 0), (32, 0), (0, 24), (32, 24)]);

        ByScreens(opened.Form, ExportFormat.Binary);
        Pick(opened.Form, 2, 1);

        Assert.Equal((string[])["nivel_1_2_1.bin"], opened.Form.Files.Select(file => file.Name));

        await AcceptAsync(opened.Form);

        Assert.Equal(
            (string[])["nivel_1_2_1.bin"],
            Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    /// <summary>
    /// Y una vacía pedida por su número sí se escribe.
    /// </summary>
    /// <remarks>
    /// Saltarse las vacías vale para la tanda entera, donde son los huecos del dibujo. Pedir una
    /// por su número es pedir ésa, y una vacía a propósito —un sótano que el juego rellena al
    /// entrar— está tan pedida como las demás.
    /// </remarks>
    [AvaloniaFact]
    public async Task Una_pantalla_vacia_pedida_por_su_numero_si_se_escribe()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        opened.Map.EmptyTile = 3;

        ByScreens(opened.Form, ExportFormat.Binary);
        Pick(opened.Form, 2, 2);

        await AcceptAsync(opened.Form);

        byte[] screen = await File.ReadAllBytesAsync(Path.Combine(_folder, "nivel_1_2_2.bin"));

        Assert.Equal(32 * 24, screen.Length);
        Assert.All(screen, one => Assert.Equal(3, one));
    }

    /// <summary>Una pantalla que no existe se dice, en vez de no escribir nada sin explicar.</summary>
    [AvaloniaFact]
    public async Task Una_pantalla_que_no_existe_no_deja_exportar()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary);
        Pick(opened.Form, 5, 1);

        Assert.Equal(Text.Format("ExportScreenMissing", new ScreenNumber(5, 1), 2, 2),
            opened.Form.ErrorMessage);

        Assert.Empty(opened.Form.Files);

        await AcceptAsync(opened.Form);

        Assert.Empty(Directory.GetFiles(_folder));
    }

    /// <summary>
    /// El panel arranca por la pantalla de lo que estuviera marcado.
    /// </summary>
    /// <remarks>
    /// Lo marcado es lo último que se dijo a propósito sobre dónde se estaba trabajando y sigue
    /// ahí al abrir el panel; el ratón no, que se queda por donde saliera del lienzo camino del
    /// menú.
    /// </remarks>
    [AvaloniaFact]
    public void El_panel_arranca_por_la_pantalla_de_lo_marcado()
    {
        Opened opened = Open(96, 48, selected: (70, 30), painted: [(0, 0)]);

        Assert.Equal(3, opened.Form.ScreenColumn);
        Assert.Equal(2, opened.Form.ScreenRow);
    }

    /// <summary>Y sin nada marcado, por la primera.</summary>
    [AvaloniaFact]
    public void Sin_nada_marcado_el_panel_arranca_por_la_primera()
    {
        Opened opened = Open(96, 48, painted: [(0, 0)]);

        Assert.Equal(1, opened.Form.ScreenColumn);
        Assert.Equal(1, opened.Form.ScreenRow);
    }

    /// <summary>
    /// De una sola se dice cuál ha salido, y si llevaba relleno.
    /// </summary>
    /// <remarks>
    /// Cuántas han salido no hace falta decirlo —una, la pedida— ni que falten las vacías, que
    /// aquí no se ha saltado ninguna.
    /// </remarks>
    [AvaloniaFact]
    public async Task De_una_sola_pantalla_se_dice_cual_ha_salido_y_si_lleva_relleno()
    {
        Opened opened = Open(40, 24, painted: [(0, 0), (32, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary);
        Pick(opened.Form, 2, 1);

        await AcceptAsync(opened.Form);

        string said = Assert.Single(opened.Dialogs.Messages);

        Assert.Contains(Text.Format("ExportedOneScreenBody", new ScreenNumber(2, 1)), said);
        Assert.Contains(Text.Format("ExportedScreensPadded", 0), said);
        Assert.DoesNotContain(Text["ExportedScreensSkipped"], said);
    }

    /// <summary>La del borde pedida suelta también sale entera.</summary>
    [AvaloniaFact]
    public async Task La_pantalla_del_borde_pedida_suelta_tambien_se_rellena()
    {
        Opened opened = Open(40, 24, painted: [(32, 0)]);

        opened.Map.EmptyTile = 3;

        ByScreens(opened.Form, ExportFormat.Binary);
        Pick(opened.Form, 2, 1);

        await AcceptAsync(opened.Form);

        byte[] edge = await File.ReadAllBytesAsync(Path.Combine(_folder, "nivel_1_2_1.bin"));

        Assert.Equal(32 * 24, edge.Length);
        Assert.Equal(7, edge[0]);
        Assert.Equal(3, edge[8]);
    }

    /// <summary>
    /// Los dos botones del par, pulsados de verdad.
    /// </summary>
    /// <remarks>
    /// Un grupo de radios escribe un <c>false</c> de vuelta en el que desmarca, así que con el
    /// modelo de vista suelto los dos se ven bien y montados se pisan el uno al otro.
    /// </remarks>
    [AvaloniaFact]
    public void Los_dos_botones_del_par_eligen_todas_o_una()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary);

        var view = new ExportView { DataContext = opened.Form };
        var window = new Window { Content = view, Width = 420, Height = 700 };

        window.Show();
        Pump();

        RadioButton all = Radio(view, Text["ExportAllScreens"]);
        RadioButton one = Radio(view, Text["ExportOneScreen"]);

        Assert.True(all.IsChecked);

        Click(window, one);

        Assert.True(opened.Form.OneScreen);
        Assert.True(opened.Form.ShowsScreenPick);
        Assert.False(all.IsChecked);

        Click(window, all);

        Assert.False(opened.Form.OneScreen);
        Assert.False(opened.Form.ShowsScreenPick);
        Assert.False(one.IsChecked);

        window.Close();
        Pump();
    }

    /// <summary>
    /// Y decir que van todas, sin la vista delante, quita la de una sola.
    /// </summary>
    /// <remarks>
    /// Montado no hace falta: el grupo desmarca el otro botón y es su enlace el que escribe el
    /// <c>false</c>. Pero la propiedad tiene que valerse sola, que quien la ponga desde fuera
    /// está diciendo «todas» y eso es lo que tiene que quedar.
    /// </remarks>
    [AvaloniaFact]
    public void Decir_que_van_todas_quita_la_de_una_sola()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        opened.Form.OneScreen = true;
        opened.Form.AllScreens = true;

        Assert.False(opened.Form.OneScreen);
        Assert.True(opened.Form.AllScreens);
    }

    // ------------------------------------------------------------------ señalada en el mapa

    /// <summary>
    /// La pantalla que se va a escribir se señala en el mapa.
    /// </summary>
    /// <remarks>
    /// Un número de pantalla no dice qué hay dentro. Señalarla en el mapa contesta las dos
    /// cosas a la vez —dónde cae y qué lleva— sin sacar a nadie del panel.
    /// </remarks>
    [AvaloniaFact]
    public void La_pantalla_pedida_se_senala_en_el_mapa()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary);

        Assert.Null(opened.Editor.ScreenPreview);

        Pick(opened.Form, 2, 1);

        Assert.Equal(new MapRegion(32, 0, 32, 24), opened.Editor.ScreenPreview);

        // Y sin tocar lo que hubiera marcado a mano, que es una herramienta y no un adorno.
        Assert.Null(opened.Editor.Selection);
    }

    /// <summary>Volver a todas la quita: ya no hay una pantalla de la que hablar.</summary>
    [AvaloniaFact]
    public void Volver_a_todas_quita_la_senal()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary);
        Pick(opened.Form, 2, 1);

        opened.Form.AllScreens = true;

        Assert.Null(opened.Editor.ScreenPreview);
    }

    /// <summary>Y una que no existe no se señala, que no hay dónde.</summary>
    [AvaloniaFact]
    public void Una_pantalla_que_no_existe_no_se_senala()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary);
        Pick(opened.Form, 5, 1);

        Assert.Null(opened.Editor.ScreenPreview);
    }

    /// <summary>
    /// Cerrar el panel quita la señal.
    /// </summary>
    /// <remarks>
    /// Por cualquiera de las salidas, no sólo por Cancelar: el panel también se va cuando se
    /// abre otro encima, y la señal se quedaría puesta sin nadie que la fuera a escribir.
    /// </remarks>
    [AvaloniaFact]
    public void Cerrar_el_panel_quita_la_senal()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary);
        Pick(opened.Form, 2, 1);

        opened.Form.CancelExportCommand.Execute(null);

        Assert.Null(opened.Editor.ScreenPreview);
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un mapa abierto con su panel de exportar, que es por donde se pasa.</summary>
    private sealed record Opened(
        MainWindowViewModel Main,
        TestDialogService Dialogs,
        TileMap Map,
        MapEditorViewModel Editor,
        ExportViewModel Form);

    /// <param name="superTile">El lado del supertile, o 0 para un mapa de tiles sueltos.</param>
    /// <param name="selected">La celda que queda marcada antes de abrir el panel, si alguna.</param>
    /// <param name="painted">Las celdas que llevan tile, que son las que hacen que una pantalla salga.</param>
    private Opened Open(
        int width,
        int height,
        int superTile = 0,
        (int Column, int Row)? selected = null,
        params (int Column, int Row)[] painted)
    {
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);
        var tileSet = new TileSet("Bosque");

        if (superTile > 0)
            tileSet.UseSuperTiles(superTile, superTile);

        TileSetEditorViewModel tiles = main.OpenTileSet(tileSet);

        var map = new TileMap("Nivel 1", width, height);

        foreach ((int column, int row) in painted)
            map.Layers[0].Grid[column, row] = 7;

        MapEditorViewModel editor = main.OpenMap(map, tiles);

        if (selected is { } cell)
            editor.Select(cell.Column, cell.Row, cell.Column, cell.Row);

        main.ExportMapCommand.Execute(null);

        return new Opened(main, dialogs, map, editor, (ExportViewModel)main.RightPanViewModel!);
    }

    private static RadioButton Radio(Visual root, string content) =>
        root.GetVisualDescendants()
            .OfType<RadioButton>()
            .Single(button => (string?)button.Content == content);

    private static void Click(Window window, Visual target)
    {
        Point centre = target.TranslatePoint(
            new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

        window.MouseDown(centre, MouseButton.Left);
        Pump();
        window.MouseUp(centre, MouseButton.Left);
        Pump();
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    private static ExportChoice Choice(ExportViewModel form, ExportFormat format) =>
        form.Formats.Single(choice => choice.Format == format);

    /// <summary>El formato y la casilla, en ese orden: el formato decide si la casilla sale.</summary>
    private static void ByScreens(ExportViewModel form, ExportFormat format, bool header = false)
    {
        form.Format = Choice(form, format);
        form.ByScreens = true;
        form.ScreenHeader = header;
    }

    /// <summary>Que va una sola, y cuál: columna y fila, contando desde uno.</summary>
    private static void Pick(ExportViewModel form, int column, int row)
    {
        form.OneScreen = true;
        form.ScreenColumn = column;
        form.ScreenRow = row;
    }

    /// <summary>La carpeta se elige después de la casilla, que es lo que la hace carpeta.</summary>
    private async Task AcceptAsync(ExportViewModel form)
    {
        form.Destination = _folder;

        await form.AcceptExportCommand.ExecuteAsync(null);
    }
}
