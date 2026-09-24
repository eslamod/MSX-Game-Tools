using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
/// Los ajustes del usuario: el idioma y el zoom de partida, guardados fuera del proyecto.
/// </summary>
/// <remarks>
/// Todas las pruebas apuntan el almacén a una carpeta temporal: los ajustes de verdad son
/// de quien ejecuta las pruebas y no se tocan. Y el idioma se deja como estaba al acabar,
/// que el Localizer es único para todo el programa.
/// </remarks>
public class SettingsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"msxcfg-{Guid.NewGuid():N}");
    private readonly string _language = Localizer.Instance.Language;

    public SettingsTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        Localizer.Instance.Language = _language;

        Directory.Delete(_folder, recursive: true);
    }

    private SettingsStore Store => new(_folder);

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void Los_ajustes_van_y_vuelven()
    {
        var zoom = new EditorPreferences { SpriteThumbnailZoom = 4, MapTileZoom = 3 };

        Assert.True(Store.Save(new Settings("ca", zoom, [])));

        Settings read = Store.Load();

        Assert.Equal("ca", read.Language);
        Assert.Equal(4, read.Preferences.SpriteThumbnailZoom);
        Assert.Equal(3, read.Preferences.MapTileZoom);
    }

    /// <summary>Unos ajustes perdidos no son motivo para no arrancar.</summary>
    [AvaloniaFact]
    public void Un_fichero_roto_no_impide_arrancar()
    {
        File.WriteAllText(Store.Path, "esto no es json");

        Settings read = Store.Load();

        Assert.Equal(string.Empty, read.Language);
        Assert.Equal(new EditorPreferences().MapTileZoom, read.Preferences.MapTileZoom);
    }

    [AvaloniaFact]
    public void Sin_fichero_valen_los_de_partida()
    {
        Settings read = Store.Load();

        Assert.Equal(string.Empty, read.Language);
        Assert.Equal(new EditorPreferences().SpriteThumbnailZoom, read.Preferences.SpriteThumbnailZoom);
    }

    /// <summary>Un fichero de una versión que no entendemos se ignora en vez de leerse a medias.</summary>
    [AvaloniaFact]
    public void Un_formato_mas_nuevo_se_ignora()
    {
        File.WriteAllText(Store.Path, """{ "version": 99, "language": "ca" }""");

        Assert.Equal(string.Empty, Store.Load().Language);
    }

    // ------------------------------------------------------------------ el arranque

    /// <summary>
    /// Con todos los ajustes, no con uno de muestra.
    /// </summary>
    /// <remarks>
    /// Por reflexión y no campo a campo a mano: así el día que se añada una preferencia
    /// nueva y se olvide de copiarla al cargar, esto cae solo. Comprobando sólo uno, seis
    /// de los siete no estaban probados y no me enteré hasta intentar romperlo.
    /// </remarks>
    [AvaloniaFact]
    public void Al_arrancar_se_aplican_todos_los_ajustes_guardados()
    {
        var fresh = new EditorPreferences();
        var saved = new EditorPreferences();

        // Cada uno distinto de SU valor por omisión, no de un número cualquiera. Dándoles
        // el índice, a la escala le tocaba justo su defecto, y entonces la prueba no
        // distinguía «se ha copiado» de «no se ha tocado nunca»: quitando la escala de
        // CopyFrom seguía en verde.
        foreach (System.Reflection.PropertyInfo property in Adjustable)
        {
            object value = property.PropertyType == typeof(double)
                ? (object)Math.Min(EditorPreferences.MaxScale, (double)property.GetValue(fresh)! + 0.5)
                : (int)property.GetValue(fresh)! + 7;

            Assert.NotEqual(property.GetValue(fresh), value);

            property.SetValue(saved, value);
        }

        Store.Save(new Settings("en", saved, []));

        var main = new MainWindowViewModel(settings: Store);

        main.LoadSettings();

        Assert.Equal("en", Localizer.Instance.Language);
        Assert.NotEmpty(Adjustable);

        foreach (System.Reflection.PropertyInfo property in Adjustable)
            Assert.Equal(property.GetValue(saved), property.GetValue(main.Preferences));
    }

    /// <summary>
    /// Todo lo que se ajusta y se guarda: los zooms y la escala de la interfaz.
    /// </summary>
    /// <remarks>
    /// Por tipo y no por una lista escrita a mano, para que una preferencia nueva entre
    /// sola. Cuando la escala llegó, era un <c>double</c> entre siete <c>int</c> y una
    /// lista de enteros la habría dejado fuera sin decir nada.
    /// </remarks>
    private static IReadOnlyList<System.Reflection.PropertyInfo> Adjustable =>
    [
        .. typeof(EditorPreferences).GetProperties()
            .Where(property => property.CanWrite)
            .Where(property => property.PropertyType == typeof(int) || property.PropertyType == typeof(double)),
    ];

    /// <summary>
    /// Las pestañas abiertas guardan una referencia a los ajustes, así que al cargarlos hay
    /// que copiar los valores y no cambiar el objeto por otro.
    /// </summary>
    [AvaloniaFact]
    public void Cargar_no_cambia_el_objeto_de_ajustes()
    {
        Store.Save(new Settings("es", new EditorPreferences { MapTileZoom = 4 }, []));

        var main = new MainWindowViewModel(settings: Store);
        EditorPreferences before = main.Preferences;

        main.LoadSettings();

        Assert.Same(before, main.Preferences);
        Assert.Equal(4, before.MapTileZoom);
    }

    /// <summary>
    /// El idioma guardado tiene que estar puesto antes de construir nada.
    /// </summary>
    /// <remarks>
    /// Los cajones del árbol resuelven su texto una sola vez, al crearse. Aplicando el
    /// idioma después de construir el ViewModel salían en el idioma del arranque anterior:
    /// los menús sí cambiaban, porque son enlaces que se resuelven al enseñar la ventana,
    /// y el árbol no, que es justo la mezcla más confusa.
    /// </remarks>
    [AvaloniaFact]
    public void El_arbol_arranca_en_el_idioma_guardado()
    {
        Store.Save(new Settings("ca", new EditorPreferences(), []));

        Store.ApplyLanguage();

        var main = new MainWindowViewModel(settings: Store);

        main.LoadSettings();

        Assert.Equal(
            Localizer.Instance["TreeMaps"],
            main.TreeGeneralVm.PrimaryNodes.Single(node => node.Tag == Constants.TAG_ID_NODE_MAPS).DisplayText);

        Assert.Equal("Mapes", main.TreeGeneralVm.PrimaryNodes
            .Single(node => node.Tag == Constants.TAG_ID_NODE_MAPS).DisplayText);
    }

    [AvaloniaFact]
    public void Sin_almacen_no_se_toca_el_disco()
    {
        var main = new MainWindowViewModel();

        main.LoadSettings();
        main.SaveSettings();

        Assert.Empty(Directory.GetFiles(_folder));
    }

    // ------------------------------------------------------------------ el panel

    [AvaloniaFact]
    public async Task Aceptar_aplica_el_idioma_y_el_zoom_y_los_guarda()
    {
        Localizer.Instance.Language = "es";

        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs, Store);

        main.ShowPreferencesCommand.Execute(null);

        var form = (EditPreferencesViewModel)main.RightPanViewModel!;

        form.Language = Localizer.Languages.Single(choice => choice.Code == "ca");
        form.MapTileZoom = 4;

        await form.AcceptPreferencesCommand.ExecuteAsync(null);

        Assert.Equal("ca", Localizer.Instance.Language);
        Assert.Equal(4, main.Preferences.MapTileZoom);
        Assert.Null(main.RightPanViewModel);

        // Y en disco, para la próxima vez.
        Settings saved = Store.Load();

        Assert.Equal("ca", saved.Language);
        Assert.Equal(4, saved.Preferences.MapTileZoom);
    }

    /// <summary>
    /// Cambiar de idioma avisa de que no todo cambia hasta reiniciar, y avisa en el idioma
    /// nuevo, que es el que se acaba de elegir.
    /// </summary>
    [AvaloniaFact]
    public async Task Cambiar_de_idioma_avisa_de_que_hay_que_reiniciar()
    {
        Localizer.Instance.Language = "es";

        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs, Store);

        main.ShowPreferencesCommand.Execute(null);

        var form = (EditPreferencesViewModel)main.RightPanViewModel!;

        form.Language = Localizer.Languages.Single(choice => choice.Code == "ca");

        await form.AcceptPreferencesCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Messages);
        Assert.Equal(Localizer.Instance["LanguageChangedMessage"], dialogs.Messages[0]);
        Assert.Contains("l'arbre del projecte", dialogs.Messages[0], StringComparison.Ordinal);
    }

    /// <summary>Avisar de algo que no ha pasado enseña a no leer los avisos.</summary>
    [AvaloniaFact]
    public async Task Tocar_solo_el_zoom_no_avisa_de_nada()
    {
        Localizer.Instance.Language = "es";

        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs, Store);

        main.ShowPreferencesCommand.Execute(null);

        var form = (EditPreferencesViewModel)main.RightPanViewModel!;

        form.MapTileZoom = 4;

        await form.AcceptPreferencesCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Messages);
        Assert.Equal(4, main.Preferences.MapTileZoom);
    }

    /// <summary>
    /// El tamaño de la pantalla también se guarda.
    /// </summary>
    /// <remarks>
    /// Es del juego que se está haciendo y no cambia de una sesión a la siguiente: volver a
    /// escribirlo en cada arranque sería peor que no preguntarlo.
    /// </remarks>
    [AvaloniaFact]
    public async Task El_tamano_de_la_pantalla_se_guarda_para_la_proxima_vez()
    {
        Localizer.Instance.Language = "es";

        var main = new MainWindowViewModel(new TestDialogService(), Store);

        main.ShowPreferencesCommand.Execute(null);

        var form = (EditPreferencesViewModel)main.RightPanViewModel!;

        form.ScreenWidth = 24;
        form.ScreenHeight = 22;

        await form.AcceptPreferencesCommand.ExecuteAsync(null);

        Assert.Equal(24, main.Preferences.ScreenWidth);
        Assert.Equal(22, main.Preferences.ScreenHeight);

        Settings saved = Store.Load();

        Assert.Equal(24, saved.Preferences.ScreenWidth);
        Assert.Equal(22, saved.Preferences.ScreenHeight);
    }

    [AvaloniaFact]
    public void Cancelar_no_cambia_nada()
    {
        Localizer.Instance.Language = "es";

        var main = new MainWindowViewModel(settings: Store);

        main.ShowPreferencesCommand.Execute(null);

        var form = (EditPreferencesViewModel)main.RightPanViewModel!;

        form.Language = Localizer.Languages.Single(choice => choice.Code == "en");
        form.MapTileZoom = 4;
        form.CancelPreferencesCommand.Execute(null);

        Assert.Equal("es", Localizer.Instance.Language);
        Assert.NotEqual(4, main.Preferences.MapTileZoom);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    /// <summary>Se abre con lo que hay puesto, no con los valores de partida.</summary>
    [AvaloniaFact]
    public void El_panel_arranca_con_los_ajustes_de_ahora()
    {
        Localizer.Instance.Language = "en";

        var main = new MainWindowViewModel(settings: Store);

        main.Preferences.MapTileZoom = 3;
        main.Preferences.ScreenHeight = 22;
        main.ShowPreferencesCommand.Execute(null);

        var form = (EditPreferencesViewModel)main.RightPanViewModel!;

        Assert.Equal("en", form.Language.Code);
        Assert.Equal(3, form.MapTileZoom);
        Assert.Equal(22, form.ScreenHeight);
    }

    // ------------------------------------------------------------------ la escala

    /// <summary>
    /// La escala llega al layout de la ventana, no sólo a los ajustes.
    /// </summary>
    /// <remarks>
    /// Con la ventana montada y midiendo lo que ocupa el menú: de layout y no de render,
    /// que es lo que hace que el texto se dibuje al tamaño nuevo en vez de estirarse.
    /// </remarks>
    [AvaloniaFact]
    public void La_escala_agranda_la_ventana_de_verdad()
    {
        var main = new MainWindowViewModel(new TestDialogService { ChooseAnswer = false }, Store);
        var window = new MainWindow { DataContext = main, Width = 1200, Height = 800 };

        window.Show();
        Pump();

        Menu bar = window.GetVisualDescendants().OfType<Menu>().First();
        double before = bar.Bounds.Height;

        Assert.True(before > 0);

        main.Preferences.InterfaceScale = 2;
        Pump();

        // El menú no ha cambiado: lo que ha cambiado es el sitio que ocupa en la ventana.
        Assert.Equal(before, bar.Bounds.Height);
        Assert.Equal(before * 2, bar.TranslatePoint(new Point(0, bar.Bounds.Height), window)!.Value.Y, 1);

        window.Close();
        Pump();
        Pump();
    }

    [AvaloniaFact]
    public async Task La_escala_se_guarda_y_vuelve()
    {
        var main = new MainWindowViewModel(new TestDialogService(), Store);

        main.ShowPreferencesCommand.Execute(null);

        var form = (EditPreferencesViewModel)main.RightPanViewModel!;

        form.Scale = EditPreferencesViewModel.Scales.Single(choice => choice.Value == 1.5);

        await form.AcceptPreferencesCommand.ExecuteAsync(null);

        Assert.Equal(1.5, main.Preferences.InterfaceScale);
        Assert.Equal(1.5, Store.Load().Preferences.InterfaceScale);
    }

    /// <summary>Esto es para agrandar; encoger la interfaz no le hace falta a nadie.</summary>
    [AvaloniaFact]
    public void La_escala_no_baja_de_uno_ni_sube_de_dos()
    {
        var preferences = new EditorPreferences();

        preferences.InterfaceScale = 0.5;

        Assert.Equal(EditorPreferences.MinScale, preferences.InterfaceScale);

        preferences.InterfaceScale = 10;

        Assert.Equal(EditorPreferences.MaxScale, preferences.InterfaceScale);
    }

    /// <summary>Pedirlas dos veces trae el panel que ya estaba, no otro encima.</summary>
    [AvaloniaFact]
    public void Las_preferencias_no_se_apilan()
    {
        var main = new MainWindowViewModel(settings: Store);

        main.ShowPreferencesCommand.Execute(null);
        main.ShowPreferencesCommand.Execute(null);

        Assert.Single(main.RightPanels.OfType<EditPreferencesViewModel>());
    }
}
