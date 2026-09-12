using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Cambiar el juego de tiles de un mapa que ya existe, banda por banda.
/// </summary>
/// <remarks>
/// Hasta ahora no se podía cambiar, ni siquiera con un solo juego: un mapa dibujado con el
/// equivocado no tenía más vuelta que rehacerlo. Las celdas guardan números de tile, así que
/// cambiar de juego no mueve ningún número; lo que cambia es de qué son dibujos.
/// </remarks>
public class MapPropertiesBandsTests
{
    /// <summary>Las propiedades de un mapa llegan con su juego puesto.</summary>
    [AvaloniaFact]
    public void Las_propiedades_llegan_con_el_juego_del_mapa()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel bosque = main.OpenTileSet(new TileSet("Bosque"));

        main.OpenTileSet(new TileSet("Ciudad"));

        MapEditorViewModel editor = main.OpenMap(new TileMap("Nivel", 32, 24), bosque);

        EditPropertiesViewModel form = Properties(main, editor);

        Assert.True(form.IsMap);
        Assert.Same(bosque, form.Bands!.TileSet);
    }

    /// <summary>
    /// Y al aceptar, el editor se repunta entero al nuevo.
    /// </summary>
    /// <remarks>
    /// No es sólo cambiar un campo: de ese juego cuelgan el pincel, los bloques, la paleta y la
    /// tira de tiles que se enseña abajo.
    /// </remarks>
    [AvaloniaFact]
    public void Cambiar_el_juego_del_mapa_repunta_el_editor()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel bosque = main.OpenTileSet(new TileSet("Bosque"));
        TileSetEditorViewModel ciudad = main.OpenTileSet(new TileSet("Ciudad"));

        MapEditorViewModel editor = main.OpenMap(new TileMap("Nivel", 32, 24), bosque);

        editor.MarkClean();

        EditPropertiesViewModel form = Properties(main, editor);

        form.Bands!.TileSet = ciudad;

        form.AcceptPropertiesCommand.Execute(null);

        Assert.Same(ciudad.TileSet, editor.TileSet);
        Assert.Same(ciudad.Thumbnails, editor.Tiles);
        Assert.Same(ciudad.Thumbnails, editor.CellImagesByThird[0]);

        Assert.Equal(ciudad.TileSet.Id, editor.Map.TileSetId);
        Assert.True(editor.IsModified);
    }

    /// <summary>Y se puede cambiar sólo una banda.</summary>
    [AvaloniaFact]
    public void Se_puede_cambiar_solo_la_banda_de_en_medio()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel bosque = main.OpenTileSet(new TileSet("Bosque"));
        TileSetEditorViewModel ciudad = main.OpenTileSet(new TileSet("Ciudad"));

        MapEditorViewModel editor = main.OpenMap(new TileMap("Nivel", 32, 24), bosque);

        EditPropertiesViewModel form = Properties(main, editor);

        form.Bands!.Middle = ciudad;

        form.AcceptPropertiesCommand.Execute(null);

        Assert.Equal(3, editor.CellImagesByThird.Count);

        Assert.Same(bosque.Thumbnails, editor.CellImagesByThird[0]);
        Assert.Same(ciudad.Thumbnails, editor.CellImagesByThird[1]);
        Assert.Same(bosque.Thumbnails, editor.CellImagesByThird[2]);

        Assert.Equal(ciudad.TileSet.Id, editor.Map.TileSetFor(8).Id);
    }

    /// <summary>Un mapa más alto que la pantalla sólo ofrece el suyo.</summary>
    [AvaloniaFact]
    public void Un_mapa_alto_solo_ofrece_un_juego()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel bosque = main.OpenTileSet(new TileSet("Bosque"));

        MapEditorViewModel editor = main.OpenMap(new TileMap("Nivel", 32, 40), bosque);

        EditPropertiesViewModel form = Properties(main, editor);

        Assert.False(form.Bands!.ShowsMiddle);
        Assert.False(form.Bands.ShowsBottom);
    }

    /// <summary>Tampoco aquí se pueden mezclar paletas.</summary>
    [AvaloniaFact]
    public void No_deja_mezclar_paletas_entre_bandas()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel bosque = main.OpenTileSet(new TileSet("Bosque"));
        TileSetEditorViewModel otra = main.OpenTileSet(new TileSet("Otra"), ColorPalette.CreateMsxStandard());

        MapEditorViewModel editor = main.OpenMap(new TileMap("Nivel", 32, 24), bosque);

        EditPropertiesViewModel form = Properties(main, editor);

        form.Bands!.Middle = otra;

        form.AcceptPropertiesCommand.Execute(null);

        Assert.True(form.HasError);
        Assert.Contains("Otra", form.ErrorMessage);

        // Y el mapa se queda como estaba, con el suyo y sin bandas.
        Assert.Single(editor.Map.TileSets);
        Assert.NotNull(main.RightPanViewModel);
    }

    /// <summary>
    /// Pasar por propiedades sin tocar los juegos no repinta el mapa.
    /// </summary>
    /// <remarks>
    /// Repuntar el editor rehace la tira de tiles, los bloques y los supertiles, y eso no tiene
    /// por qué pasar por haber entrado a cambiar el nombre.
    /// </remarks>
    [AvaloniaFact]
    public void Renombrar_no_repinta_el_mapa()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel bosque = main.OpenTileSet(new TileSet("Bosque"));

        MapEditorViewModel editor = main.OpenMap(new TileMap("Nivel", 32, 24), bosque);

        EditPropertiesViewModel form = Properties(main, editor);

        bool repainted = false;

        editor.RefreshRequested += () => repainted = true;

        form.Name = "Otro nombre";

        form.AcceptPropertiesCommand.Execute(null);

        Assert.False(repainted, "renombrar ha repintado el mapa entero");
    }

    // ------------------------------------------------------------------ los andamios

    private static EditPropertiesViewModel Properties(MainWindowViewModel main, PanelBaseViewModel document)
    {
        main.ShowPropertiesOf(document);

        return (EditPropertiesViewModel)main.RightPanViewModel!;
    }
}
