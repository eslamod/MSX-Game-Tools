using Avalonia;
using Avalonia.Headless;
using MSX_SpritesEditor.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

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
