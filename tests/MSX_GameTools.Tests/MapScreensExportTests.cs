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
/// Exporting the map cut into the screens of the game.
/// </summary>
/// <remarks>
/// A game of fixed screens draws the whole map and then loads a screen every time a door is
/// crossed. What is checked here is what does not show by reading: where it cuts, which screens
/// it does not write, what it fills the one at the edge with, and what is said at the end.
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

        // In reading order, and column before row: the one the editor calls «2-1» is the one
        // ending in _2_1.
        Assert.Equal(
            (string[])["nivel_1_1_1.bin", "nivel_1_2_1.bin", "nivel_1_1_2.bin", "nivel_1_2_2.bin"],
            opened.Form.Files.Select(file => file.Name));

        await AcceptAsync(opened.Form);

        Assert.Equal(
            (string[])["nivel_1_1_1.bin", "nivel_1_1_2.bin", "nivel_1_2_1.bin", "nivel_1_2_2.bin"],
            Directory.GetFiles(_folder).Select(Path.GetFileName).Order());
    }

    /// <summary>
    /// The screens with nothing drawn do not make it to a file.
    /// </summary>
    /// <remarks>
    /// In a map of fixed screens the rectangle is usually not whole —an L, a cross, a castle
    /// with its wings—, and a file of 768 zeros for every hole in the drawing is not a map, it
    /// is wasted room. The ones that do go out keep their number.
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
    /// The screen at the edge goes out whole, with the filler tile where the map does not reach.
    /// </summary>
    /// <remarks>
    /// Every screen of the game measures the same, so the loader always reads the same number of
    /// bytes: a short screen would leave the rest of it with whatever was there before.
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

        // Eight columns of map and the other twenty-four of filler.
        Assert.Equal(7, edge[0]);
        Assert.Equal(3, edge[8]);
        Assert.Equal(3, edge[^1]);
    }

    /// <summary>The header is optional: every screen measures the same.</summary>
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
    /// Every screen carries written which map and which piece it came from.
    /// </summary>
    /// <remarks>
    /// With twenty files in the same folder, the name is all that is left to tell which is which.
    /// And the label carries the number inside, so two screens can be assembled together without
    /// clashing.
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

        // And without a header, which was not asked for.
        Assert.DoesNotContain("; Header:", screen);
    }

    /// <summary>
    /// A screen that cuts a super tile in half does not let the export go on.
    /// </summary>
    /// <remarks>
    /// Without saying so, the list of files would be left empty and unexplained: it would show
    /// that nothing is going out, but not why. And the answer —changing the size of the
    /// screen— is in the preferences, which is another panel.
    /// </remarks>
    [AvaloniaFact]
    public async Task Una_pantalla_que_parte_un_supertile_no_deja_exportar()
    {
        Opened opened = Open(32, 24, superTile: 2, painted: [(0, 0)]);

        opened.Main.Preferences.ScreenHeight = 21;

        // With the index on, as it comes by default: with no screens to point at, not even the
        // index goes in the list.
        ByScreens(opened.Form, ExportFormat.Binary, index: true);

        Assert.True(opened.Form.HasError);
        Assert.Empty(opened.Form.Files);

        await AcceptAsync(opened.Form);

        Assert.Empty(Directory.GetFiles(_folder));

        // And the panel stays open, which is what it takes to fix it.
        Assert.NotNull(opened.Main.RightPanViewModel);
    }

    /// <summary>
    /// By screens the example ROM is not offered.
    /// </summary>
    /// <remarks>
    /// The ROM loads a map and shows it; a folder of screens is another program —the one that
    /// goes from screen to screen— and that one is not written. Offering it would be a box that
    /// lies.
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
    /// Ticking the box turns the destination into the folder that holds it, and back.
    /// </summary>
    /// <remarks>
    /// Left as it was, a folder chosen before would be read as a file —the folder would be the
    /// one above— and the screens would end up one level higher than where it was said.
    /// </remarks>
    [AvaloniaFact]
    public void Marcar_la_casilla_convierte_el_destino_en_carpeta()
    {
        Opened opened = Open(32, 24, painted: [(0, 0)]);

        opened.Form.Format = Choice(opened.Form, ExportFormat.Binary);
        opened.Form.Destination = Path.Combine(_folder, "nivel_1.bin");

        opened.Form.ByScreens = true;

        Assert.Equal(_folder, opened.Form.Destination);
        Assert.Contains("nivel_1_1_1.bin", opened.Form.Files.Select(file => file.Name));

        opened.Form.ByScreens = false;

        Assert.Equal(Path.Combine(_folder, "nivel_1.bin"), opened.Form.Destination);
        Assert.Equal("nivel_1.bin", opened.Form.Files[0].Name);
    }

    /// <summary>With csv there are no screens to offer, and the box ticked drops.</summary>
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
    /// With many screens the list is summed up.
    /// </summary>
    /// <remarks>
    /// Thirteen names are no longer read at a glance, and a hundred even less. What needs
    /// knowing —what they are called and how many there are— is still there.
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
    /// At the end it is said how many went out, how many did not, and what the filler is.
    /// </summary>
    /// <remarks>
    /// The two things that do not show by looking at the folder: that files are missing on
    /// purpose, and that the screens at the edge carry filler that was not in the map.
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

    /// <summary>The button next to it asks for a folder, which is what is needed here.</summary>
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

    // ------------------------------------------------------------------ one screen on its own

    /// <summary>
    /// A screen can be asked for by its number, and only that one goes out.
    /// </summary>
    /// <remarks>
    /// It is for when a room is touched and has to be written again: the other twenty are
    /// already there, and rewriting all of them turns the change of one into twenty files with
    /// a new date.
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
    /// And an empty one asked for by its number does get written.
    /// </summary>
    /// <remarks>
    /// Skipping the empty ones is for the whole batch, where they are the holes of the drawing.
    /// Asking for one by its number is asking for that one, and one left empty on purpose —a
    /// cellar the game fills in on entering— is as asked for as the others.
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

    /// <summary>A screen that does not exist is said, instead of writing nothing unexplained.</summary>
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
    /// The panel starts on the screen of whatever was selected.
    /// </summary>
    /// <remarks>
    /// The selection is the last thing said on purpose about where one was working, and it is
    /// still there when the panel opens; the mouse is not, it stays wherever it left the canvas
    /// on the way to the menu.
    /// </remarks>
    [AvaloniaFact]
    public void El_panel_arranca_por_la_pantalla_de_lo_marcado()
    {
        Opened opened = Open(96, 48, selected: (70, 30), painted: [(0, 0)]);

        Assert.Equal(3, opened.Form.ScreenColumn);
        Assert.Equal(2, opened.Form.ScreenRow);
    }

    /// <summary>And with nothing selected, on the first one.</summary>
    [AvaloniaFact]
    public void Sin_nada_marcado_el_panel_arranca_por_la_primera()
    {
        Opened opened = Open(96, 48, painted: [(0, 0)]);

        Assert.Equal(1, opened.Form.ScreenColumn);
        Assert.Equal(1, opened.Form.ScreenRow);
    }

    /// <summary>
    /// Of a single one it is said which went out, and whether it carried filler.
    /// </summary>
    /// <remarks>
    /// How many went out does not need saying —one, the one asked for— nor that the empty ones
    /// are missing, since none was skipped here.
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

    /// <summary>The one at the edge asked for on its own goes out whole too.</summary>
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
    /// The two buttons of the pair, really pressed.
    /// </summary>
    /// <remarks>
    /// A group of radio buttons writes a <c>false</c> back into the one it unticks, so with the
    /// view model on its own both look fine, and mounted they step on each other.
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
    /// And saying that all of them go, with no view in front, takes away the single one.
    /// </summary>
    /// <remarks>
    /// Mounted it is not needed: the group unticks the other button and its binding is the one
    /// writing the <c>false</c>. But the property has to hold up on its own, since whoever sets
    /// it from outside is saying «all» and that is what has to stay.
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

    // ------------------------------------------------------------------ marked on the map

    /// <summary>
    /// The screen that is about to be written gets marked on the map.
    /// </summary>
    /// <remarks>
    /// The number of a screen does not say what is inside. Marking it on the map answers both
    /// things at once —where it falls and what it carries— without taking anyone out of the
    /// panel.
    /// </remarks>
    [AvaloniaFact]
    public void La_pantalla_pedida_se_senala_en_el_mapa()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary);

        Assert.Null(opened.Editor.ScreenPreview);

        Pick(opened.Form, 2, 1);

        Assert.Equal(new MapRegion(32, 0, 32, 24), opened.Editor.ScreenPreview);

        // And without touching what had been selected by hand, which is a tool and not an
        // ornament.
        Assert.Null(opened.Editor.Selection);
    }

    /// <summary>Going back to all of them takes it away: there is no one screen to talk about.</summary>
    [AvaloniaFact]
    public void Volver_a_todas_quita_la_senal()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary);
        Pick(opened.Form, 2, 1);

        opened.Form.AllScreens = true;

        Assert.Null(opened.Editor.ScreenPreview);
    }

    /// <summary>And one that does not exist is not marked, there being nowhere to.</summary>
    [AvaloniaFact]
    public void Una_pantalla_que_no_existe_no_se_senala()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary);
        Pick(opened.Form, 5, 1);

        Assert.Null(opened.Editor.ScreenPreview);
    }

    /// <summary>
    /// Closing the panel takes the mark away.
    /// </summary>
    /// <remarks>
    /// By any of the ways out, not only by Cancel: the panel also goes when another one opens on
    /// top of it, and the mark would stay on with nobody left to write it.
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

    // ------------------------------------------------------------------ the index

    /// <summary>
    /// With all of them the index comes out by default, heading the list.
    /// </summary>
    /// <remarks>
    /// Heading it because the list is cut after a dozen: with a hundred screens, the two files
    /// that are not screens would end up in the count of the ones that do not fit.
    /// </remarks>
    [AvaloniaFact]
    public void Con_todas_el_indice_sale_de_partida_y_encabeza_la_lista()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        opened.Form.Format = Choice(opened.Form, ExportFormat.Binary);
        opened.Form.ByScreens = true;

        Assert.True(opened.Form.ScreenIndex);
        Assert.True(opened.Form.ShowsScreenIndex);

        Assert.Equal(
            (string[])["nivel_1_screens.asm", "nivel_1_screens_data.asm", "nivel_1_1_1.bin"],
            opened.Form.Files.Select(file => file.Name));
    }

    /// <summary>
    /// With only one there is no index.
    /// </summary>
    /// <remarks>
    /// The index speaks of the whole batch. Written with a single screen, it would say the others
    /// are not there, and they are: they went out before.
    /// </remarks>
    [AvaloniaFact]
    public void Con_una_sola_no_se_ofrece_el_indice()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        Pick(opened.Form, 1, 1);

        Assert.False(opened.Form.ShowsScreenIndex);
        Assert.Equal((string[])["nivel_1_1_1.bin"], opened.Form.Files.Select(file => file.Name));
    }

    /// <summary>
    /// The table goes row by row, with 0 where a screen was not written.
    /// </summary>
    /// <remarks>
    /// Zero because no screen can start at 0x0000: the game tells there is no room there by
    /// testing the pointer, which is the whole point of skipping the empty ones.
    /// </remarks>
    [AvaloniaFact]
    public async Task La_tabla_va_fila_a_fila_con_cero_en_las_vacias()
    {
        Opened opened = Open(12, 6, painted: [(0, 0), (8, 0), (0, 3), (4, 3)]);

        SmallScreens(opened);
        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        await AcceptAsync(opened.Form);

        string table = await File.ReadAllTextAsync(Path.Combine(_folder, "nivel_1_screens.asm"));

        Assert.Contains("NIVEL_1_SCREENS_WIDE .equ 3", table);
        Assert.Contains("NIVEL_1_SCREENS_HIGH .equ 2", table);
        Assert.Contains("nivel_1_screens:", table);
        Assert.Contains("    .dw nivel_1_1_1_map, 0, nivel_1_3_1_map    ; row 1", table);
        Assert.Contains("    .dw nivel_1_1_2_map, nivel_1_2_2_map, 0    ; row 2", table);
    }

    /// <summary>A long row of screens goes on in the next line instead of in one endless line.</summary>
    [AvaloniaFact]
    public async Task Una_fila_larga_sigue_en_la_linea_de_abajo()
    {
        (int Column, int Row)[] all = [.. Enumerable.Range(0, 10).Select(screen => (screen * 4, 0))];

        Opened opened = Open(40, 3, painted: all);

        SmallScreens(opened);
        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        await AcceptAsync(opened.Form);

        string[] lines = await File.ReadAllLinesAsync(Path.Combine(_folder, "nivel_1_screens.asm"));

        int first = Array.FindIndex(lines, line => line.EndsWith("; row 1"));

        Assert.StartsWith("    .dw nivel_1_1_1_map, nivel_1_2_1_map,", lines[first]);
        Assert.Equal("    .dw nivel_1_9_1_map, nivel_1_10_1_map", lines[first + 1]);
    }

    /// <summary>
    /// The screens in bytes come in under the label the table points at.
    /// </summary>
    /// <remarks>
    /// A binary file has no label of its own, so it has to be written next to it; and the name
    /// has to be the very one the table uses, or the table points at nothing.
    /// </remarks>
    [AvaloniaFact]
    public async Task Las_pantallas_en_bytes_entran_con_la_etiqueta_de_la_tabla()
    {
        Opened opened = Open(8, 3, painted: [(0, 0), (4, 0)]);

        SmallScreens(opened);
        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        await AcceptAsync(opened.Form);

        string[] data = await File.ReadAllLinesAsync(
            Path.Combine(_folder, "nivel_1_screens_data.asm"));

        int label = Array.IndexOf(data, "nivel_1_2_1_map:");

        Assert.True(label >= 0, "the label of 2-1 is not there");
        Assert.Equal("    .incbin \"nivel_1_2_1.bin\"", data[label + 1]);
    }

    /// <summary>And the ones in assembler with an include, since they already carry their label.</summary>
    [AvaloniaFact]
    public async Task Las_pantallas_en_ensamblador_entran_con_un_include()
    {
        Opened opened = Open(8, 3, painted: [(0, 0), (4, 0)]);

        SmallScreens(opened);
        ByScreens(opened.Form, ExportFormat.Assembler, index: true);
        await AcceptAsync(opened.Form);

        string[] data = await File.ReadAllLinesAsync(
            Path.Combine(_folder, "nivel_1_screens_data.asm"));

        Assert.Contains("    .include \"nivel_1_2_1.asm\"", data);
        Assert.DoesNotContain("nivel_1_2_1_map:", data);
    }

    /// <summary>
    /// The four assemblers, with the screens in bytes and in assembler.
    /// </summary>
    public static TheoryData<string, ExportFormat> IndexBundles => new()
    {
        { "sasSX", ExportFormat.Binary },
        { "sasSX", ExportFormat.Assembler },
        { "sjasmplus", ExportFormat.Binary },
        { "sjasmplus", ExportFormat.Assembler },
        { "pasmo", ExportFormat.Binary },
        { "pasmo", ExportFormat.Assembler },
        { "asMSX", ExportFormat.Binary },
        { "asMSX", ExportFormat.Assembler },
    };

    /// <summary>
    /// The index assembles, and every pointer in the table lands where its screen starts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Assembled and read back, like the example ROMs: a table that assembles can still point
    /// one screen off, and that only shows by following each pointer. Every painted screen
    /// carries its own number in its first cell, so a pointer that lands anywhere else finds
    /// another number or a zero.
    /// </para>
    /// <para>
    /// Nine screens in a row so that the row goes on in a second line, and three empty ones so
    /// that the zeros are there too.
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [MemberData(nameof(IndexBundles))]
    public async Task El_indice_ensambla_y_cada_puntero_cae_en_su_pantalla(
        string name, ExportFormat format)
    {
        string? tool = Assembler.Find(name);

        Assert.SkipUnless(tool is not null, $"{name} no está aquí: {Assembler.HowToGetIt(name)}");

        AsmDialect dialect = AsmDialect.Of(name);

        (int Column, int Row)[] painted =
        [
            .. from row in Enumerable.Range(1, 2)
               from column in Enumerable.Range(1, 9)
               where !Empty.Contains((column, row))
               select ((column - 1) * 4, (row - 1) * 3),
        ];

        Opened opened = Open(36, 6, painted: painted);

        foreach ((int column, int row) in painted)
            opened.Map.Layers[0].Grid[column, row] = Id((column / 4) + 1, (row / 3) + 1);

        SmallScreens(opened);
        opened.Main.Preferences.AsmData = dialect.Style.Data;

        ByScreens(opened.Form, format, index: true);
        await AcceptAsync(opened.Form);

        string host = Path.Combine(_folder, "host.asm");

        await File.WriteAllTextAsync(host, Host(dialect));

        string rom = Path.Combine(_folder, "nivel_1.rom");
        string said = Assembler.Run(tool!, name, host, rom);

        SkipIfAsMsxFellOver(name, rom, said);

        Assert.True(File.Exists(rom), $"{name} no sacó ROM: {said}");

        byte[] bytes = await File.ReadAllBytesAsync(rom);

        int marker = bytes.AsSpan().IndexOf("IDX!"u8);

        Assert.True(marker >= 0, "the marker the host writes is not in the ROM");

        int table = Word(bytes, marker + 4) - 0x4000;

        // The two constants, which have to assemble and hold what the map measures.
        Assert.Equal(9, bytes[marker + 6]);
        Assert.Equal(2, bytes[marker + 7]);

        for (int row = 1; row <= 2; row++)
        {
            for (int column = 1; column <= 9; column++)
            {
                int pointer = Word(bytes, table + (2 * (((row - 1) * 9) + (column - 1))));

                if (Empty.Contains((column, row)))
                {
                    Assert.Equal(0, pointer);

                    continue;
                }

                int start = pointer - 0x4000;

                Assert.Equal(Id(column, row), bytes[start]);
                Assert.All(bytes[(start + 1)..(start + 12)], one => Assert.Equal(0, one));
            }
        }
    }

    /// <summary>
    /// Written alone, a screen that the index in the folder does not have is said.
    /// </summary>
    /// <remarks>
    /// It happens with one that was empty when all of them went out: the index has a 0 where it
    /// goes, and now that it exists the game would still not find it.
    /// </remarks>
    [AvaloniaFact]
    public async Task Una_pantalla_suelta_que_el_indice_no_tiene_se_avisa()
    {
        Opened opened = Open(8, 3, painted: [(0, 0)]);

        SmallScreens(opened);
        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        await AcceptAsync(opened.Form);

        opened.Map.Layers[0].Grid[4, 0] = 5;

        ExportViewModel again = Reopen(opened);

        ByScreens(again, ExportFormat.Binary);
        Pick(again, 2, 1);
        await AcceptAsync(again);

        Assert.Contains(Text["ExportedScreenNotInIndex"], opened.Dialogs.Messages[^1]);
    }

    /// <summary>And one that the index does have is not, which would be crying wolf.</summary>
    [AvaloniaFact]
    public async Task Una_pantalla_suelta_que_el_indice_tiene_no_se_avisa()
    {
        Opened opened = Open(8, 3, painted: [(0, 0), (4, 0)]);

        SmallScreens(opened);
        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        await AcceptAsync(opened.Form);

        ExportViewModel again = Reopen(opened);

        ByScreens(again, ExportFormat.Binary);
        Pick(again, 2, 1);
        await AcceptAsync(again);

        Assert.DoesNotContain(Text["ExportedScreenNotInIndex"], opened.Dialogs.Messages[^1]);
    }

    // ------------------------------------------------------------------ the pages of a mapper

    /// <summary>
    /// With pages, the screens are shared into them without cutting any, a file per page.
    /// </summary>
    /// <remarks>
    /// Ten screens of 768 bytes take 7680 of the 8192 of a page; the eleventh would not fit
    /// whole, so it starts the next one. Half a screen in each page would need the two mapped
    /// at once.
    /// </remarks>
    [AvaloniaFact]
    public void Con_paginas_las_pantallas_se_reparten_sin_partir_ninguna()
    {
        Opened opened = Open(12 * 32, 24, painted: FirstCells(12));

        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        Paged(opened.Form, 8);

        string[] names = [.. opened.Form.Files.Select(file => file.Name)];

        Assert.Equal(
            (string[])["nivel_1_screens.asm", "nivel_1_screens_page_0.asm", "nivel_1_screens_page_1.asm"],
            names[..3]);

        Assert.DoesNotContain("nivel_1_screens_data.asm", names);
    }

    /// <summary>
    /// The paged table says of each screen its page and its offset, both from the start.
    /// </summary>
    /// <remarks>
    /// From the start and not as addresses, so that they hold in whatever segment the pages end
    /// up and whatever window the mapper shows them in: the game maps the page and adds.
    /// </remarks>
    [AvaloniaFact]
    public async Task La_tabla_paginada_dice_pagina_y_desplazamiento()
    {
        Opened opened = Open(12 * 32, 24, painted: FirstCells(12, skip: 3));

        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        Paged(opened.Form, 8);
        await AcceptAsync(opened.Form);

        string table = await File.ReadAllTextAsync(Path.Combine(_folder, "nivel_1_screens.asm"));

        Assert.Contains("NIVEL_1_SCREENS_PAGE_COUNT .equ 2", table);

        // The third screen is empty: its page says so, and the ones after it move up a place.
        Assert.Contains("    .db 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00    ; row 1", table);
        Assert.Contains("    .db 0x00, 0x00, 0x00, 0x01", table);
        Assert.Contains(
            "    .dw 0x0000, 0x0300, 0x0000, 0x0600, 0x0900, 0x0C00, 0x0F00, 0x1200    ; row 1",
            table);
        Assert.Contains("    .dw 0x1500, 0x1800, 0x1B00, 0x0000", table);

        // And the second page starts under its own label with the one screen that did not fit.
        string[] second = await File.ReadAllLinesAsync(
            Path.Combine(_folder, "nivel_1_screens_page_1.asm"));

        int start = Array.IndexOf(second, "nivel_1_screens_page_1:");

        Assert.True(start >= 0, "the second page has no label of its own");
        Assert.Equal("nivel_1_12_1_map:", second[start + 1]);
        Assert.Equal("    .incbin \"nivel_1_12_1.bin\"", second[start + 2]);
    }

    /// <summary>With the size in front, the offsets count it: each screen takes four bytes more.</summary>
    [AvaloniaFact]
    public async Task Con_cabecera_los_desplazamientos_la_cuentan()
    {
        Opened opened = Open(3 * 32, 24, painted: FirstCells(3));

        ByScreens(opened.Form, ExportFormat.Binary, header: true, index: true);
        Paged(opened.Form, 8);
        await AcceptAsync(opened.Form);

        string table = await File.ReadAllTextAsync(Path.Combine(_folder, "nivel_1_screens.asm"));

        Assert.Contains("    .dw 0x0000, 0x0304, 0x0608    ; row 1", table);
    }

    /// <summary>A screen that does not fit in a page is said, and nothing is written.</summary>
    [AvaloniaFact]
    public async Task Una_pantalla_que_no_cabe_en_la_pagina_no_deja_exportar()
    {
        Opened opened = Open(128, 96, painted: [(0, 0)]);

        opened.Main.Preferences.ScreenWidth = 128;
        opened.Main.Preferences.ScreenHeight = 96;

        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        Paged(opened.Form, 8);

        Assert.Equal(Text.Format("ExportScreenTooBig", 128 * 96, 8), opened.Form.ErrorMessage);

        await AcceptAsync(opened.Form);

        Assert.Empty(Directory.GetFiles(_folder));

        // In a page of 16K it does fit.
        Paged(opened.Form, 16);

        Assert.False(opened.Form.HasError);
    }

    /// <summary>The pages are only asked with the index: they change what it says and nothing else.</summary>
    [AvaloniaFact]
    public void Sin_indice_no_se_preguntan_las_paginas()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary, index: true);

        Assert.True(opened.Form.ShowsScreenPages);
        Assert.Equal(0, opened.Form.ScreenPages.Bytes);

        opened.Form.ScreenIndex = false;

        Assert.False(opened.Form.ShowsScreenPages);
    }

    /// <summary>
    /// The paged index assembles, and every page and offset lands where its screen starts.
    /// </summary>
    /// <remarks>
    /// The host brings the table in and then each page behind a marker of its own, so that the
    /// start of each page can be found in the ROM. Following page and offset from there has to
    /// land on the screen that carries that number, for the four assemblers, with the screens in
    /// bytes and in assembler. Thirteen screens with the fourth empty: twelve written, ten in the
    /// first page and two in the second.
    /// </remarks>
    [AvaloniaTheory]
    [MemberData(nameof(IndexBundles))]
    public async Task El_indice_paginado_ensambla_y_cada_desplazamiento_cae_en_su_pantalla(
        string name, ExportFormat format)
    {
        string? tool = Assembler.Find(name);

        Assert.SkipUnless(tool is not null, $"{name} no está aquí: {Assembler.HowToGetIt(name)}");

        AsmDialect dialect = AsmDialect.Of(name);

        (int Column, int Row)[] painted = FirstCells(13, skip: 4);

        Opened opened = Open(13 * 32, 24, painted: painted);

        foreach ((int column, int row) in painted)
            opened.Map.Layers[0].Grid[column, row] = (column / 32) + 1;

        opened.Main.Preferences.AsmData = dialect.Style.Data;

        ByScreens(opened.Form, format, index: true);
        Paged(opened.Form, 8);
        await AcceptAsync(opened.Form);

        string host = Path.Combine(_folder, "host.asm");

        await File.WriteAllTextAsync(host, PagedHost(dialect, pages: 2));

        string rom = Path.Combine(_folder, "nivel_1.rom");
        string said = Assembler.Run(tool!, name, host, rom);

        SkipIfAsMsxFellOver(name, rom, said);

        Assert.True(File.Exists(rom), $"{name} no sacó ROM: {said}");

        byte[] bytes = await File.ReadAllBytesAsync(rom);

        int marker = bytes.AsSpan().IndexOf("IDX!"u8);

        Assert.True(marker >= 0, "the marker the host writes is not in the ROM");

        int pages = Word(bytes, marker + 4) - 0x4000;
        int offsets = Word(bytes, marker + 6) - 0x4000;

        Assert.Equal(13, bytes[marker + 8]);
        Assert.Equal(1, bytes[marker + 9]);
        Assert.Equal(2, bytes[marker + 10]);

        int[] starts =
        [
            bytes.AsSpan().IndexOf("PG0!"u8) + 4,
            bytes.AsSpan().IndexOf("PG1!"u8) + 4,
        ];

        for (int column = 1; column <= 13; column++)
        {
            int page = bytes[pages + column - 1];

            if (column == 4)
            {
                Assert.Equal(0xFF, page);

                continue;
            }

            int start = starts[page] + Word(bytes, offsets + (2 * (column - 1)));

            Assert.Equal(column, bytes[start]);
            Assert.All(bytes[(start + 1)..(start + 768)], one => Assert.Equal(0, one));
        }
    }

    // ------------------------------------------------------------------ left over from before

    /// <summary>
    /// Going paged, the data file of an earlier flat export is said, and not deleted.
    /// </summary>
    /// <remarks>
    /// Paged, that file is not written, and the table has the same name in both forms, so it
    /// gets overwritten: without a word, the folder is left with a paged table next to the data
    /// of a flat one, and which goes with which is anyone's guess.
    /// </remarks>
    [AvaloniaFact]
    public async Task Paginando_se_avisa_del_fichero_de_datos_de_antes()
    {
        Opened opened = Open(12 * 32, 24, painted: FirstCells(12));

        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        await AcceptAsync(opened.Form);

        ExportViewModel again = Reopen(opened);

        ByScreens(again, ExportFormat.Binary, index: true);
        Paged(again, 8);
        again.Destination = _folder;

        Assert.Equal(Text.Format("ExportLeftovers", "nivel_1_screens_data.asm"), again.Leftovers);

        await again.AcceptExportCommand.ExecuteAsync(null);

        Assert.True(File.Exists(Path.Combine(_folder, "nivel_1_screens_data.asm")));
    }

    /// <summary>And going back to flat, the pages of the paged one.</summary>
    [AvaloniaFact]
    public async Task Sin_paginar_se_avisa_de_las_paginas_de_antes()
    {
        Opened opened = Open(12 * 32, 24, painted: FirstCells(12));

        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        Paged(opened.Form, 8);
        await AcceptAsync(opened.Form);

        ExportViewModel again = Reopen(opened);

        ByScreens(again, ExportFormat.Binary, index: true);
        again.Destination = _folder;

        Assert.Equal(
            Text.Format("ExportLeftovers", "nivel_1_screens_page_0.asm, nivel_1_screens_page_1.asm"),
            again.Leftovers);
    }

    /// <summary>
    /// With fewer pages than before, the ones that no longer go out are said.
    /// </summary>
    /// <remarks>
    /// The same form, and still left over: the screens of that page now live in another one, and
    /// the old file would bring them in twice.
    /// </remarks>
    [AvaloniaFact]
    public async Task Con_menos_paginas_se_avisa_de_las_que_ya_no_salen()
    {
        Opened opened = Open(12 * 32, 24, painted: FirstCells(12));

        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        Paged(opened.Form, 8);
        await AcceptAsync(opened.Form);

        foreach ((int column, int row) in FirstCells(12).Skip(6))
            opened.Map.Layers[0].Grid[column, row] = null;

        ExportViewModel again = Reopen(opened);

        ByScreens(again, ExportFormat.Binary, index: true);
        Paged(again, 8);
        again.Destination = _folder;

        Assert.Equal(Text.Format("ExportLeftovers", "nivel_1_screens_page_1.asm"), again.Leftovers);
    }

    /// <summary>Without the index, all of the one from before is left over.</summary>
    [AvaloniaFact]
    public async Task Sin_indice_se_avisa_del_indice_de_antes()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        await AcceptAsync(opened.Form);

        ExportViewModel again = Reopen(opened);

        ByScreens(again, ExportFormat.Binary);
        again.Destination = _folder;

        Assert.Equal(
            Text.Format("ExportLeftovers", "nivel_1_screens.asm, nivel_1_screens_data.asm"),
            again.Leftovers);
    }

    /// <summary>
    /// With only one screen nothing is said: the index in the folder still holds.
    /// </summary>
    /// <remarks>
    /// Writing one screen on its own leaves the index as it was on purpose; calling it left over
    /// would be telling the user to throw away the table they are going to use.
    /// </remarks>
    [AvaloniaFact]
    public async Task Con_una_sola_no_se_avisa_del_indice()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        await AcceptAsync(opened.Form);

        ExportViewModel again = Reopen(opened);

        ByScreens(again, ExportFormat.Binary);
        Pick(again, 1, 1);
        again.Destination = _folder;

        Assert.Null(again.Leftovers);
    }

    /// <summary>
    /// What is written again is not left over, and neither is what is not of the family.
    /// </summary>
    [AvaloniaFact]
    public async Task Lo_que_se_vuelve_a_escribir_no_sobra()
    {
        Opened opened = Open(64, 48, painted: [(0, 0)]);

        ByScreens(opened.Form, ExportFormat.Binary, index: true);
        await AcceptAsync(opened.Form);

        await File.WriteAllTextAsync(Path.Combine(_folder, "notas.txt"), "de otra cosa");

        ExportViewModel again = Reopen(opened);

        ByScreens(again, ExportFormat.Binary, index: true);
        again.Destination = _folder;

        Assert.Null(again.Leftovers);
    }

    /// <summary>
    /// The first cell of each of that many screens of 32x24 in a row, but for the one to skip.
    /// </summary>
    /// <param name="skip">The screen left empty, counting from one, or 0 for none.</param>
    private static (int Column, int Row)[] FirstCells(int screens, int skip = 0) =>
        [.. Enumerable.Range(1, screens).Where(screen => screen != skip).Select(screen => ((screen - 1) * 32, 0))];

    /// <summary>Pages of the mapper of that many K.</summary>
    private static void Paged(ExportViewModel form, int kilobytes) =>
        form.ScreenPages = form.PageSizes.Single(choice => choice.Bytes == kilobytes * 1024);

    /// <summary>
    /// A cartridge that brings in the paged table and each page behind a marker.
    /// </summary>
    /// <remarks>
    /// The pages one after the other, which is not how a megaROM lays them out, and it does not
    /// need to be: the offsets count from the start of each page, and the marker says where
    /// that is.
    /// </remarks>
    private static string PagedHost(AsmDialect dialect, int pages) =>
        string.Join(
                "\n",
                [
                    dialect.Header,
                    "Begin:",
                    "                ret",
                    $"                {dialect.Directive("include")} \"nivel_1_screens.asm\"",
                    .. Enumerable.Range(0, pages).SelectMany(page => (string[])
                    [
                        $"                {dialect.Directive("db")} \"PG{page}!\"",
                        $"                {dialect.Directive("include")} \"nivel_1_screens_page_{page}.asm\"",
                    ]),
                    $"                {dialect.Directive("db")} \"IDX!\"",
                    $"                {dialect.Directive("dw")} nivel_1_screens_pages",
                    $"                {dialect.Directive("dw")} nivel_1_screens_offsets",
                    $"                {dialect.Directive("db")} NIVEL_1_SCREENS_WIDE, NIVEL_1_SCREENS_HIGH, NIVEL_1_SCREENS_PAGE_COUNT",
                    dialect.Tail,
                ])
            .Replace("{ORG}", dialect.Directive("org"))
            .Replace("{DB}", dialect.Directive("db"))
            .Replace("{DW}", dialect.Directive("dw"))
            .Replace("{DS}", dialect.Directive("ds"))
            .Replace("{START}", "Begin")
            .Replace("{STEM}", "nivel_1")
            .Replace("{ROM_END}", "0x8000")
            .Replace("\n", Environment.NewLine);

    /// <summary>
    /// asMSX 0.16 falls over now and then, depending on the byte each line lands on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured by hand: the very same files crash it or not by adding a letter to a comment of
    /// the host —with seven letters more it dies, with six or eight it does not—. It has done it
    /// with the screens in bytes and in assembler. What was not found is why.
    /// </para>
    /// <para>
    /// It dies with a segmentation fault, without a word and without a ROM, which is not what an
    /// assembler does with a line it rejects: that one prints its banner and the error. So it is
    /// a bug of its own and not something of ours it refuses, and it comes out as not checked
    /// here, which is the truth, instead of as a failure of the index.
    /// </para>
    /// <para>
    /// asMSX 1.2.0 does not do it: with <c>ASMSX</c> pointing at it the whole suite passes, the
    /// files that brought down 0.16 included, and it gives the same bytes as the other three.
    /// </para>
    /// </remarks>
    private static void SkipIfAsMsxFellOver(string name, string rom, string said) =>
        Assert.SkipWhen(
            name == "asMSX" && !File.Exists(rom) && string.IsNullOrWhiteSpace(said),
            "asMSX se ha caído sin decir nada. Al 0.16 le pasa con algunos ficheros según en qué "
            + "byte caiga cada línea, y no es algo de lo exportado que rechace; la 1.2.0 ya no se cae. "
            + "Aquí no se ha comprobado con él.");

    /// <summary>The screens of the index tests: nine across and two down, minus these.</summary>
    private static readonly (int Column, int Row)[] Empty = [(3, 1), (9, 1), (5, 2)];

    /// <summary>What a screen carries in its first cell, which is what says the pointer found it.</summary>
    private static int Id(int column, int row) => ((row - 1) * 9) + column;

    /// <summary>Screens of 4x3, so that the ROM of the test stays small and quick.</summary>
    private static void SmallScreens(Opened opened)
    {
        opened.Main.Preferences.ScreenWidth = 4;
        opened.Main.Preferences.ScreenHeight = 3;
    }

    /// <summary>The panel again, as it opens from the menu, over the same map.</summary>
    private static ExportViewModel Reopen(Opened opened)
    {
        opened.Main.ExportMapCommand.Execute(null);

        return (ExportViewModel)opened.Main.RightPanViewModel!;
    }

    private static int Word(byte[] bytes, int at) => bytes[at] | (bytes[at + 1] << 8);

    /// <summary>
    /// A cartridge that brings the index in and leaves where the table is behind a marker.
    /// </summary>
    /// <remarks>
    /// The header and the tail are the dialect's, the same ones the example ROMs are built
    /// with, so each assembler gets the cartridge it knows how to make. The marker is what finds
    /// the table afterwards without having to know where each assembler put it.
    /// </remarks>
    private static string Host(AsmDialect dialect) =>
        string.Join(
                "\n",
                dialect.Header,
                "Begin:",
                "                ret",
                $"                {dialect.Directive("include")} \"nivel_1_screens.asm\"",
                $"                {dialect.Directive("include")} \"nivel_1_screens_data.asm\"",
                $"                {dialect.Directive("db")} \"IDX!\"",
                $"                {dialect.Directive("dw")} nivel_1_screens",
                $"                {dialect.Directive("db")} NIVEL_1_SCREENS_WIDE, NIVEL_1_SCREENS_HIGH",
                dialect.Tail)
            .Replace("{ORG}", dialect.Directive("org"))
            .Replace("{DB}", dialect.Directive("db"))
            .Replace("{DW}", dialect.Directive("dw"))
            .Replace("{DS}", dialect.Directive("ds"))
            .Replace("{START}", "Begin")
            .Replace("{STEM}", "nivel_1")
            .Replace("{ROM_END}", "0x8000")
            .Replace("\n", Environment.NewLine);

    // ------------------------------------------------------------------ the scaffolding

    /// <summary>A map opened with its export panel, which is the way through.</summary>
    private sealed record Opened(
        MainWindowViewModel Main,
        TestDialogService Dialogs,
        TileMap Map,
        MapEditorViewModel Editor,
        ExportViewModel Form);

    /// <param name="superTile">The side of the super tile, or 0 for a map of loose tiles.</param>
    /// <param name="selected">The cell left selected before opening the panel, if any.</param>
    /// <param name="painted">The cells that carry a tile, which are what makes a screen go out.</param>
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
    /// <remarks>
    /// Without the index unless it is asked for: it is on by default, and the tests that are
    /// about the screens themselves would be counting two files that are not screens.
    /// </remarks>
    private static void ByScreens(
        ExportViewModel form, ExportFormat format, bool header = false, bool index = false)
    {
        form.Format = Choice(form, format);
        form.ByScreens = true;
        form.ScreenHeader = header;
        form.ScreenIndex = index;
    }

    /// <summary>That only one goes, and which: column and row, counting from one.</summary>
    private static void Pick(ExportViewModel form, int column, int row)
    {
        form.OneScreen = true;
        form.ScreenColumn = column;
        form.ScreenRow = row;
    }

    /// <summary>The folder is chosen after the box, which is what makes it a folder.</summary>
    private async Task AcceptAsync(ExportViewModel form)
    {
        form.Destination = _folder;

        await form.AcceptExportCommand.ExecuteAsync(null);
    }
}
