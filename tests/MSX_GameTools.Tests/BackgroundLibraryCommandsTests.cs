using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Cargar y eliminar imágenes de referencia desde la barra superior, y que viajen en el
/// fichero del banco.
/// </summary>
public class BackgroundLibraryCommandsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxbg-{Guid.NewGuid():N}");

    public BackgroundLibraryCommandsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [AvaloniaFact]
    public async Task Una_imagen_pequena_se_carga_sin_preguntar_nada()
    {
        var dialogs = new TestDialogService { OpenPath = WritePng("chica.png", 30, 30) };
        var main = new MainWindowViewModel(dialogs);

        await main.LoadBackgroundCommand.ExecuteAsync(null);

        Assert.Single(main.Backgrounds.Images);
        Assert.Single(main.Backgrounds.Tiles);
        Assert.Equal(0, dialogs.CellSizeCalls);

        // Y el selector pide imágenes, no el json de las paletas.
        Assert.Equal(PickerFileKind.Image, dialogs.LastPickerKind);
    }

    [AvaloniaFact]
    public async Task Una_hoja_pregunta_el_tamano_de_celda()
    {
        var dialogs = new TestDialogService { OpenPath = WritePng("hoja.png", 64, 48), CellSize = 16 };
        var main = new MainWindowViewModel(dialogs);

        await main.LoadBackgroundCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.CellSizeCalls);
        Assert.Equal(12, main.Backgrounds.Tiles.Count);

        // El mensaje dice de qué imagen se trata y cuánto mide, que es lo que hace falta
        // para elegir el tamaño con criterio.
        Assert.Contains("hoja.png", dialogs.LastCellSizeMessage);
        Assert.Contains("64x48", dialogs.LastCellSizeMessage);
        Assert.Equal(64, dialogs.LastCellSizeMaximum);
    }

    [AvaloniaFact]
    public async Task Cancelar_el_tamano_de_celda_no_carga_nada()
    {
        var dialogs = new TestDialogService { OpenPath = WritePng("hoja.png", 64, 48), CellSize = null };
        var main = new MainWindowViewModel(dialogs);

        await main.LoadBackgroundCommand.ExecuteAsync(null);

        Assert.Empty(main.Backgrounds.Images);
    }

    [AvaloniaFact]
    public async Task Eliminar_avisa_de_cuantos_fondos_se_lleva()
    {
        var dialogs = new TestDialogService { OpenPath = WritePng("hoja.png", 64, 48), CellSize = 16 };
        var main = new MainWindowViewModel(dialogs);

        await main.LoadBackgroundCommand.ExecuteAsync(null);
        await main.DeleteBackgroundCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ConfirmCalls);
        Assert.Contains("12 fondos", dialogs.LastConfirmMessage);
        Assert.Contains("hoja.png", dialogs.LastConfirmMessage);

        Assert.Empty(main.Backgrounds.Images);
        Assert.Empty(main.Backgrounds.Tiles);
    }

    [AvaloniaFact]
    public async Task Decir_que_no_a_la_confirmacion_deja_la_imagen()
    {
        var dialogs = new TestDialogService
        {
            OpenPath = WritePng("hoja.png", 64, 48),
            CellSize = 16,
            ConfirmAnswer = false,
        };

        var main = new MainWindowViewModel(dialogs);

        await main.LoadBackgroundCommand.ExecuteAsync(null);
        await main.DeleteBackgroundCommand.ExecuteAsync(null);

        Assert.Single(main.Backgrounds.Images);
    }

    [AvaloniaFact]
    public void Eliminar_esta_deshabilitado_sin_ninguna_imagen()
        => Assert.False(new MainWindowViewModel().DeleteBackgroundCommand.CanExecute(null));

    [AvaloniaFact]
    public void Al_eliminar_una_de_dos_la_seleccion_se_va_a_la_otra()
    {
        var main = new MainWindowViewModel();

        ReferenceImage first = main.Backgrounds.Load(WritePng("una.png", 64, 16), cellSize: 16);
        ReferenceImage second = main.Backgrounds.Load(WritePng("otra.png", 30, 30), cellSize: 0);

        Assert.Same(second, main.Backgrounds.SelectedImage);

        main.Backgrounds.Remove(second);

        Assert.Same(first, main.Backgrounds.SelectedImage);
        Assert.True(main.DeleteBackgroundCommand.CanExecute(null));
    }

    /// <summary>
    /// El mismo problema que ya ha dado tres bugs en este proyecto: al desaparecer de la
    /// lista el elemento seleccionado, el <c>ComboBox</c> escribe <c>null</c> en el
    /// ViewModel, y si se le hace caso se deshace la selección que <c>Remove</c> acaba de
    /// mover. Con la propiedad a pelo no se ve: hace falta el control de verdad enlazado.
    /// </summary>
    [AvaloniaFact]
    public void Un_ComboBox_enlazado_no_deshace_la_seleccion_al_eliminar()
    {
        var library = new ReferenceImageLibrary();

        ReferenceImage first = library.Load(WritePng("una.png", 30, 30), cellSize: 0);
        ReferenceImage second = library.Load(WritePng("otra.png", 30, 30), cellSize: 0);

        var combo = new ComboBox { ItemsSource = library.Images };

        combo.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding(nameof(ReferenceImageLibrary.SelectedImage))
            {
                Source = library,
                Mode = BindingMode.TwoWay,
            });

        var window = new Window { Content = combo };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Same(second, combo.SelectedItem);

        library.Remove(second);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(first, library.SelectedImage);
        Assert.Same(first, combo.SelectedItem);

        // Cerrarla no es cortesía: una ventana abierta se queda en la aplicación con su
        // compositor vivo el resto de la serie. Ver «suite-headless-se-cuelga».
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Al_eliminar_la_ultima_ya_no_queda_seleccion()
    {
        var main = new MainWindowViewModel();

        ReferenceImage only = main.Backgrounds.Load(WritePng("una.png", 30, 30), cellSize: 0);
        main.Backgrounds.Remove(only);

        Assert.Null(main.Backgrounds.SelectedImage);
        Assert.False(main.DeleteBackgroundCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Las_imagenes_y_los_fondos_elegidos_sobreviven_a_guardar_y_cargar()
    {
        string sheet = WritePng("hoja.png", 64, 48);
        string bankPath = Path.Combine(_folder, "bicho.json");

        var dialogs = new TestDialogService { SavePath = bankPath, OpenPath = bankPath, CellSize = 16 };
        var main = new MainWindowViewModel(dialogs);

        main.Backgrounds.Load(sheet, cellSize: 16);

        SpritesEditorViewModel editor = main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));
        editor.AddGroupCommand.Execute(null);

        editor.SelectedGroup!.Group.Background = new BackgroundRef(sheet, 5);
        editor.SpritesBank.SpritesList[0].Background = new BackgroundRef(sheet, 7);

        await main.SaveDocumentCommand.ExecuteAsync(null);

        // Una sesión nueva: la biblioteca empieza vacía y se repuebla al abrir el banco.
        var reopened = new MainWindowViewModel(new TestDialogService { OpenPath = bankPath });
        await reopened.OpenCommand.ExecuteAsync(null);

        Assert.Single(reopened.Backgrounds.Images);
        Assert.Equal(12, reopened.Backgrounds.Tiles.Count);

        var loaded = (SpritesEditorViewModel)reopened.Tabs[0];

        Assert.Equal(new BackgroundRef(sheet, 5), loaded.SpritesBank.Groups[0].Background);
        Assert.Equal(new BackgroundRef(sheet, 7), loaded.SpritesBank.SpritesList[0].Background);

        // Y el puntero resuelve contra la biblioteca recién cargada.
        Assert.NotNull(reopened.Backgrounds.Find(loaded.SpritesBank.Groups[0].Background));
    }

    [AvaloniaFact]
    public async Task Si_el_png_ya_no_esta_el_banco_se_abre_igual_y_avisa()
    {
        string sheet = WritePng("hoja.png", 64, 48);
        string bankPath = Path.Combine(_folder, "bicho.json");

        var dialogs = new TestDialogService { SavePath = bankPath, OpenPath = bankPath };
        var main = new MainWindowViewModel(dialogs);

        main.Backgrounds.Load(sheet, cellSize: 16);
        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        await main.SaveDocumentCommand.ExecuteAsync(null);

        File.Delete(sheet);

        var reopened = new MainWindowViewModel(new TestDialogService { OpenPath = bankPath });
        await reopened.OpenCommand.ExecuteAsync(null);

        // Perder una imagen de referencia no puede costarte el banco entero.
        Assert.Single(reopened.Tabs);
        Assert.Empty(reopened.Backgrounds.Images);

        var messages = ((TestDialogService)reopened.Dialogs).Messages;
        Assert.Single(messages);
        Assert.Contains("hoja.png", messages[0]);
    }

    [AvaloniaFact]
    public async Task Un_banco_sin_fondos_no_ensucia_el_fichero()
    {
        string bankPath = Path.Combine(_folder, "bicho.json");
        var dialogs = new TestDialogService { SavePath = bankPath };
        var main = new MainWindowViewModel(dialogs);

        main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho"));

        await main.SaveDocumentCommand.ExecuteAsync(null);

        string json = await File.ReadAllTextAsync(bankPath);

        // Sin fondo no se escribe el puntero, que si no cada patrón y cada grupo
        // arrastrarían un nulo por nada.
        Assert.DoesNotContain("\"background\"", json, StringComparison.OrdinalIgnoreCase);
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
