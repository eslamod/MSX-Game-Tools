namespace MSX_SpritesEditor.Services;

/// <summary>
/// Confirma sin preguntar. Es el valor por defecto para tests y para cualquier
/// escenario sin ventana; la aplicación siempre inyecta el servicio real.
/// </summary>
public sealed class AlwaysConfirmDialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel) =>
        Task.FromResult(true);
}
