using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Material.Icons.Avalonia;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Que los botones de los formularios del lateral se puedan pulsar aunque no quepa todo.
/// </summary>
/// <remarks>
/// Un formulario que crece hacia abajo empuja sus botones fuera de la ventana y deja al
/// usuario sin forma de aceptar ni de cancelar. Pasa con la ventana baja, y sobre todo con
/// la escala de la interfaz al 200%, que es cuando el contenido mide el doble.
/// </remarks>
public class PanelFitTests
{
    /// <summary>Lo bajo que se pone la ventana para que el contenido no quepa seguro.</summary>
    private const int ShortWindow = 300;

    [AvaloniaFact]
    public void El_boton_de_aceptar_preferencias_se_ve_aunque_no_quepa_todo()
    {
        var main = new MainWindowViewModel();

        main.ShowPreferencesCommand.Execute(null);

        var view = new EditPreferencesView { DataContext = main.RightPanViewModel };
        var window = new Window { Content = view, Width = 420, Height = ShortWindow };

        window.Show();
        Pump();

        Button accept = Buttons(view).First(button => IsLabelled(button, "FormAccept"));

        // Dentro del panel, no empujado por debajo del borde.
        double bottom = accept.TranslatePoint(new Point(0, accept.Bounds.Height), view)!.Value.Y;

        Assert.True(
            bottom <= view.Bounds.Height,
            $"El botón de aceptar acaba en {bottom} y el panel mide {view.Bounds.Height}.");

        window.Close();
        Pump();
    }

    /// <summary>Y lo mismo con la escala al máximo, que es cuando se vio.</summary>
    [AvaloniaFact]
    public void Con_la_escala_al_doble_los_botones_siguen_alcanzables()
    {
        var main = new MainWindowViewModel(new TestDialogService { ChooseAnswer = false });

        main.Preferences.InterfaceScale = EditorPreferences.MaxScale;
        main.ShowPreferencesCommand.Execute(null);

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 720 };

        window.Show();
        Pump();

        EditPreferencesView view = window.GetVisualDescendants().OfType<EditPreferencesView>().Single();
        Button accept = Buttons(view).First(button => IsLabelled(button, "FormAccept"));

        double bottom = accept.TranslatePoint(new Point(0, accept.Bounds.Height), view)!.Value.Y;

        Assert.True(
            bottom <= view.Bounds.Height,
            $"El botón de aceptar acaba en {bottom} y el panel mide {view.Bounds.Height}.");

