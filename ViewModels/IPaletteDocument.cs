using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Un panel que dibuja con una paleta y no con «la del programa».
/// </summary>
/// <remarks>
/// <para>
/// La paleta va embebida en el fichero de cada documento, así que no puede ser un ajuste
/// del espacio de trabajo: con dos juegos abiertos con paletas distintas, guardar uno
/// escribiría dentro de su fichero los colores del otro.
/// </para>
/// <para>
/// La barra de paletas de la ventana lee y escribe aquí, siempre sobre el documento que
/// esté delante: enseña la suya al cambiar de pestaña y se la cambia a ése al elegir otra.
/// </para>
/// </remarks>
public interface IPaletteDocument
{
    /// <summary>
    /// La paleta con la que se dibuja y con la que se guardaría.
    /// </summary>
    /// <remarks>
    /// Asignar la misma no hace nada. Asignar otra repinta el documento entero y lo deja
    /// sin guardar, porque cambia lo que iría a su fichero.
    /// </remarks>
    ColorPalette ColorPalette { get; set; }
}
