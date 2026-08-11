using Avalonia;

namespace MSX_GameTools;

internal static class Program
{
    // En Avalonia el punto de entrada es explícito (en WPF lo generaba App.xaml).
    // No uses APIs de Avalonia antes de que AppMain haya arrancado.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Lo usa también el previsualizador de XAML del editor.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
