using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_SpritesEditor;
using MSX_SpritesEditor.Entities;
using MSX_SpritesEditor.ViewModels;
using MSX_SpritesEditor.Views;
using Xunit;

namespace MSX_SpritesEditor.Tests;

public class PaletteEditingTests
{
    [Fact]
    public void Cambiar_una_componente_actualiza_color_y_hex()
    {
        var color = new PaletteColor(5, "Light blue", 2, 3, 7);

        color.Red = 7;

        Assert.Equal(7, color.Red);
        Assert.Equal(255, color.Color.R);
        Assert.Equal("737", color.HexRgb);
    }

    [Fact]
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

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    [InlineData(9, 7)]
    public void Las_componentes_se_limitan_al_rango_del_msx(int value, int expected)
    {
        var color = new PaletteColor(5, "Light blue", 0, 0, 0) { Red = value };

        Assert.Equal(expected, color.Red);
    }

    [Fact]
    public void Cambiar_el_color_descarta_el_nombre_heredado()
    {
        ColorPalette palette = new PaletteLibrary().Add();
        Assert.Equal("Light green", palette[3].Name);

        palette[3].Red = 7;

        // "Light green" describía el color de la paleta original: ya no es cierto.
        Assert.Equal(string.Empty, palette[3].Name);
        Assert.Equal("Color 3", palette[3].DisplayName);
    }

    [Fact]
    public void Un_nombre_puesto_a_mano_sobrevive_a_los_cambios_de_color()
    {
        ColorPalette palette = new PaletteLibrary().Add();

        palette[3].Name = "piel";
        palette[3].SetComponents(7, 5, 4);
        palette[3].Blue = 0;

        Assert.Equal("piel", palette[3].Name);
        Assert.Equal("piel", palette[3].DisplayName);
    }

    [Fact]
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

    [Fact]
    public void Un_color_sin_nombre_se_muestra_por_su_indice()
    {
        var color = new PaletteColor(10, string.Empty, 1, 2, 3);

        Assert.Equal("Color A", color.DisplayName);

        color.Name = "sombra";

        Assert.Equal("sombra", color.DisplayName);
    }

    [Fact]
    public void La_paleta_estandar_conserva_sus_nombres()
    {
        ColorPalette standard = ColorPalette.CreateMsxStandard();

        // Es de solo lectura, asi que sus nombres nunca llegan a quedarse obsoletos.
        Assert.True(standard.IsReadOnly);
        Assert.Equal("Light green", standard[3].Name);
        Assert.Equal("Light green", standard[3].DisplayName);
    }

