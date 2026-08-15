using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El cromo de la aplicación responde a la variante clara u oscura, y los colores del
/// dominio no.
/// </summary>
/// <remarks>
/// La variante se pide con un <see cref="ThemeVariantScope"/> alrededor de la vista y no
/// tocando <c>Application.Current</c>: eso último es estado global del proceso y se
/// quedaría puesto para las pruebas siguientes.
/// </remarks>
public class ThemeVariantTests
{
    /// <summary>Los colores del cromo están definidos en las dos variantes, y distintos.</summary>
    [AvaloniaFact]
    public void Cada_color_del_cromo_tiene_su_version_clara_y_su_version_oscura()
    {
        string[] keys =
        [
            "AppPanelBackground",
            "AppSurfaceBackground",
            "AppSeparator",
            "AppControlBorder",
            "AppTextSecondary",
            "AppTextError",
            "AppTextWarning",
            "AppToolButtonBackground",
            "AppToolButtonForeground",
        ];

        foreach (string key in keys)
        {
            Color light = Resolve(key, ThemeVariant.Light);
            Color dark = Resolve(key, ThemeVariant.Dark);

            Assert.True(
                light != dark,
                $"«{key}» vale {light} en las dos variantes: entonces no es un color del cromo.");
        }
    }

    /// <summary>
    /// Y llega hasta el control: un panel montado se pinta distinto según la variante.
    /// </summary>
    /// <remarks>
    /// Comprobar sólo el recurso no diría si la vista lo está usando. Esto se mide sobre
    /// el <c>Background</c> que acaba teniendo el control.
    /// </remarks>
    [AvaloniaFact]
    public void Un_panel_montado_se_pinta_segun_la_variante()
    {
        Color light = PanelBackground(ThemeVariant.Light);
        Color dark = PanelBackground(ThemeVariant.Dark);

        Assert.True(
            light != dark,
            $"El panel se pinta {light} en las dos variantes: no está usando el recurso.");

        // Y en oscuro es oscuro de verdad, no un gris claro con otro nombre.
        Assert.True(
            Brightness(dark) < Brightness(light),
            $"En oscuro el panel sale {dark}, más claro que el {light} de la variante clara.");
    }

    /// <summary>
    /// Los colores del MSX no se mueven con la variante.
    /// </summary>
    /// <remarks>
    /// Es la mitad que importa no romper: un tile tiene que verse igual con el tema que
    /// sea. Si algún día alguien mete la paleta en el diccionario del cromo, esto salta.
    /// </remarks>
    [AvaloniaFact]
    public void La_paleta_del_msx_no_depende_de_la_variante()
    {
        ColorPalette palette = ColorPalette.CreateMsxStandard();

        Color green = palette.GetColor(2);

        Assert.Equal(Color.FromRgb(36, 219, 36), green);
        Assert.Equal("161", palette[2].HexRgb);
    }

    // ------------------------------------------------------------------ las listas

    /// <summary>
    /// Las listas de nombres no se quedan blancas en oscuro.
    /// </summary>
    /// <remarks>
    /// La de capas y la de bloques llevaban el fondo blanco puesto a mano. En oscuro
    /// quedaban blancas mientras Fluent pintaba la letra en blanco, y no se leía nada.
    /// Se comprueba sobre la vista montada porque el fallo era justo ése: el recurso
    /// existía y la vista no lo usaba.
    /// </remarks>
    [AvaloniaFact]
    public void La_lista_de_bloques_no_se_queda_blanca_en_oscuro()
    {
        Color light = BlockListBackground(ThemeVariant.Light);
        Color dark = BlockListBackground(ThemeVariant.Dark);

        // En claro sigue siendo blanca, que es como estaba.
        Assert.Equal(Colors.White, light);

        Assert.True(
            Brightness(dark) < Brightness(light) / 2,
            $"En oscuro la lista sale {dark}: sigue siendo un fondo claro.");
    }

