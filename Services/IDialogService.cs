namespace MSX_SpritesEditor.Services;

/// <summary>
/// Diálogos que un ViewModel necesita pedir sin conocer la ventana. La implementación
/// real vive en la capa de vistas; los tests usan una que responde sin preguntar.
/// </summary>
public interface IDialogService
{
    /// <summary>Pide confirmación de una acción. Devuelve <c>false</c> si se cancela.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel);
}
