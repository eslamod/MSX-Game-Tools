namespace MSX_GameTools.Services;

/// <summary>
/// El fichero no contiene lo que dice contener. El mensaje explica qué falla, para
/// poder enseñárselo al usuario tal cual.
/// </summary>
public sealed class FileFormatException : Exception
{
    public FileFormatException(string message)
        : base(message)
    {
    }

    public FileFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
