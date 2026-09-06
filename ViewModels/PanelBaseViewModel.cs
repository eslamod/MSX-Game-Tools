using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.ViewModels;

/// <summary>Base de todo panel que puede vivir en una pestaña o en el panel derecho.</summary>
public abstract partial class PanelBaseViewModel : ObservableObject
{
    /// <summary>El documento tal y como quedó la última vez que se guardó.</summary>
    private string? _savedText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabLabel))]
    private string _header = string.Empty;

    [ObservableProperty]
    private string _tagId = string.Empty;

    /// <summary>El fichero del que salió o en el que se guardó; nulo si nunca se guardó.</summary>
    [ObservableProperty]
    private string? _filePath;

    /// <summary>Si se ha tocado algo desde el último guardado. Es lo que marca el asterisco.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabLabel))]
    private bool _isModified;

    /// <summary>
    /// Si el panel es una herramienta y no un documento.
    /// </summary>
    /// <remarks>
    /// Una herramienta vive en el panel de la derecha y se ve a la vez que lo que se está
    /// editando; un documento ocupa una pestaña del centro. Los bloques son herramienta:
    /// hay que verlos mientras se dibujan los tiles.
    /// </remarks>
    public virtual bool IsTool => false;

    /// <summary>
    /// Si lo que hay en el panel se guarda en un fichero.
    /// </summary>
    /// <remarks>
    /// Los formularios del lateral no lo son: se abren, se aceptan y se van, y no hay nada
    /// suyo que perder. Los bloques tampoco, aunque se editen: se guardan dentro del
    /// fichero de su juego de tiles, así que el documento es el juego.
    /// </remarks>
    public virtual bool IsDocument => false;

    /// <summary>
    /// Cómo se llama el elemento: lo que se lee en la pestaña y en el árbol, y lo que se
    /// propone como nombre de fichero.
    /// </summary>
    /// <remarks>
    /// Se cambia por <see cref="MainWindowViewModel.Rename"/> y no aquí: renombrar arrastra
    /// la cabecera, el árbol y, en un juego de tiles, el nombre que sus mapas llevan
    /// apuntado, y eso sólo lo sabe la ventana.
    /// </remarks>
    public virtual string DocumentName
    {
        get => Header;
        set { }
    }

    /// <summary>Las letras entre paréntesis de la pestaña: «TS», «SP», «MP».</summary>
    public virtual string HeaderTag => string.Empty;

    /// <summary>
    /// El panel se ha cerrado y ya no está en el lateral.
    /// </summary>
    /// <remarks>
    /// Para soltar lo que se enganchó al abrirlo. Un formulario que escucha a su editor
    /// para enterarse de lo que pasa fuera lo seguiría escuchando después de cerrado, y
    /// abrir y cerrar el mismo formulario iría dejando oyentes muertos detrás.
    /// </remarks>
    public virtual void OnClosed()
    {
    }

    /// <summary>
    /// Rehace la cabecera a partir del nombre.
    /// </summary>
    /// <remarks>
    /// Aquí y no en cada sitio que abre un panel: así lo que sale al renombrar tiene la
    /// misma forma que lo que salió al crearlo, sin tener que acordarse.
    /// </remarks>
    public void RefreshHeader() =>
        Header = HeaderTag.Length == 0 ? DocumentName : $"{DocumentName} ({HeaderTag})";

    /// <summary>
    /// Qué es este documento, para las claves de las frases que lo nombran.
    /// </summary>
    /// <remarks>
    /// Es un trozo de clave —«TileSet», «Map»— y no un texto. Las frases que nombran el
    /// tipo se escriben enteras una vez por tipo («No se pudo guardar el mapa») en vez de
    /// montarlas con el nombre por fuera: eso sólo funciona en español y de casualidad,
    /// porque los tres son masculinos.
    /// </remarks>
    public virtual string KindKey => "Document";

    /// <summary>Cómo se llama el tipo, para nombrarlo dentro de un paréntesis.</summary>
    public string DocumentKind => Localization.Localizer.Instance[$"Kind{KindKey}"];

    /// <summary>Lo que se lee en la pestaña. El asterisco marca lo que está sin guardar.</summary>
    public string TabLabel => IsModified ? $"{Header} *" : Header;

    /// <summary>
    /// El documento tal y como quedaría en su fichero ahora mismo.
    /// </summary>
    /// <remarks>
    /// Es lo que escribe Guardar y es lo que se compara para saber si hay cambios. Con una
    /// sola definición las dos cosas no pueden discrepar: si un día el formato cambia, el
    /// aviso de cambios sin guardar cambia con él sin tocar nada más.
    /// </remarks>
    public virtual string ToFileText() => string.Empty;

    /// <summary>
    /// Apunta que se ha tocado algo, para que aparezca el asterisco.
    /// </summary>
    /// <remarks>
    /// Es una señal barata y generosa: puede sobrar —deshacer hasta el principio deja el
    /// asterisco puesto— y no pasa nada. Lo que decide si hay trabajo que perder es
    /// <see cref="HasUnsavedChanges"/>, que mira el contenido de verdad; así olvidarse de
    /// llamar aquí afea la pestaña pero no pierde nada.
    /// </remarks>
    public virtual void Touch()
    {
        if (IsDocument)
            IsModified = true;
    }

    /// <summary>El documento acaba de salir de un fichero o de entrar en él.</summary>
    /// <param name="text">
    /// Lo que hay en el fichero, si ya se tenía. Guardar lo acaba de escribir, así que
    /// pasarlo evita serializar el documento entero dos veces seguidas.
    /// </param>
    public void MarkSaved(string path, string? text = null)
    {
        FilePath = path;
        _savedText = text ?? ToFileText();
        IsModified = false;
    }

    /// <summary>
    /// Documento recién creado: existe, pero todavía no hay nada que perder.
    /// </summary>
    /// <remarks>
    /// Lo que se importa de un png, un csv o un binario no pasa por aquí: eso sí trae
    /// contenido, no tiene fichero del editor donde estar, y sale marcado desde el
    /// principio.
    /// </remarks>
    public void MarkClean()
    {
        _savedText = ToFileText();
        IsModified = false;
    }

    /// <summary>
    /// Si lo que hay ahora se diferencia de lo último que se guardó.
    /// </summary>
    /// <remarks>
    /// Cuesta lo que serializar el documento entero, así que se pregunta al salir y al
    /// eliminar, no mientras se dibuja. A cambio no se puede equivocar por un
    /// <see cref="Touch"/> que falte.
    /// </remarks>
    public bool HasUnsavedChanges() => IsDocument && ToFileText() != _savedText;
}
