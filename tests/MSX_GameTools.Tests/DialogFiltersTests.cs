using Avalonia.Headless.XUnit;
using Avalonia.Platform.Storage;
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
public class DialogFiltersTests
{
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
}
