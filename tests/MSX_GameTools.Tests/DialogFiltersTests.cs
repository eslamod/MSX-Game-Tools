using Avalonia.Headless.XUnit;
using Avalonia.Platform.Storage;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Qué tipos de fichero ofrece cada diálogo.
/// </summary>
/// <remarks>
/// Se prueba la tabla y no el diálogo: abrirlo de verdad necesita una ventana y el
/// selector del sistema. Lo que se equivocaba era la tabla.
/// </remarks>
public class DialogFiltersTests : IDisposable
{
    private readonly string _before = Localizer.Instance.Language;

    /// <summary>El idioma es de todo el programa: hay que devolverlo como estaba.</summary>
    public void Dispose() => Localizer.Instance.Language = _before;

    /// <summary>
    /// «Cualquiera» quiere decir cualquiera. Al abrir, el filtro se aplicaba siempre, así
    /// que importar un csv acababa enseñando sólo los json del editor.
    /// </summary>
    [AvaloniaFact]
    public void Cualquier_fichero_no_lleva_filtro()
    {
        Assert.Null(DialogService.FiltersFor(PickerFileKind.Any));
    }

    [AvaloniaFact]
    public void Los_ficheros_del_editor_son_json()
    {
        FilePickerFileType type = Assert.Single(DialogService.FiltersFor(PickerFileKind.Json)!);

        Assert.Equal(["*.json"], type.Patterns);

        // Y ya no se llaman «Paleta MSX»: tambien se abren mapas y juegos de tiles.
        Assert.DoesNotContain("Paleta", type.Name);
    }

    [AvaloniaFact]
    public void Las_imagenes_llevan_los_formatos_de_imagen()
    {
        FilePickerFileType type = Assert.Single(DialogService.FiltersFor(PickerFileKind.Image)!);

        Assert.Contains("*.png", type.Patterns!);
    }

    /// <summary>
    /// Cada formato de exportación trae el suyo.
    /// </summary>
    /// <remarks>
    /// Se mira el patrón y no el nombre: el nombre está traducido y cambia con el idioma
    /// que haya dejado puesto otra prueba.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(PickerFileKind.Assembler, "*.asm")]
    [InlineData(PickerFileKind.Binary, "*.bin")]
    [InlineData(PickerFileKind.Csv, "*.csv")]
    public void Cada_formato_de_exportacion_lleva_su_extension(PickerFileKind kind, string pattern)
    {
        FilePickerFileType type = Assert.Single(DialogService.FiltersFor(kind)!);

        Assert.Equal([pattern], type.Patterns);
    }

    /// <summary>
    /// Y cómo se llaman va en el idioma del programa.
    /// </summary>
    /// <remarks>
    /// Estaban escritos a mano en español, así que el selector salía diciendo «Ficheros del
    /// editor» con el resto del programa en inglés. Se leen en cada llamada, de modo que
    /// cambiar de idioma se nota sin reiniciar.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(PickerFileKind.Json, "en", "Editor files")]
    [InlineData(PickerFileKind.Json, "ca", "Fitxers de l'editor")]
    [InlineData(PickerFileKind.Image, "en", "Image")]
    [InlineData(PickerFileKind.Project, "ca", "Projecte MSX Game Tools")]
    [InlineData(PickerFileKind.Assembler, "en", "Assembler")]
    [InlineData(PickerFileKind.Binary, "ca", "Binari")]
    public void Los_filtros_se_llaman_en_el_idioma_del_programa(
        PickerFileKind kind, string language, string name)
    {
        Localizer.Instance.Language = language;

        Assert.Equal(name, Assert.Single(DialogService.FiltersFor(kind)!).Name);
    }
}
