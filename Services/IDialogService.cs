namespace MSX_GameTools.Services;

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

    /// <summary>El índice del proyecto, que tiene su propia extensión.</summary>
    Project,

    /// <summary>
    /// Sin filtro. Para importar, que el fichero puede venir de cualquier herramienta y
    /// llamarse como quiera.
    /// </summary>
    Any,

    /// <summary>Lo que escribe una exportación en texto.</summary>
    Assembler,

    /// <summary>Lo que escribe una exportación en bytes.</summary>
    Binary,

    /// <summary>La tabla de nombres del mapa, en texto separado por comas.</summary>
    Csv,
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
    Task<string?> PickFileToSaveAsync(
        string title, string suggestedFileName, PickerFileKind kind = PickerFileKind.Json);

    /// <summary>
    /// Pregunta entre dos opciones que hacen algo distinto. <c>true</c> la primera,
    /// <c>false</c> la segunda y <c>null</c> si se cancela.
    /// </summary>
    /// <remarks>
    /// Distinto de <see cref="ConfirmAsync"/>, donde cancelar y decir que no son lo
    /// mismo. Aquí las dos opciones siguen adelante y hace falta una tercera salida.
    /// </remarks>
    Task<bool?> ChooseAsync(string title, string message, string firstLabel, string secondLabel);
}
