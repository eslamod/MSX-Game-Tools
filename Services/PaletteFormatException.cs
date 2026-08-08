namespace MSX_SpritesEditor.Services;

/// <summary>El fichero no contiene una paleta MSX válida.</summary>
public sealed class PaletteFormatException : Exception
{
    public PaletteFormatException(string message)
        : base(message)
    {
    }

    public PaletteFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
