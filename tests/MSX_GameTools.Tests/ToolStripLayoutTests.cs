using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
/// Las tiras de herramientas: que el texto de los botones quepa y que la línea de la
/// pestaña seleccionada no se meta encima del texto.
/// </summary>
public class ToolStripLayoutTests : IDisposable
{
    /// <summary>Pixeles de aire que se le exigen al texto dentro de su botón.</summary>
    /// <remarks>
    /// No basta con que quepa justo. Antes cabía con cero de margen —el hueco medía 16 y
    /// el texto pedía 16.00— y en pantalla salía cortado: cualquier redondeo o una fuente
    /// un pelo más ancha se lo come.
    /// </remarks>
    private const double MinimumSlack = 3;

    /// <summary>Lo que ocupan dos cifras más el relleno de la caja.</summary>
    /// <remarks>
    /// Dos porque el tamaño de un bloque llega a 16, y con una sola cifra visible «16» se
    /// leería «1».
    /// </remarks>
    private const double TwoDigits = 28;

    /// <summary>Lo que se le exige a la caja del nombre del bloque.</summary>
    /// <remarks>
    /// Da para «Arbol 2» y su relleno. Es una caja de texto y se desplaza sola con nombres
    /// largos, así que lo que importa es que no se quede en un sello.
    /// </remarks>
    private const double ShortName = 80;

    private readonly Window _window;
    private readonly MapEditorView _view;

