namespace MSX_SpritesEditor.Services;

/// <summary>
/// Lo que un ViewModel necesita pedirle al usuario sin conocer la ventana. La
/// implementación real vive en la capa de vistas; los tests usan una que responde
/// sin abrir nada.
/// </summary>
/// <summary>Para qué se abre el selector, que decide qué extensiones ofrece.</summary>
public enum PickerFileKind
{
    Json,
    Image,
}

public interface IDialogService
{
    /// <summary>Pide confirmación de una acción. Devuelve <c>false</c> si se cancela.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel);

    /// <summary>Informa de algo, con un único botón de cierre.</summary>
    Task ShowMessageAsync(string title, string message);

    /// <summary>Ruta del fichero elegido, o <c>null</c> si se cancela.</summary>
    Task<string?> PickFileToOpenAsync(string title, PickerFileKind kind = PickerFileKind.Json);

    /// <summary>
    /// Pide el lado de celda con el que trocear una hoja de sprites. Devuelve
    /// <c>null</c> si se cancela.
    /// </summary>
    Task<int?> AskCellSizeAsync(string title, string message, int suggested, int maximum);

    /// <summary>
    /// Enseña la hoja con su retícula encima para elegir una celda con el ratón.
    /// Devuelve el índice elegido, o <c>null</c> si se cancela.
    /// </summary>
    Task<int?> PickReferenceCellAsync(Entities.ReferenceImage image, int currentCell);

    /// <summary>Ruta donde guardar, o <c>null</c> si se cancela.</summary>
    Task<string?> PickFileToSaveAsync(string title, string suggestedFileName);
}
