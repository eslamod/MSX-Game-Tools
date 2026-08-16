using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.Entities;

/// <summary>De qué es un fichero de los recientes.</summary>
/// <remarks>
/// Propio y no el <c>EditorFileKind</c> de <c>EditorFile</c>, que dice qué guarda un
/// documento mirándolo por dentro. Un proyecto no es uno de esos —no se reconoce con el
/// mismo mecanismo ni se abre por el mismo camino— y aquí hace falta distinguirlo, porque
/// abrir un proyecto cierra todo lo demás y abrir un documento no.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<RecentKind>))]
public enum RecentKind
{
    Project,
    SpriteBank,
    TileSet,
    Map,
    Palette,
}

/// <summary>Algo que se abrió, y de qué era.</summary>
public sealed record RecentItem(string Path, RecentKind Kind)
{
    /// <summary>Lo que se enseña en el menú. La ruta entera va en la ayuda emergente.</summary>
    [JsonIgnore]
    public string Name => System.IO.Path.GetFileName(Path);
}

/// <summary>
/// Lo último que se abrió, para no tener que buscarlo otra vez.
/// </summary>
/// <remarks>
/// <para>
/// Es del usuario y no del proyecto: va en los ajustes, junto al idioma. Lo que abriste
/// ayer no es parte del juego que estás haciendo.
/// </para>
/// <para>
/// El primero es el último abierto. Volver a abrir algo que ya está en la lista lo sube
/// arriba en vez de duplicarlo, que es lo que hace útil una lista corta: si trabajas con
/// tres ficheros, esos tres se quedan arriba por mucho que abras.
/// </para>
/// </remarks>
public sealed class RecentFiles : ObservableObject
{
    /// <summary>Cuántos se recuerdan.</summary>
    /// <remarks>
    /// Diez es lo que cabe en un menú sin tener que leerlo despacio. Pasado eso, buscar en
    /// la lista cuesta lo mismo que buscar en el disco y deja de servir de nada.
    /// </remarks>
    public const int Capacity = 10;

    public RecentFiles() =>
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));

    public ObservableCollection<RecentItem> Items { get; } = [];

    /// <summary>Con esto se apaga la entrada del menú mientras no haya nada que ofrecer.</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>Pone algo arriba del todo.</summary>
    public void Add(string path, RecentKind kind)
    {
        string full = Normalize(path);

        // Se quita antes de poner: si ya estaba, esto es subirlo, no añadirlo dos veces.
        RemoveWithoutNormalizing(full);

        Items.Insert(0, new RecentItem(full, kind));

        while (Items.Count > Capacity)
            Items.RemoveAt(Items.Count - 1);
    }

    /// <summary>Lo quita, si estaba. Para cuando el fichero ya no existe.</summary>
    public void Remove(string path) => RemoveWithoutNormalizing(Normalize(path));

    public void Clear() => Items.Clear();

    /// <summary>Deja la lista tal cual venía de los ajustes.</summary>
    /// <remarks>
    /// Pasa por <see cref="Add" /> al revés en lugar de copiar: así lo guardado se limpia
    /// con las mismas reglas que lo nuevo. Un fichero de ajustes editado a mano, o escrito
    /// por una versión anterior, puede traer repetidos o de más.
    /// </remarks>
    public void Reset(IEnumerable<RecentItem> saved)
    {
        Items.Clear();

        foreach (RecentItem item in saved.Reverse())
            Add(item.Path, item.Kind);
    }

    private void RemoveWithoutNormalizing(string full)
    {
        for (int at = Items.Count - 1; at >= 0; at--)
        {
            if (Same(Items[at].Path, full))
                Items.RemoveAt(at);
        }
    }

    /// <summary>
    /// Dos rutas que llevan al mismo sitio son la misma.
    /// </summary>
    /// <remarks>
    /// Sin distinguir mayúsculas porque en Windows no las distingue el disco, y quien abre
    /// el mismo fichero desde el selector y desde una carpeta escrita a mano no espera
    /// verlo dos veces en el menú.
    /// </remarks>
    private static bool Same(string one, string other) =>
        string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// La ruta completa, para que dos formas de nombrar lo mismo se reconozcan.
    /// </summary>
    /// <remarks>
    /// Si la ruta no se puede resolver se queda como vino: esto es una comodidad, y no
    /// merece tirar por tierra una apertura que por lo demás fue bien.
    /// </remarks>
    private static string Normalize(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException
                                              or NotSupportedException
                                              or PathTooLongException)
        {
            return path;
        }
    }
}
