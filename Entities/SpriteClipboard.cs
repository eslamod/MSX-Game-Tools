namespace MSX_GameTools.Entities;

/// <summary>
/// El patrón que se llevó, común a todos los bancos abiertos.
/// </summary>
/// <remarks>
/// <para>
/// Uno solo para toda la ventana, como el portapapeles de cualquier programa: se copia en un
/// banco y se pega en otro, que es lo que hacía falta para no tener que redibujar el mismo
/// bicho en cada banco.
/// </para>
/// <para>
/// Guarda de qué tipo de banco salió porque el destino puede no ser del mismo, y en MSX1 el
/// patrón entero va de un solo color. Sin esa procedencia no hay forma de saber si al pegar
/// se pierde algo.
/// </para>
/// </remarks>
public class SpriteClipboard
{
    /// <summary>Lo copiado, o null si no se ha copiado nada todavía.</summary>
    public Sprite? Content { get; private set; }

    /// <summary>De qué clase de banco salió lo copiado.</summary>
    public SpriteBank.SpriteType From { get; private set; }

    /// <summary>
    /// Se queda con una copia suelta del patrón.
    /// </summary>
    /// <remarks>
    /// La copia se hace aquí y no la pide a quien llama: es lo que garantiza que retocar el
    /// original después de copiarlo no cambie lo que se acabe pegando, sin que haya que
    /// acordarse en cada sitio desde el que se copie.
    /// </remarks>
    public void Put(Sprite sprite, SpriteBank.SpriteType from)
    {
        Content = sprite.Copy();
        From = from;
    }
}