    public ToolStripLayoutTests()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));
        MapEditorViewModel editor = main.OpenMap(new TileMap("Nivel", 16, 16), tiles);

        _view = new MapEditorView { DataContext = editor };
        _window = new Window { Content = _view, Width = 1100, Height = 800 };

        _window.Show();
        Dispatcher.UIThread.RunJobs();
    }

    public void Dispose()
    {
        _window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Un botón que sólo lleva icono tiene que explicarse al pasar el ratón.
    /// </summary>
    /// <remarks>
    /// Es lo que sostiene la decisión de quitarles el texto: sin la ayuda emergente, un
    /// icono que no reconoces no tiene ninguna otra forma de decirte qué hace. Y es lo que
    /// se olvida al añadir el botón número trece.
    /// </remarks>
    [AvaloniaFact]
    public void Todo_boton_que_solo_lleva_icono_se_explica_al_pasar_el_raton()
    {
        AssertIconsExplainThemselves(_view, atLeast: 9);
    }

    /// <summary>Y lo mismo en el editor de sprites, que es donde faltaban.</summary>
    /// <remarks>
    /// De los dieciséis botones que allí eran glifos de texto, diez no tenían ayuda
    /// emergente —entre ellos la equis roja que borra un sprite, que además es la única
    /// destructiva de la tira—. Convertirlos a icono obligó a escribirlas.
    /// </remarks>
    [AvaloniaFact]
    public void En_el_editor_de_sprites_los_iconos_tambien_se_explican()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        var view = new SpritesEditorView
        {
            DataContext = new SpritesEditorViewModel(bank, ColorPalette.CreateMsxStandard()),
        };

        var window = new Window { Content = view, Width = 1400, Height = 900 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Los cuatro de navegación del banco están siempre; los de grupo salen al cambiar
        // de modo, así que no se exigen aquí.
        AssertIconsExplainThemselves(view, atLeast: 4);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Y en el editor de tiles.</summary>
    [AvaloniaFact]
    public void En_el_editor_de_tiles_los_iconos_tambien_se_explican()
    {
        using var editor = new MountedTileSet();

        AssertIconsExplainThemselves(editor.View, atLeast: 4);
    }

    /// <summary>
    /// Los rótulos del miembro del grupo caben en su columna, en los tres idiomas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Esa columna mide 56 y el rótulo «Desplazamiento X» pedía 101: salía cortado en
    /// «Desplaza» y la flecha se le venía encima. Ahora pone «Desp. X», que es lo que dice
    /// el usuario que hace falta para que quepa; lo que se pierde de la palabra entera
    /// está en la ayuda emergente de las flechas, que ya explican el movimiento.
    /// </para>
    /// <para>
    /// Por idioma porque el catalán es el más largo de los tres y es el que se olvida:
    /// una abreviatura que cabe en español puede no caber traducida.
    /// </para>
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("es")]
    [InlineData("en")]
    [InlineData("ca")]
    public void Los_rotulos_del_miembro_caben_en_su_columna(string language)
    {
        string before = Localizer.Instance.Language;

        try
        {
            Localizer.Instance.Language = language;

            var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
            var view = new SpritesEditorView
            {
                DataContext = new SpritesEditorViewModel(bank, ColorPalette.CreateMsxStandard()),
            };

            var window = new Window { Content = view, Width = 1400, Height = 900 };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            // Por el conmutador y no por la propiedad del ViewModel: el panel del grupo
            // sólo existe si se llega a él como se llega en la aplicación.
            var groups = (RadioButton)view.FindControl<RadioButton>("GroupsModeButton")!;

            groups.IsChecked = true;
            Dispatcher.UIThread.RunJobs();

            Grid[] rows = [.. view.GetVisualDescendants()
                .OfType<Grid>()
                .Where(grid => grid.ColumnDefinitions.Count == 4
                               && grid.ColumnDefinitions[0].Width.Value == 56)];

            Assert.Equal(3, rows.Length);

            foreach (Grid row in rows)
            {
                TextBlock label = row.Children.OfType<TextBlock>().First();

                double needed = Unconstrained(label);
                double room = row.ColumnDefinitions[0].Width.Value;

                Assert.True(
                    room >= needed + MinimumSlack,
                    $"En {language}, «{label.Text}» pide {needed:0.0} y la columna deja "
                    + $"{room:0.0}: se queda en {room - needed:0.0} de aire.");
            }

            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            Localizer.Instance.Language = before;
        }
    }

    /// <summary>Y en el panel de bloques.</summary>
    [AvaloniaFact]
    public void En_el_panel_de_bloques_los_iconos_tambien_se_explican()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        var view = new TileBlocksView { DataContext = new TileBlocksViewModel(tiles) };
        var window = new Window { Content = view, Width = 420, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        AssertIconsExplainThemselves(view, atLeast: 3);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// El ancho y el alto del bloque se leen: el número cabe al lado de su girador.
    /// </summary>
    /// <remarks>
    /// Un <c>NumericUpDown</c> reparte su ancho entre la caja del número y los dos botones
    /// del girador, y los botones van primero. Si se queda corto, lo que desaparece es el
    /// número —el control sigue ahí, con sus flechas, y sólo se nota mirándolo—.
    /// </remarks>
    /// <param name="width">
    /// El panel se estira con su separador, así que se mide también estrecho: los dos
    /// giradores se llevan lo suyo por ancho fijo y lo que sobra es para el nombre.
    /// </param>
    [AvaloniaTheory]
    [InlineData(380)]
    [InlineData(300)]
    public void El_ancho_y_el_alto_del_bloque_se_leen(int width)
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));
        var blocks = new TileBlocksViewModel(tiles);

        blocks.AddBlockCommand.Execute(null);

        var view = new TileBlocksView { DataContext = blocks };
        var window = new Window { Content = view, Width = width, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        NumericUpDown[] numbers = [.. view.GetVisualDescendants().OfType<NumericUpDown>()];

        Assert.Equal(2, numbers.Length);

        foreach (NumericUpDown number in numbers)
        {
            TextBox box = number.GetVisualDescendants().OfType<TextBox>().First();

            Assert.True(
                box.Bounds.Width >= TwoDigits,
                $"La caja del número mide {box.Bounds.Width:0.0} dentro de un control de "
                + $"{number.Bounds.Width:0.0}, y hacen falta {TwoDigits} para leer dos "
                + "cifras: se ve el girador pero no el valor.");
        }

        // Y lo que ganan los números no se lo pueden quitar todo al nombre, que comparte
        // fila con ellos y es el único que se estira.
        TextBox name = view.GetVisualDescendants()
            .OfType<TextBox>()
            .First(box => !box.GetVisualAncestors().OfType<NumericUpDown>().Any());

        Assert.True(
            name.Bounds.Width >= ShortName,
            $"Con el panel a {width}, la caja del nombre queda en {name.Bounds.Width:0.0} "
            + $"y hacen falta {ShortName}: no cabría ni un nombre corto.");

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Y en la barra de arriba, con una pestaña abierta para que salga su aspa.</summary>
    [AvaloniaFact]
    public void En_la_barra_de_arriba_los_iconos_tambien_se_explican()
    {
        var main = new MainWindowViewModel();

        main.OpenTileSet(new TileSet("Bosque"));

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 720 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Cinco de la barra —los tres de paleta y los dos de fondo— mas el aspa de la
        // pestaña. Los tres de crear llevan rotulo y no cuentan.
        AssertIconsExplainThemselves(window, atLeast: 6);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// La tira de zoom y modo de pintado cabe en su columna.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Esa columna mide 256 fijos —los del lienzo— y no se estira: lo que no quepa se
    /// corta sin avisar. Los modos llevan icono y texto como en el editor de mapas, y eso
    /// son unos veinte píxeles más por botón, que es justo el margen que había.
    /// </para>
    /// <para>
    /// Se suman los anchos que piden los hijos en vez de mirar el <c>DesiredSize</c> del
    /// panel: el del panel ya viene recortado a la columna, así que siempre parece que
    /// cabe. Los hijos no, porque un StackPanel horizontal los mide con ancho infinito.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void La_tira_de_zoom_y_modo_cabe_en_su_columna()
    {
        using var editor = new MountedTileSet();

        RadioButton paint = editor.View.GetVisualDescendants()
            .OfType<RadioButton>()
            .First(button => button.GroupName == "TilePaintMode");

        var strip = (StackPanel)paint.Parent!;
        var grid = (Grid)editor.View.GetVisualDescendants().First(v => v is Grid { Name: "EditorGrid" });

        double column = grid.ColumnDefinitions[0].ActualWidth
            - strip.Margin.Left - strip.Margin.Right;

        // DesiredSize ya lleva dentro el margen de cada uno.
        double asked = strip.Children.Sum(child => child.DesiredSize.Width);

        Assert.True(
            asked <= column,
            $"La tira pide {asked:0.0} y la columna deja {column:0.0}: "
            + $"se salen {asked - column:0.0} píxeles y ahí se corta.");
    }

    /// <summary>Un editor de tiles montado en su ventana, que se cierra solo.</summary>
    private sealed class MountedTileSet : IDisposable
    {
        private readonly Window _window;

        public MountedTileSet()
        {
            var main = new MainWindowViewModel();

            View = new TileSetEditorView { DataContext = main.OpenTileSet(new TileSet("Bosque")) };
            _window = new Window { Content = View, Width = 1100, Height = 800 };

            _window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public TileSetEditorView View { get; }

        public void Dispose()
        {
            _window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// Ningún botón de esa vista se queda con un icono y sin explicación.
    /// </summary>
    /// <param name="atLeast">
    /// Cuántos tiene que encontrar como mínimo. Sin esto la comprobación pasaría sola el
    /// día que un cambio dejara la vista sin ningún icono realizado.
    /// </param>
    private static void AssertIconsExplainThemselves(Control view, int atLeast)
    {
        Button[] iconOnly = [.. view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Content is MaterialIcon)];

        Assert.True(
            iconOnly.Length >= atLeast,
            $"Sólo se han encontrado {iconOnly.Length} botones de icono y se esperaban "
            + $"al menos {atLeast}: la comprobación no estaría mirando nada.");

        foreach (Button button in iconOnly)
        {
            object? tip = ToolTip.GetTip(button);

            Assert.True(
                tip is string text && text.Length > 0,
                $"El botón del icono «{((MaterialIcon)button.Content!).Kind}» no tiene "
                + "ayuda emergente, y sin texto no hay otra forma de saber qué hace.");
        }
    }

    /// <summary>
    /// Los iconos dibujan algo de verdad.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Si se olvidan los estilos del paquete en App.axaml, el control se queda sin
    /// plantilla: la barra sale con huecos en blanco y nada se queja.
    /// </para>
    /// <para>
    /// Se mira que tenga hijos en el árbol visual y no que ocupe sitio. Medir el tamaño no
    /// vale: los estilos de esta vista le ponen un ancho y un alto fijos, así que ocupa
    /// sus dieciocho píxeles aunque no tenga nada dentro. Ese fue el primer intento y
    /// pasaba con los estilos quitados.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Los_iconos_de_la_barra_dibujan_algo()
    {
        MaterialIcon[] icons = [.. _view.GetVisualDescendants().OfType<MaterialIcon>()];

        Assert.NotEmpty(icons);

        foreach (MaterialIcon icon in icons)
        {
            Assert.True(
                icon.GetVisualDescendants().Any(),
                $"El icono «{icon.Kind}» no tiene nada dentro: se queda en un hueco vacío. "
                + "¿Están puestos los estilos del paquete en App.axaml?");
        }
    }

    /// <summary>
    /// Los modos conservan su texto: el icono los acompaña, no los sustituye.
    /// </summary>
    /// <remarks>
    /// En cuál estás metido es lo que más se mira de la barra, y un rótulo refuerza el
    /// estado mejor que un recuadro de color.
    /// </remarks>
    [AvaloniaFact]
    public void Los_modos_conservan_su_texto_junto_al_icono()
    {
        RadioButton[] modes = [.. _view.GetVisualDescendants()
            .OfType<RadioButton>()
            .Where(button => button.GroupName == "MapTool")];

        Assert.Equal(3, modes.Length);

        foreach (RadioButton mode in modes)
        {
            Assert.Single(mode.GetVisualDescendants().OfType<MaterialIcon>());

            TextBlock label = Assert.Single(mode.GetVisualDescendants().OfType<TextBlock>());

            Assert.False(
                string.IsNullOrWhiteSpace(label.Text),
                $"El modo «{mode.Tag}» se ha quedado sin rótulo.");
        }
    }

    [AvaloniaFact]
    public void El_texto_de_los_botones_de_zoom_cabe_con_holgura()
    {
        RadioButton[] buttons = [.. _view.GetVisualDescendants()
            .OfType<RadioButton>()
            .Where(button => button.GroupName == "MapTileZoom")];

        Assert.NotEmpty(buttons);

        foreach (RadioButton button in buttons)
        {
            TextBlock text = button.GetVisualDescendants().OfType<TextBlock>().First();

            double room = button.Bounds.Width
                - button.Padding.Left - button.Padding.Right
                - button.BorderThickness.Left - button.BorderThickness.Right;

            double needed = Unconstrained(text);

            Assert.True(
                room >= needed + MinimumSlack,
                $"«{button.Content}» tiene {room:0.0} de hueco y el texto pide {needed:0.00}: "
                + $"se queda en {room - needed:0.00} de aire y hacen falta {MinimumSlack}.");
        }
    }

    /// <summary>
    /// La línea de la pestaña seleccionada va debajo del texto, no encima.
    /// </summary>
    /// <remarks>
    /// Fluent la mete dentro de la caja del contenido contando con pestañas altas. Con las
    /// nuestras, esa caja mide justo lo que el texto y la línea le caía encima: medido, el
    /// texto ocupaba de 6 a 22 y la línea de 18 a 20.
    /// </remarks>
    [AvaloniaFact]
    public void La_linea_de_la_pestaña_no_se_mete_encima_del_texto()
    {
        TabControl tabs = _view.GetVisualDescendants().OfType<TabControl>().First();

        TabItem tab = tabs.GetVisualDescendants().OfType<TabItem>().First(item => item.IsSelected);

        TextBlock text = tab.GetVisualDescendants().OfType<TextBlock>().First();
        Border pipe = tab.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_SelectedPipe");

        double bottomOfText = text.TranslatePoint(new Point(0, text.Bounds.Height), tab)!.Value.Y;
        double topOfPipe = pipe.TranslatePoint(new Point(0, 0), tab)!.Value.Y;

        Assert.True(
            topOfPipe >= bottomOfText,
            $"El texto acaba en {bottomOfText:0.0} y la línea empieza en {topOfPipe:0.0}: se solapan.");

        // Y sigue dentro de la pestaña, no colgando por debajo.
        Assert.True(
            topOfPipe + pipe.Bounds.Height <= tab.Bounds.Height,
            $"La línea acaba en {topOfPipe + pipe.Bounds.Height:0.0} y la pestaña mide {tab.Bounds.Height:0.0}.");
    }

    /// <summary>
    /// Lo que mide el texto sin que nadie lo apriete.
    /// </summary>
    /// <remarks>
    /// El <c>DesiredSize</c> del que está montado ya viene recortado a lo que le dejaron,
    /// así que preguntándole a él siempre parece que cabe. Ése fue el primer intento de
    /// esta prueba, y pasaba con los botones cortados delante.
    /// </remarks>
    private static double Unconstrained(TextBlock text)
    {
        var loose = new TextBlock
        {
            Text = text.Text,
            FontSize = text.FontSize,
            FontFamily = text.FontFamily,
            FontWeight = text.FontWeight,
        };

        loose.Measure(Size.Infinity);

        return loose.DesiredSize.Width;
    }
}