    /// <summary>
    /// La fila seleccionada baja el tono en oscuro, y en claro se queda igual.
    /// </summary>
    /// <remarks>
    /// Fluent pinta la selección con el mismo azul en las dos variantes —#0078D7, medido—
    /// y sobre fondo oscuro ese salto de luminosidad se lleva toda la atención. Lo que se
    /// vigila aquí es que bajarlo en oscuro no se haya llevado por delante el claro.
    /// </remarks>
    [AvaloniaFact]
    public void La_seleccion_baja_el_tono_en_oscuro_sin_tocar_el_claro()
    {
        Color light = SelectionBackground(ThemeVariant.Light);
        Color dark = SelectionBackground(ThemeVariant.Dark);

        Assert.Equal(Color.Parse("#0078D7"), light);

        AssertDoesNotLeapOffThePanel(dark, "La selección");

        // Sigue siendo azul: bajar el tono no es apagarlo hasta que no se vea cuál es.
        Assert.True(dark.B > dark.R && dark.B > dark.G, $"La selección en oscuro sale {dark}, y ya no es azul.");
    }

    /// <summary>
    /// El botón de herramienta pulsado también baja el tono en oscuro, y no en claro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La tira de Estampar/Seleccionar sale justo encima de la lista de capas, así que los
    /// dos azules se ven a la vez y la diferencia cantaba.
    /// </para>
    /// <para>
    /// No se comprueba que valga lo mismo que la selección de las listas, aunque hoy lo
    /// valga: son dos ideas distintas y atarlas con una prueba obligaría a deshacer el
    /// enredo antes de poder tocar una sola de las dos.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void El_boton_pulsado_baja_el_tono_en_oscuro_sin_tocar_el_claro()
    {
        Color light = CheckedToolButton(ThemeVariant.Light);
        Color dark = CheckedToolButton(ThemeVariant.Dark);

        // En claro, el de siempre.
        Assert.Equal(Color.Parse("#3B78FF"), light);

        AssertDoesNotLeapOffThePanel(dark, "El botón pulsado");

        Assert.True(dark.B > dark.R && dark.B > dark.G, $"El botón pulsado sale {dark}, y ya no es azul.");
    }

    // ------------------------------------------------------------------ la azulada

    /// <summary>
    /// La variante azulada vira los grises hacia el azul.
    /// </summary>
    [AvaloniaFact]
    public void La_azulada_tiñe_los_neutros()
    {
        Color dark = Resolve("AppPanelBackground", ThemeVariant.Dark);
        Color blue = Resolve("AppPanelBackground", AppTheme.Blue);

        Assert.True(blue != dark, $"El panel azulado sale {blue}, igual que el oscuro.");

        // Azul de verdad y no un gris con otro nombre: el canal azul manda sobre el rojo.
        Assert.True(
            blue.B > blue.R + 8,
            $"El panel azulado sale {blue}, que no tiene azul suficiente para notarse.");

        // Y sigue siendo oscuro, que hereda de la oscura y no de la clara.
        Assert.True(
            Brightness(blue) < Brightness(Resolve("AppPanelBackground", ThemeVariant.Light)),
            $"El panel azulado sale {blue} y no es más oscuro que el de la variante clara.");
    }

    /// <summary>
    /// Lo que la azulada no declara sale de la oscura, que es de quien hereda.
    /// </summary>
    /// <remarks>
    /// Es lo que hace barato añadir una variante: sólo se declaran los colores que de
    /// verdad cambian. Los semánticos —error, aviso— no se tiñen a propósito: un error
    /// tiene que verse igual de error con el tono que sea. Si esta prueba cae, la herencia
    /// de <c>ThemeVariant</c> no está funcionando y cada variante tendría que repetir la
    /// lista entera.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("AppTextError")]
    [InlineData("AppTextWarning")]
    [InlineData("AppHighlightBackground")]
    [InlineData("AppGlyphAlert")]
    [InlineData("AppSwatchHoverBorder")]
    public void Lo_que_la_azulada_no_declara_lo_hereda_de_la_oscura(string key)
    {
        Assert.Equal(Resolve(key, ThemeVariant.Dark), Resolve(key, AppTheme.Blue));
    }

