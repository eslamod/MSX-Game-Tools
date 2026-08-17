using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Qué dice la barra del mapa de la celda que hay bajo el ratón.
/// </summary>
/// <remarks>
/// La otra etiqueta de la barra es la de lo que hay cogido para estampar. Con la herramienta de
/// seleccionar no cambia nunca, así que quien iba a la barra a ver qué tile había en una celda
/// leía siempre «Tile 0», que es lo que hay cogido al abrir.
/// </remarks>
public class MapHoverLabelTests
{
    [AvaloniaFact]
    public void Dice_donde_esta_el_raton_y_que_tile_hay()
    {
        MapEditorViewModel editor = Editor();

        editor.Map.Stamp(0, 3, 2, TilePatch.Single(77));
        editor.Hover = (3, 2);

        Assert.Contains("3, 2", editor.HoverLabel);
        Assert.Contains("Tile 77", editor.HoverLabel);
    }

    /// <summary>En una celda vacía no se inventa un cero, que es un tile como otro.</summary>
    [AvaloniaFact]
    public void En_una_celda_vacia_solo_dice_donde()
    {
        MapEditorViewModel editor = Editor();

        editor.Hover = (3, 2);

        Assert.DoesNotContain("Tile", editor.HoverLabel);
        Assert.Contains("3, 2", editor.HoverLabel);
    }

    /// <summary>Y fuera del mapa no dice nada.</summary>
    [AvaloniaFact]
    public void Fuera_del_mapa_no_dice_nada()
    {
        MapEditorViewModel editor = Editor();

        editor.Hover = null;

        Assert.Equal(string.Empty, editor.HoverLabel);
    }

    /// <summary>
    /// Lo que se ve, que es lo que se está señalando.
    /// </summary>
    /// <remarks>
    /// Apagar una capa cambia lo que hay debajo del ratón; decir el tile de la capa activa
    /// cuando la que se ve es otra sería peor que no decir nada.
    /// </remarks>
    [AvaloniaFact]
    public void Dice_el_de_la_capa_que_se_ve()
    {
        MapEditorViewModel editor = Editor();

        editor.Map.Stamp(0, 3, 2, TilePatch.Single(77));
        editor.AddLayerCommand.Execute(null);
        editor.Map.Stamp(1, 3, 2, TilePatch.Single(90));

        editor.Hover = (3, 2);

        Assert.Contains("Tile 90", editor.HoverLabel);

        editor.Layers[1].IsVisible = false;

        // Y al volver a pasar por encima, el de abajo.
        editor.Hover = null;
        editor.Hover = (3, 2);

        Assert.Contains("Tile 77", editor.HoverLabel);
    }

    /// <summary>Pintar sin mover el ratón deja el número al día.</summary>
    [AvaloniaFact]
    public void Al_pintar_se_entera_sin_mover_el_raton()
    {
        MapEditorViewModel editor = Editor();

        editor.Hover = (3, 2);

        List<string> changed = [];
        editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        editor.PickTiles(41, 41);
        editor.Paint(3, 2);

        Assert.Contains(nameof(MapEditorViewModel.HoverLabel), changed);
        Assert.Contains("Tile 41", editor.HoverLabel);
    }

    private static MapEditorViewModel Editor()
    {
        var main = new MainWindowViewModel();

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        return main.OpenMap(new TileMap("Nivel", 10, 10), tiles);
    }
}
