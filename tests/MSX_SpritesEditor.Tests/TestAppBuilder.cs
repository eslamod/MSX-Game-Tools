using Avalonia;
using Avalonia.Headless;
using MSX_SpritesEditor.Tests;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

// El runtime de Avalonia headless es una instancia global con afinidad de hilo:
// varias clases de test tocandolo a la vez se pisan. Los objetos de Avalonia
// (los brushes de la paleta, por ejemplo) tampoco son seguros entre hilos.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MSX_SpritesEditor.Tests;

/// <summary>
/// Arranque de Avalonia para los tests marcados con <c>[AvaloniaFact]</c>.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