    /// <summary>
    /// Y la herencia vale también para los recursos del tema base.
    /// </summary>
    /// <remarks>
    /// Fluent sólo conoce la clara y la oscura. Si su búsqueda no siguiera la variante
    /// heredada, con la azulada puesta los controles se quedarían sin sus pinceles y la
    /// ventana saldría en blanco. Se mira sobre un botón montado, que es lo que lo prueba.
    /// </remarks>
    [AvaloniaFact]
    public void Los_controles_de_Fluent_siguen_pintados_en_la_azulada()
    {
        var button = new Button { Content = "Aceptar" };

        Color blue = InScope(button, AppTheme.Blue, mounted => ((Button)mounted).Foreground);
        Color dark = InScope(new Button { Content = "Aceptar" }, ThemeVariant.Dark, m => ((Button)m).Foreground);

        Assert.Equal(dark, blue);
    }

    /// <summary>Y el panel montado con la azulada se pinta de azul, no de gris.</summary>
    [AvaloniaFact]
    public void Un_panel_montado_en_azulado_se_pinta_de_azul()
    {
        Color blue = BlockListBackground(AppTheme.Blue);
        Color dark = BlockListBackground(ThemeVariant.Dark);

        Assert.True(blue != dark, $"La lista azulada sale {blue}, igual que la oscura.");
        Assert.True(blue.B > blue.R + 4, $"La lista azulada sale {blue} y no tiene azul.");
    }

    // ------------------------------------------------------------------ el ajuste

    [AvaloniaTheory]
    [InlineData(AppThemeVariant.System, "Default")]
    [InlineData(AppThemeVariant.Light, "Light")]
    [InlineData(AppThemeVariant.Dark, "Dark")]
    public void Cada_variante_elegida_es_una_de_Avalonia(AppThemeVariant chosen, string expected)
    {
        Assert.Equal(expected, AppTheme.ToAvalonia(chosen).ToString());
    }

    /// <summary>
    /// «El del sistema» no es lo mismo que «claro», aunque hoy se vean igual.
    /// </summary>
    /// <remarks>
    /// Es la que se rompe si alguien resuelve System como Light por atajar: entonces
    /// elegir «el del sistema» dejaría de seguir al escritorio y nadie se enteraría hasta
    /// ponerlo en oscuro.
    /// </remarks>
    [AvaloniaFact]
    public void Seguir_al_sistema_no_es_quedarse_en_claro()
    {
        Assert.NotEqual(
            AppTheme.ToAvalonia(AppThemeVariant.Light),
            AppTheme.ToAvalonia(AppThemeVariant.System));
    }

