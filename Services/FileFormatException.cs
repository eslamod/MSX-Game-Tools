namespace MSX_GameTools.Services;

/// <summary>
/// El fichero no contiene lo que dice contener. El mensaje explica qué falla, para
/// poder enseñárselo al usuario tal cual.
/// </summary>
/// <remarks>
/// <para>
/// Estos mensajes van en español y no pasan por el <see cref="Localization.Localizer"/>,
/// a diferencia de todo lo demás que se le enseña al usuario. Es una decisión, no un
/// olvido: son diagnóstico —«en la capa «Suelo», la fila 3 trae 12 celdas y el mapa mide
/// 20»— y sólo salen cuando un fichero está mal, que es raro y suele acabar con alguien
/// mirando el json.
/// </para>
/// <para>
/// El título del aviso que los envuelve sí está traducido, así que quien los vea lee
/// «El fitxer no és vàlid» y debajo el detalle en español.
/// </para>
/// </remarks>
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
