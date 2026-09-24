using MSX_GameTools.Services;

namespace MSX_GameTools.Tests;

/// <summary>
/// Responde a los diálogos sin abrir nada y anota lo que le han pedido, para poder
/// comprobar qué se preguntó y con qué texto.
/// </summary>
internal sealed class TestDialogService : IDialogService
{
    /// <summary>Lo que contesta a cada confirmación.</summary>
    public bool ConfirmAnswer { get; init; } = true;

    /// <summary>Ruta que devuelve el selector de abrir; <c>null</c> equivale a cancelar.</summary>
    /// <remarks>Se puede cambiar entre llamadas, para abrir dos ficheros seguidos.</remarks>
    public string? OpenPath { get; set; }

    /// <inheritdoc cref="OpenPath"/>
    public string? SavePath { get; set; }

    /// <inheritdoc cref="OpenPath"/>
    public string? FolderPath { get; set; }

    /// <summary>Lo que contesta al preguntar el tamano de celda; <c>null</c> es cancelar.</summary>
    public int? CellSize { get; init; }

    /// <summary>Que contesta a una eleccion entre dos: null es cancelar.</summary>
    public bool? ChooseAnswer { get; init; } = false;

    /// <summary>
    /// Respuestas por orden cuando en un mismo flujo se pregunta mas de una vez.
    /// </summary>
    /// <remarks>
    /// Importar un png pregunta dos cosas seguidas -con que paleta y en que modo- y las dos
    /// pasan por aqui. Con una sola respuesta para las dos no se puede escribir una prueba
    /// que conteste distinto a cada una, que es justo lo que hace falta comprobar.
    /// </remarks>
    public Queue<bool?> ChooseAnswers { get; } = new();

    public int ChooseCalls { get; private set; }

    public string LastChooseMessage { get; private set; } = string.Empty;

    public int ConfirmCalls { get; private set; }

    /// <summary>Celda que devuelve el selector de reticula; <c>null</c> es cancelar.</summary>
    public int? PickedCell { get; set; }

    public int CellSizeCalls { get; private set; }

    public int PickCellCalls { get; private set; }

    public MSX_GameTools.Entities.ReferenceImage? LastPickCellImage { get; private set; }

    public int LastPickCellCurrent { get; private set; }

    public string LastCellSizeMessage { get; private set; } = string.Empty;

    public int LastCellSizeMaximum { get; private set; }

    public PickerFileKind LastPickerKind { get; private set; }

    /// <inheritdoc cref="LastPickerKind"/>
    /// <remarks>Aparte del de abrir: hay comandos que usan los dos selectores.</remarks>
    public PickerFileKind LastSavePickerKind { get; private set; }

    public string LastConfirmMessage { get; private set; } = string.Empty;

    public string LastConfirmLabel { get; private set; } = string.Empty;

    public string? LastSuggestedFileName { get; private set; }

    /// <summary>Veces que se ha abierto el selector de guardar, para ver si vuelve a preguntar.</summary>
    public int SaveCalls { get; private set; }

    /// <summary>Times a folder was asked for, which is another picker.</summary>
    public int FolderCalls { get; private set; }

    /// <summary>Los avisos mostrados, en orden.</summary>
    public List<string> Messages { get; } = [];

    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
    {
        ConfirmCalls++;
        LastConfirmMessage = message;
        LastConfirmLabel = confirmLabel;

        return Task.FromResult(ConfirmAnswer);
    }

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add(message);

        return Task.CompletedTask;
    }

    public Task<string?> PickFileToOpenAsync(string title, PickerFileKind kind = PickerFileKind.Json)
    {
        LastPickerKind = kind;

        return Task.FromResult(OpenPath);
    }

    public Task<int?> PickReferenceCellAsync(MSX_GameTools.Entities.ReferenceImage image, int currentCell)
    {
        PickCellCalls++;
        LastPickCellImage = image;
        LastPickCellCurrent = currentCell;

        return Task.FromResult(PickedCell);
    }

    public Task<int?> AskCellSizeAsync(string title, string message, int suggested, int maximum)
    {
        CellSizeCalls++;
        LastCellSizeMessage = message;
        LastCellSizeMaximum = maximum;

        return Task.FromResult(CellSize);
    }

    public Task<string?> PickFileToSaveAsync(
        string title, string suggestedFileName, PickerFileKind kind = PickerFileKind.Json)
    {
        SaveCalls++;
        LastSuggestedFileName = suggestedFileName;
        LastSavePickerKind = kind;

        return Task.FromResult(SavePath);
    }

    public Task<string?> PickFolderAsync(string title)
    {
        FolderCalls++;

        return Task.FromResult(FolderPath);
    }

    public Task<bool?> ChooseAsync(string title, string message, string firstLabel, string secondLabel)
    {
        ChooseCalls++;
        LastChooseMessage = message;

        // La cola manda mientras quede algo; cuando se acaba se vuelve a la respuesta fija,
        // que es lo que usan las pruebas donde solo se pregunta una vez.
        return Task.FromResult(ChooseAnswers.Count > 0 ? ChooseAnswers.Dequeue() : ChooseAnswer);
    }
}
