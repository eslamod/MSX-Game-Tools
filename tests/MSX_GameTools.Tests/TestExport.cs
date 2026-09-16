using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Tests;

/// <summary>
/// Exportar un juego de tiles pasando por su panel, que es como se hace desde el menú.
/// </summary>
/// <remarks>
/// El panel es el punto de entrada desde que exportar dejó de ser dos entradas de menú:
/// pregunta el formato y el destino, enseña lo que va a escribir y lo escribe al aceptar. Las
/// pruebas que sólo quieren los ficheros en el disco pasan por aquí para no repetir esos tres
/// pasos en cada una.
/// </remarks>
internal static class TestExport
{
    public static async Task TileSetAsync(MainWindowViewModel main, ExportFormat format, string path)
    {
        main.ExportTileSetCommand.Execute(null);

        var form = (ExportViewModel)main.RightPanViewModel!;

        form.Format = form.Formats.Single(choice => choice.Format == format);
        form.Destination = path;

        await form.AcceptExportCommand.ExecuteAsync(null);
    }
}
