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
    public string? OpenPath { get; init; }

    /// <summary>Ruta que devuelve el selector de guardar; <c>null</c> equivale a cancelar.</summary>
    public string? SavePath { get; init; }

    /// <summary>Lo que contesta al preguntar el tamano de celda; <c>null</c> es cancelar.</summary>
    public int? CellSize { get; init; }

    /// <summary>Que contesta a una eleccion entre dos: null es cancelar.</summary>
    public bool? ChooseAnswer { get; init; } = false;

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

    public string LastConfirmMessage { get; private set; } = string.Empty;

    public string LastConfirmLabel { get; private set; } = string.Empty;

    public string? LastSuggestedFileName { get; private set; }

    /// <summary>Veces que se ha abierto el selector de guardar, para ver si vuelve a preguntar.</summary>
    public int SaveCalls { get; private set; }

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

        return Task.FromResult(SavePath);
    }

    public Task<bool?> ChooseAsync(string title, string message, string firstLabel, string secondLabel)
    {
        ChooseCalls++;
        LastChooseMessage = message;

        return Task.FromResult(ChooseAnswer);
    }
}
