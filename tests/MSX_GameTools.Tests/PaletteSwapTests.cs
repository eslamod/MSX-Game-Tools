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
/// Intercambiar colores de índice en la paleta y reajustar lo que estaba pintado con
/// ellos, para que se siga viendo igual.
/// </summary>
public class PaletteSwapTests
{
    // ------------------------------------------------------------------ la permutación

    [AvaloniaFact]
    public void Un_intercambio_lleva_cada_color_al_indice_del_otro()
    {
        var swaps = new PaletteSwaps();

        swaps.Swap(3, 7);

        int[] table = swaps.Table();

        Assert.Equal(7, table[3]);
        Assert.Equal(3, table[7]);
        Assert.Equal(5, table[5]);
    }

    /// <summary>
    /// Tres intercambios encadenados, que es donde se ve si la cuenta está bien.
    /// </summary>
    /// <remarks>
    /// Aplicar los intercambios de uno en uno sobre los dibujos da mal: el primero manda
    /// al 2 lo que estaba en el 1, y el segundo se lo vuelve a llevar. Por eso se guarda
    /// la permutación y se hace una sola pasada.
    /// </remarks>
    [AvaloniaFact]
    public void Una_cadena_de_intercambios_no_manda_un_color_a_dos_sitios()
    {
        var swaps = new PaletteSwaps();

        swaps.Swap(1, 2);
        swaps.Swap(2, 3);

        int[] table = swaps.Table();

        // El 1 ha acabado en el 3, el 2 en el 1 y el 3 en el 2.
        Assert.Equal(3, table[1]);
        Assert.Equal(1, table[2]);
        Assert.Equal(2, table[3]);

        // Y sigue siendo una permutación: los 16 destinos, cada uno una vez.
        Assert.Equal(16, table.Distinct().Count());
    }

    [AvaloniaFact]
    public void Sin_tocar_nada_no_hay_intercambios()
    {
        var swaps = new PaletteSwaps();

        Assert.True(swaps.IsEmpty);

        swaps.Swap(4, 9);

        Assert.False(swaps.IsEmpty);
    }

    // ------------------------------------------------------------------ la paleta

    [AvaloniaFact]
    public void Intercambiar_mueve_los_colores_pero_no_los_indices()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard().Clone("Mía");

        string wasThree = palette[3].HexRgb;
        string wasTen = palette[10].HexRgb;

        Assert.True(palette.Swap(3, 10));

        Assert.Equal(wasTen, palette[3].HexRgb);
        Assert.Equal(wasThree, palette[10].HexRgb);

