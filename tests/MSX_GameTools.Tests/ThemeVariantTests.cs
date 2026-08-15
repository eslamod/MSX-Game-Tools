using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
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

    private static int Brightness(Color color) => color.R + color.G + color.B;
}
