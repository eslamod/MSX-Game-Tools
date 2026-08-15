using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

public class PaletteEditingTests
{
    /// <summary>Aire que se le exige entre el número de un slider y la barra.</summary>
    private const double MinimumGap = 4;

    [AvaloniaFact]
    public void Cambiar_una_componente_actualiza_color_y_hex()
    {
        var color = new PaletteColor(5, "Light blue", 2, 3, 7);

        color.Red = 7;

        Assert.Equal(7, color.Red);
        Assert.Equal(255, color.Color.R);
        Assert.Equal("737", color.HexRgb);
    }

    [AvaloniaFact]
    public void El_brush_es_siempre_el_mismo_objeto()
    {
        var color = new PaletteColor(5, "Light blue", 2, 3, 7);
        object before = color.Brush;

        color.Green = 0;

        // Mutar el brush en vez de sustituirlo es lo que hace que el lienzo y las
        // casillas se repinten solos, sin notificar a nadie.
        Assert.Same(before, color.Brush);
        Assert.Equal(0, color.Color.G);
    }

    [AvaloniaTheory]
    [InlineData(-3, 0)]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    [InlineData(9, 7)]
    public void Las_componentes_se_limitan_al_rango_del_msx(int value, int expected)
    {
        var color = new PaletteColor(5, "Light blue", 0, 0, 0) { Red = value };

        Assert.Equal(expected, color.Red);
    }

    /// <summary>
    /// Una paleta nueva sale sin nombres, aunque los colores sean los del MSX.
    /// </summary>
    /// <remarks>
    /// Los nombres del MSX son de la máquina: «Light green» es <b>el</b> color 3 del
    /// TMS9918, no una etiqueta. En una paleta propia el 3 acaba siendo cualquier otra
    /// cosa, así que arrastrar ese nombre sólo sirve para que mienta en cuanto lo tocas.
    /// Los nombres que salgan aquí los ha escrito el usuario, y por eso no se tiran nunca.
    /// </remarks>
    [AvaloniaFact]
    public void Una_paleta_nueva_no_hereda_los_nombres_del_msx()
    {
        ColorPalette palette = new PaletteLibrary().Add();

        Assert.All(palette.Colors, color => Assert.Equal(string.Empty, color.Name));
        Assert.Equal("Color 3", palette[3].DisplayName);

        // Y los colores sí son los del MSX: es sólo el nombre lo que no se copia.
        Assert.Equal("373", palette[3].HexRgb);
    }

    /// <summary>Copiar una paleta tuya sí se lleva los nombres: los escribiste tú.</summary>
    [AvaloniaFact]
    public void Copiar_una_paleta_propia_se_lleva_sus_nombres()
    {
        var library = new PaletteLibrary();

        ColorPalette mine = library.Add("Nocturna");
        mine[3].Name = "Verde del bosque";

        ColorPalette copy = mine.Clone("Nocturna 2");

        Assert.Equal("Verde del bosque", copy[3].Name);
    }

    /// <summary>La paleta del MSX no se toca, y no depende de que nadie se acuerde.</summary>
    [AvaloniaFact]
    public void La_paleta_del_msx_no_admite_cambios()
    {
        ColorPalette standard = ColorPalette.CreateMsxStandard();

        standard[3].Red = 0;
        standard[3].Name = "otro";
        standard[3].SetComponents(0, 0, 0);

        Assert.Equal("373", standard[3].HexRgb);
        Assert.Equal("Light green", standard[3].Name);
    }

    [AvaloniaFact]
    public void Un_nombre_puesto_a_mano_sobrevive_a_los_cambios_de_color()
    {
        ColorPalette palette = new PaletteLibrary().Add();

        palette[3].Name = "piel";
        palette[3].SetComponents(7, 5, 4);
        palette[3].Blue = 0;

        Assert.Equal("piel", palette[3].Name);
        Assert.Equal("piel", palette[3].DisplayName);
    }

