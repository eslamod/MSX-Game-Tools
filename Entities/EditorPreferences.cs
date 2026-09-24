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

    private AppThemeVariant _themeVariant = AppThemeVariant.System;

    private string _asmData = AsmStyle.Dotted;

    private int _screenWidth = DefaultScreenWidth;

    private int _screenHeight = DefaultScreenHeight;

    private bool _showScreenGrid;

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
    /// Arranca siguiendo al sistema, que es como se comportan las demás aplicaciones y lo
    /// que espera quien estrena ésta. A quien viene de antes no le cambia el aspecto: el
    /// ajuste se escribe en el fichero cada vez que se guarda, lo haya tocado o no, así que
    /// al abrirlo sale de ahí y esto no llega a mirarse.
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
    /// Si la tira de patrones se ve en la pestaña de grupos.
    /// </summary>
    /// <remarks>
    /// Se guarda porque es una decisión sobre el sitio: la tira ocupa alto y ese alto es de
    /// los grupos. Quien la esconda no tiene por qué volver a esconderla cada vez que abre la
    /// aplicación.
    /// </remarks>
    public bool GroupPatternsOpen { get; set; } = true;

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

    /// <summary>
    /// Lo que mide una pantalla del juego, en tiles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Para los juegos de pantallas fijas, que se dibujan como un mapa entero —las pantallas
    /// son contiguas aunque se vean de una en una— y luego se cargan por separado. Con esto el
    /// editor enseña por dónde parte cada una y la exportación corta el mapa por ahí.
    /// </para>
    /// <para>
    /// 32x24 es la pantalla entera del MSX; quien deje dos filas de marcador pone 22, y ésa es
    /// justo la razón de preguntarlo en vez de darlo por sabido.
    /// </para>
    /// <para>
    /// Aquí y no en el mapa porque es del juego y no de un mapa suyo: todos los del mismo
    /// juego se parten igual. A cambio es un ajuste de esta máquina y no viaja con el fichero.
    /// </para>
    /// </remarks>
    public int ScreenWidth
    {
        get => _screenWidth;
        set => SetProperty(ref _screenWidth, Math.Clamp(value, MinScreenSide, MaxScreenSide));
    }

    /// <inheritdoc cref="ScreenWidth"/>
    public int ScreenHeight
    {
        get => _screenHeight;
        set => SetProperty(ref _screenHeight, Math.Clamp(value, MinScreenSide, MaxScreenSide));
    }

    /// <summary>
    /// Si se pinta la rejilla de pantallas encima del mapa.
    /// </summary>
    /// <remarks>
    /// Aquí y no en cada mapa para que siga puesta al cambiar de pestaña, como los zooms:
    /// enseñar por dónde parten las pantallas es una forma de trabajar, no algo del mapa.
    /// </remarks>
    public bool ShowScreenGrid
    {
        get => _showScreenGrid;
        set => SetProperty(ref _showScreenGrid, value);
    }

    /// <summary>La pantalla entera del MSX, que es de donde se parte.</summary>
    public const int DefaultScreenWidth = 32;

    /// <inheritdoc cref="DefaultScreenWidth"/>
    public const int DefaultScreenHeight = 24;

    private const int MinScreenSide = 1;

    /// <summary>Lo que mide de lado el mapa más grande que se puede hacer.</summary>
    private const int MaxScreenSide = 256;

    public void CopyFrom(EditorPreferences other)
    {
        InterfaceScale = other.InterfaceScale;
        AsmData = other.AsmData;
        ThemeVariant = other.ThemeVariant;
        SpriteCanvasZoom = other.SpriteCanvasZoom;
        SpriteThumbnailZoom = other.SpriteThumbnailZoom;
        TileCanvasZoom = other.TileCanvasZoom;
        TileThumbnailZoom = other.TileThumbnailZoom;
        GroupPatternsOpen = other.GroupPatternsOpen;
        BlockGridZoom = other.BlockGridZoom;
        BlockTileZoom = other.BlockTileZoom;
        MapTileZoom = other.MapTileZoom;
        ScreenWidth = other.ScreenWidth;
        ScreenHeight = other.ScreenHeight;
        ShowScreenGrid = other.ShowScreenGrid;
    }
}
