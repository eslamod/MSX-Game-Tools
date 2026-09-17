using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Tests;

/// <summary>
/// Exportar un documento pasando por su panel, que es como se hace desde el menú.
/// </summary>
/// <remarks>
/// El panel es el punto de entrada desde que exportar dejó de ser dos entradas de menú:
/// pregunta el formato y el destino, enseña lo que va a escribir y lo escribe al aceptar. Las
/// pruebas que sólo quieren los ficheros en el disco pasan por aquí para no repetir esos tres
/// pasos en cada una.
/// </remarks>
internal static class TestExport
{
    /// <inheritdoc cref="ThroughPanelAsync"/>
    public static Task TileSetAsync(
        MainWindowViewModel main,
        ExportFormat format,
        string path,
        Action<ExportViewModel>? answering = null) =>
        ThroughPanelAsync(main, main.ExportTileSetCommand, format, path, answering);

    /// <inheritdoc cref="ThroughPanelAsync"/>
    public static Task SpriteBankAsync(
        MainWindowViewModel main,
        ExportFormat format,
        string path,
        Action<ExportViewModel>? answering = null) =>
        ThroughPanelAsync(main, main.ExportSpriteBankCommand, format, path, answering);

    /// <param name="open">La entrada de menú del documento, que es la que abre el panel.</param>
    /// <param name="answering">
    /// Lo que se conteste además del formato y el destino, como la casilla de la ROM de
    /// ejemplo. Después del formato a propósito: el formato es el que decide qué preguntas
    /// salen.
    /// </param>
    private static async Task ThroughPanelAsync(
        MainWindowViewModel main,
        IRelayCommand open,
        ExportFormat format,
        string path,
        Action<ExportViewModel>? answering)
    {
        open.Execute(null);

        var form = (ExportViewModel)main.RightPanViewModel!;

        form.Format = form.Formats.Single(choice => choice.Format == format);
        form.Destination = path;

        answering?.Invoke(form);

        await form.AcceptExportCommand.ExecuteAsync(null);
    }
}
