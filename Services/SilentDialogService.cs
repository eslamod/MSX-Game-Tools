namespace MSX_GameTools.Services;

/// <summary>
/// No abre nada: confirma sin preguntar, se traga los mensajes y cancela los
/// selectores de fichero. Es el valor por defecto para tests y para cualquier
/// escenario sin ventana; la aplicación siempre inyecta el servicio real.
/// </summary>
public sealed class SilentDialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel) =>
        Task.FromResult(true);

    public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;

    public Task<string?> PickFileToOpenAsync(string title, PickerFileKind kind = PickerFileKind.Json) =>
        Task.FromResult<string?>(null);

    public Task<int?> AskCellSizeAsync(string title, string message, int suggested, int maximum) =>
        Task.FromResult<int?>(null);

    public Task<int?> PickReferenceCellAsync(Entities.ReferenceImage image, int currentCell) =>
        Task.FromResult<int?>(null);

    public Task<string?> PickFileToSaveAsync(
        string title, string suggestedFileName, PickerFileKind kind = PickerFileKind.Json) =>
        Task.FromResult<string?>(null);

    public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);

    /// <summary>
    /// La segunda opción, no cancelar.
    /// </summary>
    /// <remarks>
    /// Cancelar significa «no sigas», y este servicio existe para no estorbar: con null,
    /// cerrar la ventana sin un servicio de diálogos de verdad se quedaba a medias para
    /// siempre. Sigue adelante, igual que <see cref="ConfirmAsync"/>.
    /// </remarks>
    public Task<bool?> ChooseAsync(string title, string message, string firstLabel, string secondLabel) =>
        Task.FromResult<bool?>(false);
}
