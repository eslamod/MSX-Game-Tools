using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
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

        string wasThree = palette[3].DisplayName;
        string wasTen = palette[10].DisplayName;

        palette.Swap(3, 10);

        Assert.Equal(wasTen, palette[3].DisplayName);
        Assert.Equal(wasThree, palette[10].DisplayName);
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
        main.AddPaletteCommand.Execute(null);

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
}
