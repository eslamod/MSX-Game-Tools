using Avalonia;
using Avalonia.Headless;
using MSX_GameTools.Tests;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

// El runtime de Avalonia headless es una instancia global con afinidad de hilo:
// varias clases de test tocandolo a la vez se pisan. Los objetos de Avalonia
// (los brushes de la paleta, por ejemplo) tampoco son seguros entre hilos.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MSX_GameTools.Tests;

/// <summary>
/// Arranque de Avalonia para los tests, que van todos con <c>[AvaloniaFact]</c>.
/// </summary>
/// <remarks>
/// Todos, incluso los que parecen de logica pura y no montan ninguna vista. Casi
/// cualquier cosa de este proyecto acaba creando un objeto de Avalonia —un banco crea
/// miniaturas, una paleta crea brushes— y esos exigen el hilo de UI en cuanto el runtime
/// headless esta arrancado. Con [Fact] la comprobacion no salta si ese test corre antes
/// que cualquier [AvaloniaFact], asi que el resultado dependia del orden de ejecucion y
/// aparecian fallos intermitentes de «Call from invalid thread» al añadir tests nuevos.
/// </remarks>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