    [AvaloniaFact]
    public void El_caracter_del_nombre_se_conserva_al_copiar_la_paleta()
    {
        var library = new PaletteLibrary();
        ColorPalette first = library.Add();
        first[3].Name = "piel";     // propio
        // first[4] conserva su nombre heredado

        ColorPalette second = library.Add();

        second[3].Red = 0;
        second[4].Red = 0;

        Assert.Equal("piel", second[3].Name);
        Assert.Equal(string.Empty, second[4].Name);
    }

    [AvaloniaFact]
    public void Un_color_sin_nombre_se_muestra_por_su_indice()
    {
        var color = new PaletteColor(10, string.Empty, 1, 2, 3);

        Assert.Equal("Color A", color.DisplayName);

        color.Name = "sombra";

        Assert.Equal("sombra", color.DisplayName);
    }

    [AvaloniaFact]
    public void La_paleta_estandar_conserva_sus_nombres()
    {
        ColorPalette standard = ColorPalette.CreateMsxStandard();

        // Es de solo lectura, asi que sus nombres nunca llegan a quedarse obsoletos.
        Assert.True(standard.IsReadOnly);
        Assert.Equal("Light green", standard[3].Name);
        Assert.Equal("Light green", standard[3].DisplayName);
    }

    [AvaloniaFact]
    public void El_color_0_es_transparente_y_no_editable()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        Assert.True(palette[0].IsTransparent);
        Assert.False(palette[0].IsEditable);
        Assert.True(palette[1].IsEditable);
    }

    [AvaloniaFact]
    public void La_paleta_avisa_cuando_cambia_uno_de_sus_colores()
    {
        ColorPalette palette = new PaletteLibrary().Add();
        int notifications = 0;
        palette.ColorsChanged += _ => notifications++;

        palette[3].Blue = 1;
        palette[3].Blue = 1; // mismo valor: no vuelve a avisar
        palette[4].Red = 0;

        Assert.Equal(2, notifications);
    }

    [AvaloniaFact]
    public void Editar_la_paleta_del_banco_repinta_las_miniaturas()
    {
        ColorPalette editable = new PaletteLibrary().Add();
        var vm = new SpritesEditorViewModel(new SpriteBank(SpriteBank.SpriteType.MSX2), editable);

        vm.CurrentSprite.ArraySpriteRows[0].ArrayColumns[0] = true;
        vm.RowColors[0].PickCommand.Execute(editable[3]); // verde claro

        editable[3].SetComponents(7, 0, 0); // ahora rojo

        Assert.Equal(PixelReader.Bgra(editable[3].Color), PixelReader.At(vm.CurrentSprite.ImageMini!, 0, 0));
    }

    [AvaloniaFact]
    public void Cambiar_la_paleta_del_banco_repinta_las_miniaturas()
    {
        ColorPalette standard = ColorPalette.CreateMsxStandard();
        var vm = new SpritesEditorViewModel(new SpriteBank(SpriteBank.SpriteType.MSX2), standard);

        vm.CurrentSprite.ArraySpriteRows[0].ArrayColumns[0] = true;
        vm.RowColors[0].PickCommand.Execute(standard[3]);

        ColorPalette other = standard.Clone("Otra");
        other[3].SetComponents(7, 0, 7);       // magenta chillón

        vm.ColorPalette = other;

        Assert.Same(other, vm.ColorPalette);
        Assert.Equal(PixelReader.Bgra(other[3].Color), PixelReader.At(vm.CurrentSprite.ImageMini!, 0, 0));
    }

    [AvaloniaFact]
    public void Los_comandos_de_paleta_respetan_la_estandar()
    {
        var main = new MainWindowViewModel();

        Assert.False(main.EditPaletteCommand.CanExecute(null));
        Assert.False(main.DeletePaletteCommand.CanExecute(null));

        main.AddPaletteCommand.Execute(null);

        Assert.True(main.EditPaletteCommand.CanExecute(null));
        Assert.True(main.DeletePaletteCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Crear_una_paleta_abre_su_editor_en_el_panel_derecho()
    {
        var main = new MainWindowViewModel();

        main.AddPaletteCommand.Execute(null);

        var editor = Assert.IsType<EditPaletteViewModel>(main.RightPanViewModel);
        Assert.Same(main.Palettes.ActivePalette, editor.Palette);

        // Arranca en el color 1: el 0 es transparente y no se puede editar.
        Assert.Equal(1, editor.SelectedColor.Index);
    }

    [AvaloniaFact]
    public void El_panel_de_edicion_refleja_y_aplica_los_sliders()
    {
        var main = new MainWindowViewModel();
        main.AddPaletteCommand.Execute(null);
        var vm = (EditPaletteViewModel)main.RightPanViewModel!;

        using PaletteEditorHost host = PaletteEditorHost.Show(vm);

        Assert.Equal(3, host.Sliders.Count);

        vm.SelectedColor = vm.Palette[5]; // Light blue: 2,3,7
        host.Pump();

        Assert.Equal(2, host.Sliders[0].Value);
        Assert.Equal(3, host.Sliders[1].Value);
        Assert.Equal(7, host.Sliders[2].Value);

        // Y al revés: mover el slider cambia el color de la paleta.
        host.Sliders[0].Value = 7;
        host.Pump();

        Assert.Equal(7, vm.Palette[5].Red);
        Assert.Equal("737", vm.Palette[5].HexRgb);
    }

    [AvaloniaFact]
    public void Los_sliders_se_deshabilitan_en_el_color_transparente()
    {
        var main = new MainWindowViewModel();
        main.AddPaletteCommand.Execute(null);
        var vm = (EditPaletteViewModel)main.RightPanViewModel!;

        using PaletteEditorHost host = PaletteEditorHost.Show(vm);

        vm.SelectedColor = vm.Palette[1];
        host.Pump();
        Assert.All(host.Sliders, s => Assert.True(s.IsEffectivelyEnabled));

        vm.SelectedColor = vm.Palette[0]; // transparente
        host.Pump();
        Assert.All(host.Sliders, s => Assert.False(s.IsEffectivelyEnabled));
    }

    /// <summary>
    /// La barra de desplazamiento no puede taparle los números a los sliders, ni pegarse.
    /// </summary>
    /// <remarks>
    /// Se dibuja encima del contenido, y los valores de R, G y B van pegados al borde
    /// derecho, así que quedaban debajo. Se mide en vez de mirarlo: el ancho de la barra lo
    /// pone el tema y no es un número que se pueda dar por sabido. Y no basta con que no se
    /// toquen: la barra engorda al agarrarla, y con el hueco justo el número quedaba a ras.
    /// </remarks>
    [AvaloniaFact]
    public void La_barra_no_tapa_los_valores_de_los_sliders()
    {
        var main = new MainWindowViewModel();
        main.AddPaletteCommand.Execute(null);

        using PaletteEditorHost host = PaletteEditorHost.Show(
            (EditPaletteViewModel)main.RightPanViewModel!, height: 420);

        // La que se ve, que hay mas de una: el ListBox tiene la suya aunque este apagada.
        ScrollBar bar = Assert.Single(
            host.View.GetVisualDescendants().OfType<ScrollBar>(),
            scroll => scroll.Orientation == Orientation.Vertical && scroll.IsVisible);

        double barLeft = bar.TranslatePoint(new Point(0, 0), host.View)!.Value.X;

        // Los numeros de los sliders son los Consolas que van a la derecha de cada uno.
        foreach (Slider slider in host.Sliders)
        {
            Grid row = slider.FindAncestorOfType<Grid>()!;

            TextBlock value = row.GetVisualDescendants()
                .OfType<TextBlock>()
                .Last(text => text.Bounds.Width > 0);

            double right = value.TranslatePoint(new Point(value.Bounds.Width, 0), host.View)!.Value.X;

            Assert.True(
                right <= barLeft - MinimumGap,
                $"El valor acaba en {right:0} y la barra empieza en {barLeft:0}: "
                + $"quedan {barLeft - right:0} px y hacen falta {MinimumGap}.");
        }
    }

    private sealed class PaletteEditorHost : IDisposable
    {
        private readonly Window _window;

        private PaletteEditorHost(Window window, List<Slider> sliders)
        {
            _window = window;
            Sliders = sliders;
        }

        public List<Slider> Sliders { get; }

        /// <summary>El panel montado, para medirlo.</summary>
        public EditPaletteView View { get; private set; } = null!;

        /// <param name="height">
        /// Alto de la ventana. Con el de por omisión el panel cabe entero y no sale barra;
        /// para lo que la barra tape hay que apretarlo.
        /// </param>
        public static PaletteEditorHost Show(EditPaletteViewModel vm, double height = 700)
        {
            // Un ContentControl con el ViewModel dentro, igual que el panel derecho de
            // la ventana principal: asi se ejerce tambien el ViewLocator, que es quien
            // resuelve la vista y le pone el DataContext.
            var window = new Window
            {
                Content = new ContentControl { Content = vm },
                Width = 360,
                Height = height,
            };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            EditPaletteView view = window.GetVisualDescendants().OfType<EditPaletteView>().Single();

            return new PaletteEditorHost(window, [.. view.GetVisualDescendants().OfType<Slider>()])
            {
                View = view,
            };
        }

        public void Pump() => Dispatcher.UIThread.RunJobs();

        public void Dispose()
        {
            _window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    // Con la ventana montada de verdad: el ComboBox enlazado a la paleta activa
    // escribe null en cuanto el elemento seleccionado desaparece de la coleccion, y
    // eso reventaba el CanExecute de los comandos. Probando el ViewModel suelto no
    // se reproduce.

    [AvaloniaFact]
    public async Task Eliminar_la_paleta_activa_con_la_ventana_montada_no_deja_la_seleccion_vacia()
    {
        using MainWindowHost app = MainWindowHost.Show();

        app.ViewModel.AddPaletteCommand.Execute(null); // Palette 1
        app.ViewModel.AddPaletteCommand.Execute(null); // Palette 2, activa
        app.Pump();

        Assert.Same(app.ViewModel.Palettes.ActivePalette, app.PaletteCombo.SelectedItem);

        await app.ViewModel.DeletePaletteCommand.ExecuteAsync(null);
        app.Pump();

        Assert.NotNull(app.ViewModel.Palettes.ActivePalette);
        Assert.Equal("Palette 1", app.ViewModel.Palettes.ActivePalette.Name);
        Assert.Same(app.ViewModel.Palettes.ActivePalette, app.PaletteCombo.SelectedItem);
    }

    [AvaloniaFact]
    public async Task Eliminar_hasta_quedarse_con_la_estandar_deshabilita_los_comandos()
    {
        using MainWindowHost app = MainWindowHost.Show();

        app.ViewModel.AddPaletteCommand.Execute(null);
        await app.ViewModel.DeletePaletteCommand.ExecuteAsync(null);
        app.Pump();

        Assert.Single(app.ViewModel.Palettes.Palettes);
        Assert.Equal(ColorPalette.StandardName, app.ViewModel.Palettes.ActivePalette.Name);
        Assert.False(app.ViewModel.EditPaletteCommand.CanExecute(null));
        Assert.False(app.ViewModel.DeletePaletteCommand.CanExecute(null));
        Assert.Same(app.ViewModel.Palettes.ActivePalette, app.PaletteCombo.SelectedItem);
    }

    /// <summary>
    /// Ninguna entrada de menú puede no hacer nada.
    /// </summary>
    /// <remarks>
    /// Un nombre de comando mal escrito en el XAML deja <c>Command</c> en null y la opción
    /// no hace nada, sin ningún aviso. Antes esto iba con una lista de textos escrita a
    /// mano, que había que mantener y que sólo cubría los que alguien se acordó de poner;
    /// así se comprueban todas, y sin depender de lo que digan, que ahora además cambia
    /// con el idioma.
    /// </remarks>
    [AvaloniaFact]
    public void Ninguna_opcion_de_menu_se_queda_sin_hacer_nada()
    {
        using MainWindowHost app = MainWindowHost.Show();

        // Las de primer nivel sólo despliegan, y Salir cierra la ventana desde el
        // code-behind, que no es un comando.
        List<MenuItem> dead =
        [
            .. app.MenuItems.Where(item =>
                item.ItemCount == 0
                && item.Command is null
                && (item.Header as string) != Localizer.Instance["MenuExit"]),
        ];

        Assert.Empty(dead.Select(item => item.Header as string));
    }

    /// <summary>Y ninguna se queda sin texto, que es lo que pasa si falta la traducción.</summary>
    [AvaloniaFact]
    public void Ninguna_opcion_de_menu_se_queda_sin_texto()
    {
        using MainWindowHost app = MainWindowHost.Show();

        Assert.All(
            app.MenuItems,
            item => Assert.False(string.IsNullOrWhiteSpace(item.Header as string)));
    }

    /// <summary>La ventana principal montada de verdad, con sus enlaces vivos.</summary>
    private sealed class MainWindowHost : IDisposable
    {
        private readonly MainWindow _window;

        private MainWindowHost(MainWindow window, MainWindowViewModel viewModel, ComboBox paletteCombo)
        {
            _window = window;
            ViewModel = viewModel;
            PaletteCombo = paletteCombo;
        }

        public MainWindowViewModel ViewModel { get; }

        public ComboBox PaletteCombo { get; }

        /// <summary>Los MenuItem se declaran en el XAML, así que están en el árbol lógico.</summary>
        public IEnumerable<MenuItem> MenuItems =>
            _window.GetLogicalDescendants().OfType<MenuItem>();

        public static MainWindowHost Show()
        {
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow { DataContext = viewModel };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            ComboBox combo = window.GetVisualDescendants().OfType<ComboBox>().First();

            return new MainWindowHost(window, viewModel, combo);
        }

        public void Pump() => Dispatcher.UIThread.RunJobs();

        public void Dispose()
        {
            _window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task Eliminar_la_paleta_que_se_esta_editando_cierra_el_panel()
    {
        var main = new MainWindowViewModel();
        main.AddPaletteCommand.Execute(null);

        await main.DeletePaletteCommand.ExecuteAsync(null);

        Assert.Null(main.RightPanViewModel);
        Assert.Equal(ColorPalette.StandardName, main.Palettes.ActivePalette.Name);
    }

    [AvaloniaFact]
    public async Task Eliminar_pide_confirmacion_nombrando_la_paleta()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = true };
        var main = new MainWindowViewModel(dialogs);
        main.AddPaletteCommand.Execute(null);
        main.Palettes.ActivePalette.Name = "Nocturna";

        await main.DeletePaletteCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ConfirmCalls);
        Assert.Contains("Nocturna", dialogs.LastConfirmMessage);
        Assert.Equal("Eliminar", dialogs.LastConfirmLabel);
        Assert.Single(main.Palettes.Palettes);
    }

    [AvaloniaFact]
    public async Task Cancelar_la_confirmacion_no_elimina_nada()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = false };
        var main = new MainWindowViewModel(dialogs);
        main.AddPaletteCommand.Execute(null);
        ColorPalette created = main.Palettes.ActivePalette;

        await main.DeletePaletteCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ConfirmCalls);
        Assert.Equal(2, main.Palettes.Palettes.Count);
        Assert.Same(created, main.Palettes.ActivePalette);

        // Y el panel de edición sigue abierto sobre ella.
        var editor = Assert.IsType<EditPaletteViewModel>(main.RightPanViewModel);
        Assert.Same(created, editor.Palette);
    }

}