    [Fact]
    public void El_color_0_es_transparente_y_no_editable()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        Assert.True(palette[0].IsTransparent);
        Assert.False(palette[0].IsEditable);
        Assert.True(palette[1].IsEditable);
    }

    [Fact]
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
    public void Editar_la_paleta_activa_repinta_las_miniaturas()
    {
        var palettes = new PaletteLibrary();
        ColorPalette editable = palettes.Add();
        var vm = new SpritesEditorViewModel(new SpriteBank(SpriteBank.SpriteType.MSX2), palettes);

        vm.CurrentSprite.ArraySpriteRows[0].ArrayColumns[0] = true;
        vm.RowColors[0].PickCommand.Execute(editable[3]); // verde claro

        editable[3].SetComponents(7, 0, 0); // ahora rojo

        Assert.Equal(PixelReader.Bgra(editable[3].Color), PixelReader.At(vm.CurrentSprite.ImageMini!, 0, 0));
    }

    [AvaloniaFact]
    public void Cambiar_de_paleta_activa_repinta_las_miniaturas()
    {
        var palettes = new PaletteLibrary();
        ColorPalette standard = palettes.ActivePalette;
        var vm = new SpritesEditorViewModel(new SpriteBank(SpriteBank.SpriteType.MSX2), palettes);

        vm.CurrentSprite.ArraySpriteRows[0].ArrayColumns[0] = true;
        vm.RowColors[0].PickCommand.Execute(standard[3]);

        ColorPalette other = palettes.Add();   // copia, y pasa a ser la activa
        other[3].SetComponents(7, 0, 7);       // magenta chillón

        Assert.Same(other, vm.ColorPalette);
        Assert.Equal(PixelReader.Bgra(other[3].Color), PixelReader.At(vm.CurrentSprite.ImageMini!, 0, 0));
    }

    [Fact]
    public void Los_comandos_de_paleta_respetan_la_estandar()
    {
        var main = new MainWindowViewModel();

        Assert.False(main.EditPaletteCommand.CanExecute(null));
        Assert.False(main.DeletePaletteCommand.CanExecute(null));

        main.AddPaletteCommand.Execute(null);

        Assert.True(main.EditPaletteCommand.CanExecute(null));
        Assert.True(main.DeletePaletteCommand.CanExecute(null));
    }

    [Fact]
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

    /// <summary>Monta el panel de edición como lo haría la aplicación, vía ViewLocator.</summary>
    private sealed class PaletteEditorHost : IDisposable
    {
        private readonly Window _window;

        private PaletteEditorHost(Window window, List<Slider> sliders)
        {
            _window = window;
            Sliders = sliders;
        }

        public List<Slider> Sliders { get; }

        public static PaletteEditorHost Show(EditPaletteViewModel vm)
        {
            // Un ContentControl con el ViewModel dentro, igual que el panel derecho de
            // la ventana principal: asi se ejerce tambien el ViewLocator, que es quien
            // resuelve la vista y le pone el DataContext.
            var window = new Window
            {
                Content = new ContentControl { Content = vm },
                Width = 360,
                Height = 700,
            };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            EditPaletteView view = window.GetVisualDescendants().OfType<EditPaletteView>().Single();

            return new PaletteEditorHost(window, [.. view.GetVisualDescendants().OfType<Slider>()]);
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
    public void Eliminar_la_paleta_activa_con_la_ventana_montada_no_deja_la_seleccion_vacia()
    {
        using MainWindowHost app = MainWindowHost.Show();

        app.ViewModel.AddPaletteCommand.Execute(null); // Palette 1
        app.ViewModel.AddPaletteCommand.Execute(null); // Palette 2, activa
        app.Pump();

        Assert.Same(app.ViewModel.Palettes.ActivePalette, app.PaletteCombo.SelectedItem);

        app.ViewModel.DeletePaletteCommand.Execute(null);
        app.Pump();

        Assert.NotNull(app.ViewModel.Palettes.ActivePalette);
        Assert.Equal("Palette 1", app.ViewModel.Palettes.ActivePalette.Name);
        Assert.Same(app.ViewModel.Palettes.ActivePalette, app.PaletteCombo.SelectedItem);
    }

    [AvaloniaFact]
    public void Eliminar_hasta_quedarse_con_la_estandar_deshabilita_los_comandos()
    {
        using MainWindowHost app = MainWindowHost.Show();

        app.ViewModel.AddPaletteCommand.Execute(null);
        app.ViewModel.DeletePaletteCommand.Execute(null);
        app.Pump();

        Assert.Single(app.ViewModel.Palettes.Palettes);
        Assert.Equal(ColorPalette.StandardName, app.ViewModel.Palettes.ActivePalette.Name);
        Assert.False(app.ViewModel.EditPaletteCommand.CanExecute(null));
        Assert.False(app.ViewModel.DeletePaletteCommand.CanExecute(null));
        Assert.Same(app.ViewModel.Palettes.ActivePalette, app.PaletteCombo.SelectedItem);
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

    [Fact]
    public void Eliminar_la_paleta_que_se_esta_editando_cierra_el_panel()
    {
        var main = new MainWindowViewModel();
        main.AddPaletteCommand.Execute(null);

        main.DeletePaletteCommand.Execute(null);

        Assert.Null(main.RightPanViewModel);
        Assert.Equal(ColorPalette.StandardName, main.Palettes.ActivePalette.Name);
    }
}
