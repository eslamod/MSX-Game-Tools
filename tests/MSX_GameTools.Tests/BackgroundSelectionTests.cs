using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El desplegable elige imagen y el botón elige celda. Son dos controles porque una hoja
/// puede tener cientos de celdas y ninguna se reconoce por su número.
/// </summary>
public class BackgroundSelectionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxsel-{Guid.NewGuid():N}");

    public BackgroundSelectionTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [AvaloniaFact]
    public void Elegir_una_imagen_de_una_sola_celda_la_pone_sin_preguntar()
    {
        var dialogs = new TestDialogService();
        BackgroundSelectionViewModel selection = NewSelection(dialogs, out ReferenceImageLibrary library);

        ReferenceImage image = library.Load(WritePng("chica.png", 30, 30), cellSize: 0);

        selection.Image = image;

        Assert.Equal(0, dialogs.PickCellCalls);
        Assert.Same(image.Tiles[0], selection.Tile);

        // Sin retícula no hay nada que elegir, así que el botón no se enseña.
        Assert.False(selection.CanPickCell);
        Assert.Equal("completa", selection.CellLabel);
    }

    [AvaloniaFact]
    public void Elegir_una_hoja_abre_la_reticula()
    {
        var dialogs = new TestDialogService { PickedCell = 5 };
        BackgroundSelectionViewModel selection = NewSelection(dialogs, out ReferenceImageLibrary library);

        ReferenceImage sheet = library.Load(WritePng("hoja.png", 64, 48), cellSize: 16);

        selection.Image = sheet;

        Assert.Equal(1, dialogs.PickCellCalls);
        Assert.Same(sheet, dialogs.LastPickCellImage);
        Assert.Equal(5, selection.Tile!.Index);
    }

    /// <summary>
    /// Lo que no resolvía el desplegable solo: con la imagen ya elegida, volver a
    /// elegirla no dispara nada, así que sin el botón no habría forma de cambiar de
    /// celda sin pasar antes por otra imagen.
    /// </summary>
    [AvaloniaFact]
    public async Task Con_la_imagen_ya_puesta_el_boton_permite_cambiar_de_celda()
    {
        var dialogs = new TestDialogService { PickedCell = 2 };
        BackgroundSelectionViewModel selection = NewSelection(dialogs, out ReferenceImageLibrary library);

        ReferenceImage sheet = library.Load(WritePng("hoja.png", 64, 48), cellSize: 16);
        selection.Image = sheet;

        Assert.Equal(2, selection.Tile!.Index);

        // El desplegable escribe lo mismo que ya había: no debe tocar la celda. Se
        // cancela la retícula para que, si volviera a arrancar por la celda 0, el
        // recuento se quedara ahí y se notara.
        dialogs.PickedCell = null;
        selection.Image = sheet;

        Assert.Equal(2, selection.Tile!.Index);

        dialogs.PickedCell = 9;
        await selection.PickCellCommand.ExecuteAsync(null);

        Assert.Equal(9, selection.Tile!.Index);
    }

    [AvaloniaFact]
    public async Task La_reticula_se_abre_enseñando_la_celda_que_ya_estaba_puesta()
    {
        var dialogs = new TestDialogService { PickedCell = 7 };
        BackgroundSelectionViewModel selection = NewSelection(dialogs, out ReferenceImageLibrary library);

        selection.Image = library.Load(WritePng("hoja.png", 64, 48), cellSize: 16);

        await selection.PickCellCommand.ExecuteAsync(null);

        Assert.Equal(7, dialogs.LastPickCellCurrent);
    }

    [AvaloniaFact]
    public async Task Cancelar_la_reticula_deja_la_celda_como_estaba()
    {
        var dialogs = new TestDialogService { PickedCell = 4 };
        BackgroundSelectionViewModel selection = NewSelection(dialogs, out ReferenceImageLibrary library);

        selection.Image = library.Load(WritePng("hoja.png", 64, 48), cellSize: 16);

        dialogs.PickedCell = null;
        await selection.PickCellCommand.ExecuteAsync(null);

        Assert.Equal(4, selection.Tile!.Index);
    }

    /// <summary>
    /// La retícula tiene su propio botón de quitar el fondo, y lo dice con un índice
    /// negativo porque «ninguna celda» no es una celda.
    /// </summary>
    [AvaloniaFact]
    public async Task Un_indice_negativo_quita_el_fondo()
    {
        var dialogs = new TestDialogService { PickedCell = 3 };
        BackgroundSelectionViewModel selection = NewSelection(dialogs, out ReferenceImageLibrary library);

        selection.Image = library.Load(WritePng("hoja.png", 64, 48), cellSize: 16);

        dialogs.PickedCell = -1;
        await selection.PickCellCommand.ExecuteAsync(null);

        Assert.Null(selection.Tile);
        Assert.Null(selection.Image);
    }

    [AvaloniaFact]
    public void La_etiqueta_del_boton_dice_la_posicion_en_la_reticula()
    {
        var dialogs = new TestDialogService { PickedCell = 5 };
        BackgroundSelectionViewModel selection = NewSelection(dialogs, out ReferenceImageLibrary library);

        // 64x48 a 16 son 4 columnas: la celda 5 es la 1,1.
        selection.Image = library.Load(WritePng("hoja.png", 64, 48), cellSize: 16);

        Assert.Equal("1,1", selection.CellLabel);
        Assert.True(selection.CanPickCell);
    }

    [AvaloniaFact]
    public void Poner_la_imagen_a_null_quita_el_fondo()
    {
        var dialogs = new TestDialogService { PickedCell = 1 };
        BackgroundSelectionViewModel selection = NewSelection(dialogs, out ReferenceImageLibrary library);

        selection.Image = library.Load(WritePng("hoja.png", 64, 48), cellSize: 16);
        selection.Image = null;

        Assert.Null(selection.Tile);
        Assert.Equal("…", selection.CellLabel);
    }

    [AvaloniaFact]
    public void Cambiar_de_hoja_arranca_por_su_primera_celda()
    {
        var dialogs = new TestDialogService { PickedCell = null };
        BackgroundSelectionViewModel selection = NewSelection(dialogs, out ReferenceImageLibrary library);

        ReferenceImage first = library.Load(WritePng("una.png", 64, 48), cellSize: 16);
        ReferenceImage second = library.Load(WritePng("otra.png", 64, 48), cellSize: 16);

        selection.Image = first;

        // Aunque se cancele la retícula, la imagen elegida queda puesta: si no, el
        // desplegable enseñaría una que no es la del fondo.
        Assert.Same(first, selection.Image);
        Assert.Equal(0, selection.Tile!.Index);

        selection.Image = second;

        Assert.Same(second, selection.Image);
        Assert.Equal(0, selection.Tile!.Index);
    }

    /// <summary>
    /// El motivo de que exista el toggle: quitar el fondo para ver el dibujo limpio te
    /// obligaba a acordarte de qué celda era para volver a ponerla.
    /// </summary>
    [AvaloniaFact]
    public void Ocultar_el_fondo_no_pierde_la_celda_elegida()
    {
        var dialogs = new TestDialogService { PickedCell = 6 };
        BackgroundSelectionViewModel selection = NewSelection(dialogs, out ReferenceImageLibrary library);

        selection.Image = library.Load(WritePng("hoja.png", 64, 48), cellSize: 16);

        selection.IsVisible = false;

        Assert.Null(selection.VisibleTile);
        Assert.Equal(6, selection.Tile!.Index);
        Assert.Equal("2,1", selection.CellLabel);

        selection.IsVisible = true;

        Assert.Same(selection.Tile, selection.VisibleTile);
    }

    [AvaloniaFact]
    public void El_toggle_no_se_ensena_sin_fondo_elegido()
    {
        BackgroundSelectionViewModel selection = NewSelection(new TestDialogService(), out ReferenceImageLibrary library);

        Assert.False(selection.HasTile);

        selection.Image = library.Load(WritePng("chica.png", 30, 30), cellSize: 0);

        Assert.True(selection.HasTile);
    }

    /// <summary>
    /// Con el fondo oculto la composición vuelve a pintar el hueco opaco: si siguiera
    /// transparente se vería el gris del contenedor por detrás.
    /// </summary>
    [AvaloniaFact]
    public void Ocultar_el_fondo_devuelve_el_hueco_al_color_de_fondo()
    {
        var dialogs = new TestDialogService { PickedCell = 0 };
        var library = new ReferenceImageLibrary();

        var editor = new SpritesEditorViewModel(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"), ColorPalette.CreateMsxStandard(), dialogs, library);

        editor.AddGroupCommand.Execute(null);
        SpriteGroupViewModel group = editor.SelectedGroup!;

        group.Background.Image = library.Load(WritePng("hoja.png", 64, 48), cellSize: 16);

        Assert.Equal(0, Alpha(group.Preview, 0, 0));

        group.Background.IsVisible = false;

        Assert.Equal(255, Alpha(group.Preview, 0, 0));
    }

    private static int Alpha(ImageMini mini, int x, int y) => (int)((uint)PixelReader.At(mini, x, y) >> 24);

    [AvaloniaFact]
    public void El_grupo_y_el_patron_llevan_selectores_separados()
    {
        var dialogs = new TestDialogService { PickedCell = 3 };
        var library = new ReferenceImageLibrary();

        var editor = new SpritesEditorViewModel(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"), ColorPalette.CreateMsxStandard(), dialogs, library);

        ReferenceImage sheet = library.Load(WritePng("hoja.png", 64, 48), cellSize: 16);

        editor.AddGroupCommand.Execute(null);

        editor.SelectedGroup!.Background.Image = sheet;
        dialogs.PickedCell = 8;
        editor.PatternBackground.Image = sheet;

        Assert.Equal(3, editor.SelectedGroup.Background.Tile!.Index);
        Assert.Equal(8, editor.PatternBackground.Tile!.Index);
    }

    private BackgroundSelectionViewModel NewSelection(TestDialogService dialogs, out ReferenceImageLibrary library)
    {
        library = new ReferenceImageLibrary();
        BackgroundRef stored = BackgroundRef.None;

        return new BackgroundSelectionViewModel(
            library, dialogs, () => stored, reference => stored = reference);
    }

    private string WritePng(string name, int width, int height)
    {
        string path = Path.Combine(_folder, name);

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

        bitmap.Save(path);

        return path;
    }
}
