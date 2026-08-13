using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

            case "Palette":
                main.AddPaletteCommand.Execute(null);
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