    [AvaloniaFact]
    public void La_variante_arranca_en_claro_y_va_y_vuelve_del_fichero()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"msxtheme-{Guid.NewGuid():N}");

        try
        {
            var store = new SettingsStore(folder);

            // Sin fichero, claro: a quien actualice no se le cambia el aspecto solo.
            Assert.Equal(AppThemeVariant.Light, new EditorPreferences().ThemeVariant);

            var main = new MainWindowViewModel(new TestDialogService(), store);
            main.Preferences.ThemeVariant = AppThemeVariant.Dark;
            main.SaveSettings();

            var opened = new MainWindowViewModel(new TestDialogService(), store);
            opened.LoadSettings();

            Assert.Equal(AppThemeVariant.Dark, opened.Preferences.ThemeVariant);

            // Y por nombre en el fichero, que se lee a mano cuando algo va mal.
            Assert.Contains("Dark", File.ReadAllText(store.Path));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Aceptar las preferencias deja puesta la variante elegida.</summary>
    [AvaloniaFact]
    public async Task Elegir_el_aspecto_en_preferencias_lo_aplica()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        main.ShowPreferencesCommand.Execute(null);

        var panel = Assert.IsType<EditPreferencesViewModel>(main.RightPanViewModel);

        Assert.Equal(AppThemeVariant.Light, panel.Variant.Value);

        panel.Variant = EditPreferencesViewModel.Variants
            .Single(choice => choice.Value == AppThemeVariant.Dark);

        await panel.AcceptPreferencesCommand.ExecuteAsync(null);

        Assert.Equal(AppThemeVariant.Dark, main.Preferences.ThemeVariant);
    }

    // ------------------------------------------------------------------ ayudas

    private static Color Resolve(string key, ThemeVariant variant)
    {
        Assert.True(
            Application.Current!.TryFindResource(key, variant, out object? value),
            $"No hay recurso «{key}» para la variante {variant}.");

        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    /// <summary>El color con el que acaba pintándose el fondo de un formulario del lateral.</summary>
    private static Color PanelBackground(ThemeVariant variant)
    {
        var main = new MainWindowViewModel();
        var view = new EditPreferencesView { DataContext = new EditPreferencesViewModel(main) };

        var scope = new ThemeVariantScope
        {
            RequestedThemeVariant = variant,
            Child = view,
        };

        var window = new Window { Content = scope, Width = 400, Height = 600 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Panel root = view.GetVisualDescendants().OfType<Panel>().First();
        IBrush? brush = view.Background ?? root.Background;

        window.Close();
        Dispatcher.UIThread.RunJobs();

        return Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
    }

    /// <summary>El fondo con el que acaba pintándose la lista de bloques de tiles.</summary>
    private static Color BlockListBackground(ThemeVariant variant)
    {
        var editor = new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());
        var view = new TileBlocksView { DataContext = new TileBlocksViewModel(editor) };

        return InScope(view, variant, mounted => mounted
            .GetVisualDescendants()
            .OfType<ListBox>()
            .First()
            .Background);
    }

    /// <summary>El fondo de la fila seleccionada de una lista cualquiera.</summary>
    private static Color SelectionBackground(ThemeVariant variant)
    {
        var list = new ListBox { ItemsSource = new[] { "uno", "dos" } };

        return InScope(list, variant, mounted =>
        {
            ((ListBox)mounted).SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            return ((ListBox)mounted).GetRealizedContainers()
                .OfType<ListBoxItem>()
                .Single(row => row.IsSelected)
                .GetVisualDescendants()
                .OfType<ContentPresenter>()
                .First(presenter => presenter.Name == "PART_ContentPresenter")
                .Background;
        });
    }

    /// <summary>El fondo de un botón de herramienta pulsado, con su ControlTheme propio.</summary>
    private static Color CheckedToolButton(ThemeVariant variant)
    {
        var button = new RadioButton
        {
            Content = "X2",
            IsChecked = true,
            Theme = (ControlTheme)Application.Current!.FindResource("ToggleRadioButton")!,
        };

        return InScope(button, variant, mounted => mounted
            .GetVisualDescendants()
            .OfType<Border>()
            .First(border => border.Name == "PART_Root")
            .Background);
    }

    /// <summary>Monta el control con la variante pedida, lo mide y lo cierra.</summary>
    private static Color InScope(Control control, ThemeVariant variant, Func<Control, IBrush?> measure)
    {
        var scope = new ThemeVariantScope { RequestedThemeVariant = variant, Child = control };
        var window = new Window { Content = scope, Width = 500, Height = 700 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        IBrush? brush = measure(control);

        window.Close();
        Dispatcher.UIThread.RunJobs();

        return Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
    }

    /// <summary>
    /// Lo que puede subir un color de señal por encima del panel que tiene detrás.
    /// </summary>
    /// <remarks>
    /// Ochenta y nueve de margen tiene el tono elegido, y doscientos cincuenta y dos tenía
    /// el azul vivo que molestaba; el listón queda cómodamente en medio.
    /// </remarks>
    private const int MaxJumpOverPanel = 150;

    /// <summary>
    /// El color no pega un salto de luminosidad contra el panel oscuro.
    /// </summary>
    /// <remarks>
    /// Es la regla que hay que medir, y no «que el oscuro sea más oscuro que el claro»:
    /// eso ya se cumplía con el azul vivo puesto, así que la prueba pasaba con el fallo
    /// delante. Lo que se veía mal era el salto contra el fondo, no el valor absoluto.
    /// </remarks>
    private static void AssertDoesNotLeapOffThePanel(Color signal, string what)
    {
        int panel = Brightness(Resolve("AppPanelBackground", ThemeVariant.Dark));
        int jump = Brightness(signal) - panel;

        Assert.True(
            jump < MaxJumpOverPanel,
            $"{what} en oscuro sale {signal}, que salta {jump} por encima del panel: "
            + $"el maximo son {MaxJumpOverPanel}.");
    }

    private static int Brightness(Color color) => color.R + color.G + color.B;
}
