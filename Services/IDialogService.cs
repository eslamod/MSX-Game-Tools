namespace MSX_SpritesEditor.Services;

/// <summary>
/// Lo que un ViewModel necesita pedirle al usuario sin conocer la ventana. La
/// implementación real vive en la capa de vistas; los tests usan una que responde
/// sin abrir nada.
/// </summary>
public interface IDialogService
{
    /// <summary>Pide confirmación de una acción. Devuelve <c>false</c> si se cancela.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel);

    /// <summary>Informa de algo, con un único botón de cierre.</summary>
    Task ShowMessageAsync(string title, string message);

    /// <summary>Ruta del fichero elegido, o <c>null</c> si se cancela.</summary>
    Task<string?> PickFileToOpenAsync(string title);

    /// <summary>Ruta donde guardar, o <c>null</c> si se cancela.</summary>
    Task<string?> PickFileToSaveAsync(string title, string suggestedFileName);
}
