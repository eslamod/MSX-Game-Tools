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
            // La ventana primero: el servicio de diálogos la necesita como propietaria
            // de los modales.
            var window = new MainWindow();
            var main = new MainWindowViewModel(new DialogService(window), new SettingsStore());

            // Antes de enseñarla: el idioma decide lo que dicen los menús, y cambiarlo con
            // la ventana ya delante se ve como un parpadeo.
            main.LoadSettings();

            window.DataContext = main;
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
