using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.Entities;

/// <summary>
/// Ajustes de presentación que se mantienen al cambiar de pestaña.
/// </summary>
/// <remarks>
/// <para>
/// El TabControl reconstruye la vista cada vez que se cambia de pestaña, así que sin
/// guardar esto en algún sitio el zoom volvía a X1 constantemente.
/// </para>
/// <para>
/// Es un objeto del espacio de trabajo y no un estático, aunque un estático bastaría
/// para la aplicación: con estáticos el zoom que deja un test se lo encuentra el
/// siguiente, y los tests pasan a depender del orden en que se ejecuten.
/// </para>
/// <para>
/// Uno solo para todas las pestañas, no uno por banco: lo que se quiere es seguir
/// trabajando con el zoom que estabas usando, no que cada elemento recuerde el suyo.
/// </para>
/// </remarks>
public sealed class EditorPreferences : ObservableObject
{
    private double _interfaceScale = 1;

    private AppThemeVariant _themeVariant = AppThemeVariant.Light;

    private string _asmData = AsmStyle.Dotted;

    /// <summary>
    /// Cuánto se agranda toda la interfaz, sobre lo que ya diga el sistema.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No sustituye al escalado del sistema, se suma a él: en Windows al 150% con esto al
    /// 125% se ve al 187%. Es para decir «más grande de lo que el sistema cree», que es lo
    /// que hace falta en una pantalla densa configurada al 100%, donde no sólo los píxeles
    /// del MSX se quedan pequeños: también los menús, el árbol y las etiquetas.
    /// </para>
    /// <para>
    /// En Linux además puede ser la única salida. En X11 no hay DPI por monitor fiable y
    /// Avalonia se queda en factor 1 si el escritorio no pone <c>Xft.dpi</c>, así que sin
    /// esto habría que exportar una variable de entorno antes de arrancar.
    /// </para>
    /// <para>
    /// Es lo único de aquí que avisa cuando cambia: la ventana está enlazada a ello. Los
    /// zooms los lee cada vista al montarse y no hace falta.
    /// </para>
    /// </remarks>
    public double InterfaceScale
    {
        get => _interfaceScale;
        set => SetProperty(ref _interfaceScale, Math.Clamp(value, MinScale, MaxScale));
    }

    /// <summary>
    /// Con qué variante se pinta la interfaz.
    /// </summary>
    /// <remarks>
    /// Arranca en <see cref="AppThemeVariant.Light"/> y no siguiendo al sistema: la
    /// aplicación siempre ha sido clara, y a quien la actualice no se le cambia el aspecto
    /// sin haberlo pedido. Seguir al sistema está a un desplegable de distancia.
    /// </remarks>
    public AppThemeVariant ThemeVariant
    {
        get => _themeVariant;
        set => SetProperty(ref _themeVariant, value);
    }

    /// <summary>Por debajo de uno no tiene sentido: es para agrandar, no para encoger.</summary>
    public const double MinScale = 1;

    /// <summary>Más de esto no cabe la ventana en una pantalla normal.</summary>
    public const double MaxScale = 2;

    /// <summary>Índice del zoom del lienzo de sprites, 0-2.</summary>
    public int SpriteCanvasZoom { get; set; }

    /// <summary>Factor del zoom de las miniaturas de sprites: 1, 2, 3 o 4.</summary>
    public int SpriteThumbnailZoom { get; set; } = 1;

    /// <inheritdoc cref="SpriteCanvasZoom"/>
    public int TileCanvasZoom { get; set; }

    /// <inheritdoc cref="SpriteThumbnailZoom"/>
    public int TileThumbnailZoom { get; set; } = 1;

    /// <summary>Factor del zoom de la rejilla donde se compone el bloque.</summary>
    public int BlockGridZoom { get; set; } = 2;

    /// <summary>Factor del zoom del selector de tiles del panel de bloques.</summary>
    public int BlockTileZoom { get; set; } = 1;

    /// <summary>Factor del zoom del selector de tiles y bloques del editor de mapas.</summary>
    public int MapTileZoom { get; set; } = 2;

    /// <summary>
    /// Se queda con los valores de otro.
    /// </summary>
    /// <remarks>
    /// Copiar en vez de sustituir el objeto: las pestañas ya abiertas guardan una
    /// referencia a éste, y cambiarlo por otro las dejaría mirando al de antes.
    /// </remarks>
    /// <summary>
    /// Cómo se llama la directiva de datos en el ensamblador que se use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Aquí y no preguntándolo al exportar: se elige una vez, se guarda con los demás ajustes y
    /// no se vuelve a pensar en ello. Es del usuario y no del proyecto, como el idioma: quien
    /// reciba un juego compartido sigue exportando con el suyo.
    /// </para>
    /// <para>
    /// Se guarda el texto y no una opción de una lista, porque los cuatro ensambladores que se
    /// han probado no agotan los que hay. <see cref="AsmStyle.Of"/> se encarga de que en blanco
    /// vuelva al de siempre.
    /// </para>
    /// </remarks>
    public string AsmData
    {
        get => _asmData;
        set => SetProperty(ref _asmData, value);
    }

    /// <inheritdoc cref="AsmData"/>
    public AsmStyle AsmStyle => AsmStyle.Of(AsmData);

    public void CopyFrom(EditorPreferences other)
    {
        InterfaceScale = other.InterfaceScale;
        AsmData = other.AsmData;
        ThemeVariant = other.ThemeVariant;
        SpriteCanvasZoom = other.SpriteCanvasZoom;
        SpriteThumbnailZoom = other.SpriteThumbnailZoom;
        TileCanvasZoom = other.TileCanvasZoom;
        TileThumbnailZoom = other.TileThumbnailZoom;
        BlockGridZoom = other.BlockGridZoom;
        BlockTileZoom = other.BlockTileZoom;
        MapTileZoom = other.MapTileZoom;
    }
}