        window.Close();
        Pump();
        Pump();
    }

    /// <summary>
    /// Y lo mismo con todos los formularios del lateral, no sólo con el que se vio.
    /// </summary>
    /// <remarks>
    /// Todos tienen la misma forma —filas fijas, un hueco elástico y los botones al final—
    /// así que todos se rompen igual en cuanto el contenido pasa de lo que hay. Con la
    /// escala al doble, que es el caso peor.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("Preferences")]
    [InlineData("TileSet")]
    [InlineData("SpriteBank")]
    [InlineData("Map")]
    [InlineData("Properties")]
    [InlineData("Resize")]
    [InlineData("Replace")]
    [InlineData("Palette")]
    [InlineData("NewPalette")]
    public void Los_botones_de_los_formularios_caben(string form)
    {
        var main = new MainWindowViewModel(new TestDialogService { ChooseAnswer = false });

        main.Preferences.InterfaceScale = EditorPreferences.MaxScale;

        Open(main, form);

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 720 };

        window.Show();
        Pump();

        Control view = window.GetVisualDescendants()
            .OfType<Control>()
            .First(control => ReferenceEquals(control.DataContext, main.RightPanViewModel)
                              && control is UserControl);

        // El de cerrar en los que no tienen aceptar: lo que importa es poder salir.
        Button exit = Buttons(view).First(button =>
            IsLabelled(button, "FormAccept") || IsLabelled(button, "FormClose"));

        double bottom = exit.TranslatePoint(new Point(0, exit.Bounds.Height), view)!.Value.Y;

        Assert.True(
            bottom <= view.Bounds.Height,
            $"En {form}, el botón acaba en {bottom} y el panel mide {view.Bounds.Height}.");

        window.Close();
        Pump();
        Pump();
    }

    /// <summary>
    /// Ningún rótulo de los formularios sale cortado, en ninguno de los tres idiomas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nace de que «Background» salía «Backgrou» en el panel del mapa: su columna medía 60
    /// fijos, elegidos mirando los rótulos en español —«Nombre», «Tamaño», «Fondo»,
    /// «Vacío»—, todos más cortos. Un ancho fijo lo pone quien escribe la vista, que ve un
    /// idioma; hay treinta columnas así repartidas por los formularios y nadie las había
    /// medido traducidas.
    /// </para>
    /// <para>
    /// Sólo los que se dejan medir solos: si alguien le pone un <c>Width</c> explícito a un
    /// texto, es una decisión suya y no un descuido. Y sólo los de una línea, que los que
    /// envuelven son más estrechos que su texto a propósito.
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("Preferences")]
    [InlineData("TileSet")]
    [InlineData("SpriteBank")]
    [InlineData("Map")]
    [InlineData("Properties")]
    [InlineData("Resize")]
    [InlineData("Replace")]
    [InlineData("Palette")]
    [InlineData("NewPalette")]
    public void Ningun_rotulo_de_los_formularios_sale_cortado(string form)
    {
        string before = Localizer.Instance.Language;
        var cut = new List<string>();

        try
        {
            foreach (string language in (string[])["es", "en", "ca"])
            {
                Localizer.Instance.Language = language;

                var main = new MainWindowViewModel(new TestDialogService { ChooseAnswer = false });

                Open(main, form);

                var window = new MainWindow { DataContext = main, Width = 1400, Height = 900 };

                window.Show();
                Pump();

                Control view = window.GetVisualDescendants()
                    .OfType<Control>()
                    .First(control => ReferenceEquals(control.DataContext, main.RightPanViewModel)
                                      && control is UserControl);

                cut.AddRange(Clipped(view, language));

                window.Close();
                Pump();
                Pump();
            }
        }
        finally
        {
            Localizer.Instance.Language = before;
        }

        Assert.True(cut.Count == 0, $"En {form}: {string.Join(" | ", cut)}");
    }

    /// <summary>Los textos que no caben en el sitio que les ha tocado.</summary>
    private static IEnumerable<string> Clipped(Control view, string language)
    {
        foreach (TextBlock label in view.GetVisualDescendants().OfType<TextBlock>())
        {
            // Los que no se ven no se cortan. Aquí caen las marcas de agua de las cajas de
            // texto —que existen en el árbol aunque la caja tenga contenido— y las ramas
            // que el formulario tiene plegadas, como el otro modo de Sustituir. Sin este
            // filtro salían con ancho cero y parecían todas cortadas.
            if (!label.IsEffectivelyVisible
                || label.TextWrapping != TextWrapping.NoWrap
                || !double.IsNaN(label.Width)
                || string.IsNullOrEmpty(label.Text))
            {
                continue;
            }

            double needed = Unconstrained(label);

            // Medio pixel de gracia: los anchos salen de una medida en punto flotante y una
            // diferencia de redondeo no es un texto cortado.
            if (label.Bounds.Width < needed - 0.5)
                yield return $"en {language}, «{label.Text}» tiene {label.Bounds.Width:0} y pide {needed:0}";
        }
    }

    /// <summary>
    /// Lo que mide el texto sin que nadie lo apriete.
    /// </summary>
    /// <remarks>
    /// El <c>DesiredSize</c> del que está montado ya viene recortado a lo que le dejaron,
    /// así que preguntándole a él siempre parece que cabe.
    /// </remarks>
    private static double Unconstrained(TextBlock label)
    {
        var loose = new TextBlock
        {
            Text = label.Text,
            FontSize = label.FontSize,
            FontFamily = label.FontFamily,
            FontWeight = label.FontWeight,
        };

        loose.Measure(Size.Infinity);

        return loose.DesiredSize.Width;
    }

    /// <summary>
    /// La salida de la escala nunca se cierra: ventana pequeña y escala al doble.
    /// </summary>
    /// <remarks>
    /// Es el peor caso y el único que de verdad importa: si alguien se pasa de escala y no
    /// puede volver, se queda con el programa inservible y sin arreglo desde dentro. Se
    /// comprueban las dos mitades del camino de vuelta —llegar al menú y poder aceptar—,
    /// porque con que falle una ya está encerrado.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(640, 480)]
    [InlineData(800, 600)]
    public void Con_la_escala_al_doble_siempre_se_puede_volver(int width, int height)
    {
        var main = new MainWindowViewModel(new TestDialogService { ChooseAnswer = false });

        main.Preferences.InterfaceScale = EditorPreferences.MaxScale;

        var window = new MainWindow { DataContext = main, Width = width, Height = height };

        window.Show();
        Pump();

        // Preferencias está en Archivo, el primer menú, que es el que nunca se recorta.
        MenuItem file = window.GetLogicalDescendants()
            .OfType<MenuItem>()
            .First(item => (item.Header as string) == Localizer.Instance["MenuFile"]);

        double right = file.TranslatePoint(new Point(file.Bounds.Width, 0), window)!.Value.X;

        Assert.True(right <= window.Bounds.Width, $"El menú Archivo acaba en {right} y la ventana mide {window.Bounds.Width}.");

        main.ShowPreferencesCommand.Execute(null);
        Pump();

        EditPreferencesView view = window.GetVisualDescendants().OfType<EditPreferencesView>().Single();
        Button accept = Buttons(view).First(button => IsLabelled(button, "FormAccept"));

        double bottom = accept.TranslatePoint(new Point(0, accept.Bounds.Height), view)!.Value.Y;

        Assert.True(bottom <= view.Bounds.Height, $"Aceptar acaba en {bottom} y el panel mide {view.Bounds.Height}.");

        window.Close();
        Pump();
        Pump();
    }

    /// <summary>
    /// A 4K y con la escala al doble, los editores se manejan enteros.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 1920x1080 es lo que quedan de un 4K con la escala al 200%, que es la combinación que
    /// tiene sentido: en un 2K no, porque quedarían 1280 y las barras de herramientas no
    /// caben a lo ancho. Esta prueba es la respuesta medida a «¿se puede trabajar a 4K al
    /// 200%?», que a ojo no se sabe.
    /// </para>
    /// <para>
    /// Lo que se comprueba es lo estrecho que se puede quedar sin dejar de funcionar. Que
    /// una barra no quepa a lo ancho no encierra a nadie —se ensancha la ventana y ya—, a
    /// diferencia de los botones de un formulario empujados por debajo del borde, que no se
    /// arreglaban de ninguna manera. Por eso aquí se mide y allí se arregló.
    /// </para>
    /// <para>
    /// Sólo los controles que no cuelgan de una barra de desplazamiento: lo que está dentro
    /// de una puede quedar fuera de la vista con todo el derecho.
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("TileSet")]
    [InlineData("SpriteBank")]
    [InlineData("MapEditor")]
    public void A_4K_con_la_escala_al_doble_los_editores_se_manejan(string editor)
    {
        var main = new MainWindowViewModel(new TestDialogService { ChooseAnswer = false });

        main.Preferences.InterfaceScale = EditorPreferences.MaxScale;

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        if (editor == "SpriteBank")
            main.OpenSpriteBank(new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos"));
        else if (editor == "MapEditor")
            main.OpenMap(new TileMap("Nivel 1", 32, 24), tiles);
        else
            main.SelectedTab = tiles;

        var window = new MainWindow { DataContext = main, Width = 1920, Height = 1080 };

        window.Show();
        Pump();

        var outside = new List<string>();

        foreach (Control control in Reachable(window))
        {
            Point corner = control.TranslatePoint(
                new Point(control.Bounds.Width, control.Bounds.Height), window)!.Value;

            if (corner.X > window.Bounds.Width + 1 || corner.Y > window.Bounds.Height + 1)
                outside.Add($"{Describe(control)} acaba en {corner.X:0},{corner.Y:0}");
        }

        Assert.Empty(outside);

        window.Close();
        Pump();
        Pump();
    }

    /// <summary>Los controles que hay que poder pulsar sin desplazar nada.</summary>
    private static IEnumerable<Control> Reachable(Visual root) =>
        root.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control is Button or ToggleButton or ComboBox)
            .Where(control => control.IsEffectivelyVisible && control.Bounds.Width > 0)
            .Where(control => !control.GetVisualAncestors().OfType<ScrollViewer>().Any());

    private static string Describe(Control control) =>
        $"{control.GetType().Name} «{Label(control)}»";

    /// <summary>Con qué llamar al control en el mensaje.</summary>
    /// <remarks>
    /// Desde que los botones llevan icono, su contenido es un panel y no una cadena: sin
    /// esto el aviso decía «Avalonia.Controls.StackPanel» y no había forma de saber cuál
    /// de ellos se estaba saliendo. Se busca el texto dentro, y si no hay ninguno queda el
    /// icono, que también identifica.
    /// </remarks>
    private static string Label(Control control) => control switch
    {
        ContentControl { Content: string text } => text,
        _ => control.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text
             ?? control.GetVisualDescendants().OfType<MaterialIcon>().FirstOrDefault()?.Kind.ToString()
             ?? control.GetType().Name,
    };

    /// <summary>
    /// Los botones del diálogo crecen con su texto.
    /// </summary>
    /// <remarks>
    /// El texto lo pone quien abre el diálogo y cambia con el idioma: «Guardar y salir» en
    /// español, «Desa i surt» en catalán, «Save and quit» en inglés. Con un ancho fijo se
    /// recortaba —se leía «Guardar y sa»— y en otro idioma se habría recortado en otro
    /// sitio. Se compara con un texto corto en vez de medir píxeles, que depende de la
    /// fuente de cada máquina.
    /// </remarks>
    [AvaloniaFact]
    public void Los_botones_del_dialogo_crecen_con_su_texto()
    {
        Assert.True(
            ConfirmWidth("Guardar y salir sin preguntar otra vez") > ConfirmWidth("Sí"),
            "El botón no crece con el texto: un ancho fijo lo recorta en cuanto se traduce.");
    }

    /// <summary>Lo que mide el botón de confirmar con esa etiqueta.</summary>
    private static double ConfirmWidth(string label)
    {
        var dialog = new ConfirmationWindow("Título", "Mensaje", label, "Otra cosa", threeWay: true);

        dialog.Show();
        Pump();

        double width = dialog.GetVisualDescendants()
            .OfType<Button>()
            .First(button => (button.Content as string) == label)
            .Bounds.Width;

        dialog.Close();
        Pump();

        return width;
    }

    /// <summary>Abre cada formulario por donde lo abre el usuario.</summary>
    private static void Open(MainWindowViewModel main, string form)
    {
        switch (form)
        {
            case "Preferences":
                main.ShowPreferencesCommand.Execute(null);
                break;

            case "TileSet":
                main.AddTileSetCommand.Execute(null);
                break;

            case "SpriteBank":
                main.AddSpriteBankCommand.Execute(null);
                break;

            // El formulario de crear, que pregunta el nombre y de cuál se copia.
            case "NewPalette":
                main.AddPaletteCommand.Execute(null);
                break;

            // Y el editor, que sale al aceptarlo.
            case "Palette":
                TestPalette.Create(main);
                break;

            case "Properties":
                main.OpenTileSet(new TileSet("Bosque"));
                main.ShowPropertiesCommand.Execute(
                    main.TreeGeneralVm.PrimaryNodes.SelectMany(node => node.Childs).First());

                break;

            default:
                TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

                if (form == "Map")
                {
                    main.AddMapCommand.Execute(null);
                    break;
                }

                main.OpenMap(new TileMap("Nivel 1", 32, 24), tiles);

                if (form == "Resize")
                    main.ResizeMapCommand.Execute(null);
                else
                    main.ReplaceTilesCommand.Execute(null);

                break;
        }
    }

    private static IEnumerable<Button> Buttons(Visual view) => view.GetVisualDescendants().OfType<Button>();

    /// <summary>Por el texto traducido, que es lo que el usuario ve en el botón.</summary>
    private static bool IsLabelled(Button button, string key) =>
        (button.Content as string) == Localizer.Instance[key];

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