        // La entrada sigue siendo la suya: es la ranura, no el color, lo que lleva índice.
        Assert.Equal(3, palette[3].Index);
        Assert.Equal("3", palette[3].Hex);
        Assert.Equal("A", palette[10].Hex);
    }

    [AvaloniaFact]
    public void El_nombre_viaja_con_el_color()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard().Clone("Mía");

        palette[3].Name = "Hierba";
        palette[10].Name = "Arena";

        palette.Swap(3, 10);

        Assert.Equal("Arena", palette[3].Name);
        Assert.Equal("Hierba", palette[10].Name);
    }

    /// <summary>
    /// El nombre no pasa por un estado en blanco mientras se intercambia.
    /// </summary>
    /// <remarks>
    /// Cambiar una componente descarta el nombre heredado, porque deja de describir al
    /// color. En un intercambio esa regla no vale —el nombre viaja con su color— y, si se
    /// aplicaba, quedaba un instante con el color nuevo y el nombre vacío. Se mira en el
    /// aviso de la paleta y no al final, porque ése es el momento en el que todo lo que
    /// dibuja con ella se repinta: lo que se vea ahí es lo que llega a la pantalla.
    /// </remarks>
    [AvaloniaFact]
    public void El_nombre_nunca_se_queda_en_blanco_por_el_camino()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard().Clone("Mía");

        palette[3].Name = "Hierba";

        List<string> seen = [];
        palette.ColorsChanged += _ => seen.Add(palette[2].DisplayName);

        palette.Swap(2, 3);

        Assert.NotEmpty(seen);
        Assert.All(seen, name => Assert.Equal("Hierba", name));
    }

    [AvaloniaFact]
    public void El_transparente_no_se_mueve_de_sitio()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard().Clone("Mía");

        string wasFive = palette[5].HexRgb;

        Assert.False(palette.CanSwap(0));
        Assert.False(palette.Swap(0, 5));
        Assert.Equal(wasFive, palette[5].HexRgb);
    }

    [AvaloniaFact]
    public void En_la_paleta_del_MSX_no_se_intercambia_nada()
    {
        ColorPalette standard = ColorPalette.CreateMsxStandard();

        string wasThree = standard[3].HexRgb;

        Assert.False(standard.CanSwap(3));
        Assert.False(standard.Swap(3, 10));
        Assert.Equal(wasThree, standard[3].HexRgb);
    }

    // ------------------------------------------------------------------ el reajuste

    /// <summary>
    /// Lo que se le pide a todo esto: después de aplicar, el dibujo se ve igual que antes.
    /// </summary>
    /// <remarks>
    /// Se comprueba por el color de verdad y no por el índice: que el índice haya cambiado
    /// es el medio, no el fin. Y por el panel, que es por donde entra el usuario.
    /// </remarks>
    [AvaloniaFact]
    public async Task Despues_de_aplicar_el_tile_se_ve_igual_que_antes()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = true };
        var main = new MainWindowViewModel(dialogs);

        ColorPalette palette = main.Palettes.Add("Mía");
        var tileSet = new TileSet("Bosque");

        TileSetEditorViewModel editor = main.OpenTileSet(tileSet, palette);

        TileRow row = tileSet.ListOfTiles[0].ArrayTileRows[0];
        row.ForeColor = 3;
        row.BackColor = 10;

        Avalonia.Media.Color wasFore = palette.GetColor(row.ForeColor);
        Avalonia.Media.Color wasBack = palette.GetColor(row.BackColor);

        var panel = new EditPaletteViewModel(main, palette);

        Assert.True(panel.SwapColors(3, 10));
        Assert.True(panel.HasSwaps);

        await panel.ApplyCommand.ExecuteAsync(null);

        // Los índices se han movido...
        Assert.Equal(10, row.ForeColor);
        Assert.Equal(3, row.BackColor);

        // ...y por eso se sigue viendo lo mismo.
        Assert.Equal(wasFore, palette.GetColor(row.ForeColor));
        Assert.Equal(wasBack, palette.GetColor(row.BackColor));

        Assert.False(panel.HasSwaps);
        Assert.True(editor.IsModified);
    }

    /// <summary>
    /// Un banco de sprites que comparte paleta se ajusta también, aunque el que se esté
    /// mirando sea el juego de tiles.
    /// </summary>
    /// <remarks>
    /// Es el caso que se escapa: al abrir dos ficheros con la misma paleta,
    /// <c>PaletteLibrary.Adopt</c> les da el mismo objeto, así que mover un color afecta a
    /// los dos. Reajustar sólo el de delante dejaría el otro con los colores cambiados.
    /// </remarks>
    [AvaloniaFact]
    public async Task Un_banco_que_comparte_la_paleta_tambien_se_ajusta()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = true };
        var main = new MainWindowViewModel(dialogs);

        ColorPalette palette = main.Palettes.Add("Mía");

        main.OpenTileSet(new TileSet("Bosque"), palette);

        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        SpritesEditorViewModel sprites = main.OpenSpriteBank(bank, palette);

        bank.SpritesList[0].ArraySpriteRows[0].Color = 3;

        var panel = new EditPaletteViewModel(main, palette);
        panel.SwapColors(3, 10);

        await panel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(10, bank.SpritesList[0].ArraySpriteRows[0].Color);
        Assert.True(sprites.IsModified);

        // Y el mensaje dice a quién le va a tocar, que si no se aplica a ciegas.
        Assert.Contains("Bichos", dialogs.LastConfirmMessage);
        Assert.Contains("Bosque", dialogs.LastConfirmMessage);
    }

    /// <summary>
    /// Los miembros de un grupo llevan su propia tabla de colores, aparte de la del patrón.
    /// </summary>
    [AvaloniaFact]
    public async Task Los_grupos_de_sprites_se_ajustan_por_su_cuenta()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = true };
        var main = new MainWindowViewModel(dialogs);

        ColorPalette palette = main.Palettes.Add("Mía");

        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        main.OpenSpriteBank(bank, palette);

        SpriteGroup group = bank.NewGroup(0)!;
        group.Members[0].Rows[0].Color = 3;

        var panel = new EditPaletteViewModel(main, palette);
        panel.SwapColors(3, 10);

        await panel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(10, group.Members[0].Rows[0].Color);
    }

    [AvaloniaFact]
    public async Task El_fondo_del_mapa_se_ajusta()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = true };
        var main = new MainWindowViewModel(dialogs);

        ColorPalette palette = main.Palettes.Add("Mía");
        var tileSet = new TileSet("Bosque");

        TileSetEditorViewModel tiles = main.OpenTileSet(tileSet, palette);

        var map = new TileMap("Nivel 1", 16, 16) { BackgroundColorIndex = 3 };
        main.OpenMap(map, tiles);

        var panel = new EditPaletteViewModel(main, palette);
        panel.SwapColors(3, 10);

        await panel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(10, map.BackgroundColorIndex);
    }

    // ------------------------------------------------------------------ echarse atrás

    [AvaloniaFact]
    public async Task Si_no_se_confirma_no_se_toca_ningun_dibujo()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = false };
        var main = new MainWindowViewModel(dialogs);

        ColorPalette palette = main.Palettes.Add("Mía");
        var tileSet = new TileSet("Bosque");

        main.OpenTileSet(tileSet, palette);
        tileSet.ListOfTiles[0].ArrayTileRows[0].ForeColor = 3;

        var panel = new EditPaletteViewModel(main, palette);
        panel.SwapColors(3, 10);

        await panel.ApplyCommand.ExecuteAsync(null);

        // El dibujo, intacto; y los intercambios siguen pendientes.
        Assert.Equal(3, tileSet.ListOfTiles[0].ArrayTileRows[0].ForeColor);
        Assert.True(panel.HasSwaps);
    }

    [AvaloniaFact]
    public void Descartar_deja_la_paleta_como_estaba()
    {
        var main = new MainWindowViewModel(new TestDialogService());
        ColorPalette palette = main.Palettes.Add("Mía");

        string[] before = [.. palette.Colors.Select(color => color.HexRgb)];

        var panel = new EditPaletteViewModel(main, palette);

        panel.SwapColors(1, 2);
        panel.SwapColors(2, 3);
        panel.SwapColors(7, 15);

        panel.DiscardSwaps();

        string[] after = [.. palette.Colors.Select(color => color.HexRgb)];

        Assert.Equal(before, after);
        Assert.False(panel.HasSwaps);
    }

    // ------------------------------------------------------------------ el panel montado

    /// <summary>
    /// El botón de aplicar no está hasta que hay algo que aplicar.
    /// </summary>
    /// <remarks>
    /// Montado y por el ViewLocator, que es como lo ve el usuario: la visibilidad va por
    /// enlace, y comprobarla en el ViewModel no diría si el enlace está bien puesto.
    /// </remarks>
    [AvaloniaFact]
    public void El_boton_de_aplicar_sale_al_intercambiar()
    {
        var main = new MainWindowViewModel(new TestDialogService());
        TestPalette.Create(main);

        var panel = (EditPaletteViewModel)main.RightPanViewModel!;

        var window = new Window
        {
            Content = new ContentControl { Content = panel },
            Width = 360,
            Height = 700,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Button apply = window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content is string text && text == Localizer.Instance["PaletteApply"]);

        Assert.False(apply.IsVisible);

        panel.SwapColors(3, 10);
        Dispatcher.UIThread.RunJobs();

        bool showed = apply.IsVisible;

        panel.DiscardSwaps();
        Dispatcher.UIThread.RunJobs();

        bool hidAgain = !apply.IsVisible;

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(showed, "Tras intercambiar dos colores tendria que salir el boton de aplicar.");
        Assert.True(hidAgain, "Al deshacer los intercambios el boton tendria que irse.");
    }

    /// <summary>
    /// Pulsar sobre una fila de la lista tiene que llegar al panel para poder arrastrarla.
    /// </summary>
    /// <remarks>
    /// Esto es lo que estaba roto: el <c>ListBoxItem</c> marca el <c>PointerPressed</c>
    /// como manejado al seleccionar la fila, y un manejador puesto desde el XAML no recibe
    /// los eventos ya manejados. El arrastre no empezaba nunca. Con el ratón de verdad y
    /// no llamando al manejador a mano, que si no la prueba pasaría igual estando mal.
    /// </remarks>
    [AvaloniaFact]
    public void Pulsar_en_un_color_lo_deja_listo_para_arrastrar()
    {
        var main = new MainWindowViewModel(new TestDialogService());
        TestPalette.Create(main);

        var panel = (EditPaletteViewModel)main.RightPanViewModel!;

        var window = new Window
        {
            Content = new ContentControl { Content = panel },
            Width = 360,
            Height = 700,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        EditPaletteView view = window.GetVisualDescendants().OfType<EditPaletteView>().Single();
        ListBox list = view.GetVisualDescendants().OfType<ListBox>().Single();

        // La fila del color 5, por su contenedor: es donde pincharía el usuario.
        ListBoxItem row = list.GetRealizedContainers()
            .OfType<ListBoxItem>()
            .Single(item => item.DataContext is PaletteColor { Index: 5 });

        Point centre = row.TranslatePoint(
            new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;

        window.MouseDown(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        int? pressed = view.PressedIndex;

        window.MouseUp(centre, MouseButton.Left);
        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(5, pressed);
    }

    /// <summary>
    /// Deshacer los movimientos no se lleva por delante lo que se haya retocado.
    /// </summary>
    /// <remarks>
    /// Deshacer se hacía volviendo a una copia de antes del primer intercambio, y eso
    /// borraba <b>todo</b> lo hecho desde entonces: movías un color, lo retocabas, y al
    /// deshacer volvía el de antes con su nombre viejo y el retoque desaparecía sin
    /// avisar. Deshacer es de los movimientos, no de la sesión entera.
    /// </remarks>
    [AvaloniaFact]
    public void Deshacer_los_movimientos_respeta_lo_retocado()
    {
        var main = new MainWindowViewModel(new TestDialogService());
        ColorPalette palette = main.Palettes.Add("Mía");

        var panel = new EditPaletteViewModel(main, palette);

        string wasThree = palette[3].HexRgb;

        // Se mueve el 3 al 10 y allí se pone rojo.
        panel.SwapColors(3, 10);

        palette[10].SetComponents(7, 0, 0);

        Assert.Equal("700", palette[10].HexRgb);
        Assert.Equal("Color A", palette[10].DisplayName);

        panel.DiscardSwaps();

        // El color vuelve al 3, pero rojo y sin el nombre viejo: eso se retocó a posta.
        Assert.Equal("700", palette[3].HexRgb);
        Assert.Equal("Color 3", palette[3].DisplayName);
        Assert.NotEqual(wasThree, palette[3].HexRgb);
        Assert.False(panel.HasSwaps);
    }

    /// <summary>
    /// Si el panel se va por cualquier otra vía, los intercambios pendientes se deshacen.
    /// </summary>
    /// <remarks>
    /// Dejarlos puestos sin reajustar los dibujos es la peor salida: la paleta queda bien
    /// y todo lo pintado con ella, mal, sin haber dicho nada.
    /// </remarks>
    [AvaloniaFact]
    public void Cerrar_el_panel_por_otro_lado_deshace_lo_pendiente()
    {
        var main = new MainWindowViewModel(new TestDialogService());
        ColorPalette palette = main.Palettes.Add("Mía");

        string wasThree = palette[3].HexRgb;

        main.EditPaletteCommand.Execute(null);

        var panel = (EditPaletteViewModel)main.RightPanViewModel!;
        panel.SwapColors(3, 10);

        Assert.NotEqual(wasThree, palette[3].HexRgb);

        main.RightPanViewModel = null;

        Assert.Equal(wasThree, palette[3].HexRgb);
    }
    /// <summary>
    /// En GRAPHIC 2 los colores por línea aguantan un intercambio que toque el par de los
    /// grupos.
    /// </summary>
    /// <remarks>
    /// Los 32 pares de GRAPHIC 1 están también en un juego de GRAPHIC 2, donde no mandan,
    /// con el par de fábrica: F sobre 0. Al reajustarlos, mover la F les cambiaba el par, y
    /// un grupo que cambia baja el suyo a las líneas de sus ocho tiles: intercambiar la F
    /// dejaba los 256 tiles de un color liso, cada línea con el par del grupo. Salía con
    /// cualquier juego venido de un volcado de VRAM, que es justo donde cuadrar la paleta
    /// obliga a mover colores.
    /// </remarks>
    [AvaloniaFact]
    public async Task Intercambiar_la_F_no_aplana_un_juego_de_graphic2()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = true };
        var main = new MainWindowViewModel(dialogs);

        ColorPalette palette = main.Palettes.Add("Mía");
        var tileSet = new TileSet("De un volcado");

        main.OpenTileSet(tileSet, palette);

        // Dos líneas del primer grupo y una del último, que es donde se vio: se aplanaban
        // los 32 grupos y con ellos el juego entero.
        TileRow white = tileSet.ListOfTiles[0].ArrayTileRows[0];
        TileRow other = tileSet.ListOfTiles[0].ArrayTileRows[1];
        TileRow far = tileSet.ListOfTiles[255].ArrayTileRows[3];

        (white.ForeColor, white.BackColor) = (15, 1);
        (other.ForeColor, other.BackColor) = (6, 2);
        (far.ForeColor, far.BackColor) = (10, 4);

        (Avalonia.Media.Color Fore, Avalonia.Media.Color Back) Seen(TileRow row) =>
            (palette.GetColor(row.ForeColor), palette.GetColor(row.BackColor));

        var was = new[] { Seen(white), Seen(other), Seen(far) };

        var panel = new EditPaletteViewModel(main, palette);

        Assert.True(panel.SwapColors(15, 14));

        await panel.ApplyCommand.ExecuteAsync(null);

        // La que usaba la F la sigue usando, esté donde esté ahora...
        Assert.Equal(14, white.ForeColor);
        Assert.Equal(1, white.BackColor);

        // ...y las que no la usaban se quedan como estaban, en vez de acabar todas con el
        // par del grupo.
        Assert.Equal((6, 2), (other.ForeColor, other.BackColor));
        Assert.Equal((10, 4), (far.ForeColor, far.BackColor));

        // Que es lo que se le pide: se ve igual que antes.
        Assert.Equal(was, new[] { Seen(white), Seen(other), Seen(far) });
    }
    /// <summary>
    /// Un ajuste aplicado se puede deshacer: los colores vuelven a su ranura y los dibujos
    /// a sus índices.
    /// </summary>
    /// <remarks>
    /// Hasta aquí, aplicar era el único punto sin retorno del panel: los movimientos
    /// pendientes se descartaban, pero una vez ajustados los dibujos no había forma de
    /// volver. Y es justo cuando se ve si la colocación nueva era la buena.
    /// </remarks>
    [AvaloniaFact]
    public async Task Deshacer_el_ajuste_devuelve_los_indices_y_los_colores()
    {
        var main = new MainWindowViewModel(new TestDialogService { ConfirmAnswer = true });

        ColorPalette palette = main.Palettes.Add("Mía");
        var tileSet = new TileSet("Bosque");

        main.OpenTileSet(tileSet, palette);

        TileRow row = tileSet.ListOfTiles[0].ArrayTileRows[0];
        (row.ForeColor, row.BackColor) = (3, 10);

        string wasThree = palette[3].HexRgb;
        string wasTen = palette[10].HexRgb;

        var panel = new EditPaletteViewModel(main, palette);

        panel.SwapColors(3, 10);

        await panel.ApplyCommand.ExecuteAsync(null);

        Assert.True(panel.CanUndoApplied);

        panel.UndoAppliedCommand.Execute(null);

        Assert.Equal((3, 10), (row.ForeColor, row.BackColor));
        Assert.Equal(wasThree, palette[3].HexRgb);
        Assert.Equal(wasTen, palette[10].HexRgb);

        Assert.False(panel.CanUndoApplied);
    }

    /// <summary>
    /// Y alcanza a todo lo que se ajustó, no sólo a lo que se esté mirando.
    /// </summary>
    /// <remarks>
    /// Deshacer a medias es peor que no deshacer: dejaría el banco con los índices nuevos y
    /// la paleta con los colores viejos, que es la única combinación que no se ve bien en
    /// ningún sitio.
    /// </remarks>
    [AvaloniaFact]
    public async Task Deshacer_alcanza_a_los_documentos_que_no_estan_delante()
    {
        var main = new MainWindowViewModel(new TestDialogService { ConfirmAnswer = true });

        ColorPalette palette = main.Palettes.Add("Mía");

        main.OpenTileSet(new TileSet("Bosque"), palette);

        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");

        main.OpenSpriteBank(bank, palette);

        bank.SpritesList[0].ArraySpriteRows[0].Color = 3;

        var panel = new EditPaletteViewModel(main, palette);

        panel.SwapColors(3, 10);

        await panel.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(10, bank.SpritesList[0].ArraySpriteRows[0].Color);

        panel.UndoAppliedCommand.Execute(null);

        Assert.Equal(3, bank.SpritesList[0].ArraySpriteRows[0].Color);
    }

    /// <summary>
    /// El botón sale después de aplicar, y sólo entonces.
    /// </summary>
    /// <remarks>
    /// Con movimientos pendientes no: ahí «deshacer» sería ambiguo —¿lo pendiente o lo
    /// aplicado?— y el botón que sale es el de aplicar.
    /// </remarks>
    [AvaloniaFact]
    public async Task Deshacer_solo_se_puede_despues_de_aplicar()
    {
        var main = new MainWindowViewModel(new TestDialogService { ConfirmAnswer = true });

        ColorPalette palette = main.Palettes.Add("Mía");

        main.OpenTileSet(new TileSet("Bosque"), palette);

        var panel = new EditPaletteViewModel(main, palette);

        Assert.False(panel.CanUndoApplied);

        panel.SwapColors(3, 10);

        Assert.False(panel.CanUndoApplied);

        await panel.ApplyCommand.ExecuteAsync(null);

        Assert.True(panel.CanUndoApplied);

        // Y se esconde otra vez en cuanto hay un movimiento nuevo sin aplicar...
        panel.SwapColors(5, 12);

        Assert.False(panel.CanUndoApplied);

        // ...y vuelve si ese movimiento se descarta, que deja las cosas como estaban.
        panel.DiscardSwaps();

        Assert.True(panel.CanUndoApplied);

        panel.UndoAppliedCommand.Execute(null);

        Assert.False(panel.CanUndoApplied);
    }

    /// <summary>
    /// Deshacer lo aplicado tampoco se lleva por delante lo retocado después.
    /// </summary>
    /// <remarks>
    /// Lo mismo que al descartar los movimientos pendientes, y por lo mismo: el color
    /// vuelve a su ranura, pero vuelve como está ahora. Retocarlo fue deliberado.
    /// </remarks>
    [AvaloniaFact]
    public async Task Deshacer_lo_aplicado_respeta_lo_retocado()
    {
        var main = new MainWindowViewModel(new TestDialogService { ConfirmAnswer = true });

        ColorPalette palette = main.Palettes.Add("Mía");

        main.OpenTileSet(new TileSet("Bosque"), palette);

        var panel = new EditPaletteViewModel(main, palette);

        panel.SwapColors(3, 10);

        await panel.ApplyCommand.ExecuteAsync(null);

        // El que era el 3 vive ahora en el 10, y allí se pone rojo.
        palette[10].SetComponents(7, 0, 0);

        panel.UndoAppliedCommand.Execute(null);

        Assert.Equal("700", palette[3].HexRgb);
    }

    /// <summary>
    /// Un documento abierto después del ajuste no se toca al deshacer.
    /// </summary>
    /// <remarks>
    /// Ése no pasó por el ajuste: sus dibujos apuntan a los índices de siempre, y
    /// reajustárselos al revés le movería unos colores que nadie le había movido. Por eso
    /// se guarda a quién se le aplicó en vez de volver a preguntar quién usa la paleta.
    /// </remarks>
    [AvaloniaFact]
    public async Task Un_documento_abierto_despues_del_ajuste_no_se_toca()
    {
        var main = new MainWindowViewModel(new TestDialogService { ConfirmAnswer = true });

        ColorPalette palette = main.Palettes.Add("Mía");

        main.OpenTileSet(new TileSet("Bosque"), palette);

        var panel = new EditPaletteViewModel(main, palette);

        panel.SwapColors(3, 10);

        await panel.ApplyCommand.ExecuteAsync(null);

        var late = new TileSet("Traído después");

        main.OpenTileSet(late, palette);

        TileRow row = late.ListOfTiles[0].ArrayTileRows[0];
        (row.ForeColor, row.BackColor) = (3, 10);

        panel.UndoAppliedCommand.Execute(null);

        Assert.Equal((3, 10), (row.ForeColor, row.BackColor));
    }
    /// <summary>
    /// Y una cadena de intercambios se deshace entera, con cada color a su sitio.
    /// </summary>
    /// <remarks>
    /// Con un solo intercambio no se ve si se deshace bien: ir y volver son la misma
    /// permutación, así que vale hasta la tabla de ida. Encadenando dos deja de valer, y es
    /// lo normal cuando se está cuadrando una paleta contra otra.
    /// </remarks>
    [AvaloniaFact]
    public async Task Deshacer_una_cadena_devuelve_cada_color_a_su_sitio()
    {
        var main = new MainWindowViewModel(new TestDialogService { ConfirmAnswer = true });

        ColorPalette palette = main.Palettes.Add("Mía");
        var tileSet = new TileSet("Bosque");

        main.OpenTileSet(tileSet, palette);

        TileRow row = tileSet.ListOfTiles[0].ArrayTileRows[0];
        TileRow another = tileSet.ListOfTiles[0].ArrayTileRows[1];

        (row.ForeColor, row.BackColor) = (3, 10);
        (another.ForeColor, another.BackColor) = (5, 1);

        List<string> Hexes() =>
            [.. Enumerable.Range(0, ColorPalette.Size).Select(slot => palette[slot].HexRgb)];

        List<string> was = Hexes();

        var panel = new EditPaletteViewModel(main, palette);

        panel.SwapColors(3, 10);
        panel.SwapColors(10, 5);

        await panel.ApplyCommand.ExecuteAsync(null);

        panel.UndoAppliedCommand.Execute(null);

        Assert.Equal((3, 10), (row.ForeColor, row.BackColor));
        Assert.Equal((5, 1), (another.ForeColor, another.BackColor));

        Assert.Equal(was, Hexes());
    }
    /// <summary>
    /// Y el botón de deshacer sale al aplicar, que es cuando hay algo que deshacer.
    /// </summary>
    /// <remarks>
    /// Montado, como el de aplicar: la visibilidad y el mando van por enlace, y un nombre
    /// mal escrito en el XAML no lo ve ninguna prueba del ViewModel.
    /// </remarks>
    [AvaloniaFact]
    public async Task El_boton_de_deshacer_sale_al_aplicar()
    {
        var main = new MainWindowViewModel(new TestDialogService { ConfirmAnswer = true });

        EditPaletteViewModel panel = TestPalette.Create(main);

        var window = new Window
        {
            Content = new ContentControl { Content = panel },
            Width = 360,
            Height = 700,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Button undo = window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button =>
                button.Content is string text && text == Localizer.Instance["PaletteUndoApply"]);

        Assert.False(undo.IsVisible);

        panel.SwapColors(3, 10);

        await panel.ApplyCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        bool showed = undo.IsVisible;

        // Pulsándolo de verdad: es lo que dice que el mando está enlazado con el de aquí.
        undo.Command?.Execute(undo.CommandParameter);
        Dispatcher.UIThread.RunJobs();

        bool wentAway = !undo.IsVisible;

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(showed, "Tras aplicar tendria que salir el boton de deshacer.");
        Assert.True(wentAway, "Al deshacer el ajuste el boton tendria que irse.");
    }
    /// <summary>
    /// La pista de arrastrar se enciende al entrar en la lista y se apaga al salir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Era un globo y no acababa de servir: colgado de la lista se abría una sola vez donde
    /// estuviera el ratón y se quedaba ahí, señalando a una fila que ya no era; y colgado de
    /// cada fila perseguía al ratón lista abajo.
    /// </para>
    /// <para>
    /// Con el ratón de verdad, que es lo que enciende la clase: comprobar el enlace a mano
    /// no diría si el estilo la recoge.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void La_pista_de_arrastrar_se_enciende_al_entrar_en_la_lista()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        TestPalette.Create(main);

        var panel = (EditPaletteViewModel)main.RightPanViewModel!;

        var window = new Window
        {
            Content = new ContentControl { Content = panel },
            Width = 360,
            Height = 700,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        EditPaletteView view = window.GetVisualDescendants().OfType<EditPaletteView>().Single();
        ListBox list = view.GetVisualDescendants().OfType<ListBox>().Single();

        TextBlock hint = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(text => text.Name == "SwapHint");

        double asleep = hint.Opacity;

        ListBoxItem row = list.GetRealizedContainers()
            .OfType<ListBoxItem>()
            .Single(item => item.DataContext is PaletteColor { Index: 5 });

        window.MouseMove(
            row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        double awake = hint.Opacity;

        // Y al salir de la lista se vuelve a apagar: sobre la propia pista, que está debajo.
        window.MouseMove(
            hint.TranslatePoint(new Point(hint.Bounds.Width / 2, hint.Bounds.Height / 2), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        double asleepAgain = hint.Opacity;

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, asleep);
        Assert.Equal(1, awake);
        Assert.Equal(0, asleepAgain);
    }
}
