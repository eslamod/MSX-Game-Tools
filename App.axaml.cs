using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MSX_SpritesEditor.Services;
using MSX_SpritesEditor.ViewModels;
using MSX_SpritesEditor.Views;

namespace MSX_SpritesEditor;

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
            window.DataContext = new MainWindowViewModel(new DialogService(window));

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
