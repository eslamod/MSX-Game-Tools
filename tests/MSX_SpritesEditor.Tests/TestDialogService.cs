using MSX_SpritesEditor.Services;

namespace MSX_SpritesEditor.Tests;

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

    public int ConfirmCalls { get; private set; }

    public string LastConfirmMessage { get; private set; } = string.Empty;

    public string LastConfirmLabel { get; private set; } = string.Empty;

    public string? LastSuggestedFileName { get; private set; }

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

    public Task<string?> PickFileToOpenAsync(string title) => Task.FromResult(OpenPath);

    public Task<string?> PickFileToSaveAsync(string title, string suggestedFileName)
    {
        LastSuggestedFileName = suggestedFileName;

        return Task.FromResult(SavePath);
    }
}
