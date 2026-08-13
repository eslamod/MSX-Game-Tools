using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
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

    [AvaloniaFact]
    public void Los_ajustes_van_y_vuelven()
    {
        var zoom = new EditorPreferences { SpriteThumbnailZoom = 4, MapTileZoom = 3 };

        Assert.True(Store.Save(new Settings("ca", zoom)));

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
        var saved = new EditorPreferences();

        // Un valor distinto en cada uno, para que ninguno acierte por casualidad.
        foreach ((System.Reflection.PropertyInfo property, int index) in ZoomProperties.Select((p, i) => (p, i)))
            property.SetValue(saved, index + 2);

        Store.Save(new Settings("en", saved));

        var main = new MainWindowViewModel(settings: Store);

        main.LoadSettings();

        Assert.Equal("en", Localizer.Instance.Language);

        foreach (System.Reflection.PropertyInfo property in ZoomProperties)
            Assert.Equal(property.GetValue(saved), property.GetValue(main.Preferences));
    }

    private static IEnumerable<System.Reflection.PropertyInfo> ZoomProperties =>
        typeof(EditorPreferences).GetProperties().Where(property => property.PropertyType == typeof(int));

    /// <summary>
    /// Las pestañas abiertas guardan una referencia a los ajustes, así que al cargarlos hay
    /// que copiar los valores y no cambiar el objeto por otro.
    /// </summary>
    [AvaloniaFact]
    public void Cargar_no_cambia_el_objeto_de_ajustes()
    {
        Store.Save(new Settings("es", new EditorPreferences { MapTileZoom = 4 }));

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
        Store.Save(new Settings("ca", new EditorPreferences()));

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
    public void Aceptar_aplica_el_idioma_y_el_zoom_y_los_guarda()
    {
        Localizer.Instance.Language = "es";

        var main = new MainWindowViewModel(settings: Store);

        main.ShowPreferencesCommand.Execute(null);

        var form = (EditPreferencesViewModel)main.RightPanViewModel!;

        form.Language = Localizer.Languages.Single(choice => choice.Code == "ca");
        form.MapTileZoom = 4;
        form.AcceptPreferencesCommand.Execute(null);

        Assert.Equal("ca", Localizer.Instance.Language);
        Assert.Equal(4, main.Preferences.MapTileZoom);
        Assert.Null(main.RightPanViewModel);

        // Y en disco, para la próxima vez.
        Settings saved = Store.Load();

        Assert.Equal("ca", saved.Language);
        Assert.Equal(4, saved.Preferences.MapTileZoom);
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
        main.ShowPreferencesCommand.Execute(null);

        var form = (EditPreferencesViewModel)main.RightPanViewModel!;

        Assert.Equal("en", form.Language.Code);
        Assert.Equal(3, form.MapTileZoom);
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
