using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;

namespace MSX_GameTools;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Equivalente al App_Startup de WPF.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new SettingsStore();

            // El idioma, antes que nada. Hay textos que se resuelven al construirse el
            // objeto que los lleva —los cajones del árbol— y no se enteran de un cambio
            // posterior: con esto después del ViewModel, el árbol salía en el idioma del
            // arranque anterior mientras los menús ya estaban en el nuevo.
            settings.ApplyLanguage();

            // La ventana antes que el ViewModel: el servicio de diálogos la necesita como
            // propietaria de los modales.
            var window = new MainWindow();
            var main = new MainWindowViewModel(new DialogService(window), settings);

            // Y el resto de los ajustes antes de enseñarla, para que no se vea el cambio.
            main.LoadSettings();

            window.DataContext = main;
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
