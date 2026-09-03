using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Lo que se estampa se comparte entre los mapas de un mismo juego de tiles.
/// </summary>
/// <remarks>
/// Montar un mapa grande a partir de trozos es copiar de uno y pegar en otro, y con un pincel
/// por pestaña la copia se quedaba en el mapa de origen: se cogía el trozo, se cambiaba de
/// pestaña y no había nada que estampar.
/// </remarks>
public class SharedBrushTests
{
    /// <summary>Lo copiado en un mapa se puede estampar en otro del mismo juego.</summary>
    [AvaloniaFact]
    public void Lo_copiado_en_un_mapa_se_estampa_en_otro()
    {
        (MapEditorViewModel from, MapEditorViewModel into) = TwoMaps();

        from.PickTile(TilePatch.Single(9), "Tile 9");
        from.Paint(1, 1);
        from.Paint(2, 1);

        from.Selection = MapRegion.Between(1, 1, 2, 1);
        from.CopySelectionCommand.Execute(null);

        Assert.Equal("Copia 2x1", into.BrushLabel);

        into.Paint(4, 3);

        Assert.Equal(9, into.ActiveLayer!.Layer.Grid[4, 3]);
        Assert.Equal(9, into.ActiveLayer.Layer.Grid[5, 3]);
    }

    /// <summary>
    /// Y el mapa que no está delante se entera, para que la barra no mienta.
    /// </summary>
    /// <remarks>
    /// El pincel ya no es una propiedad del mapa, así que nadie avisa por su cuenta de que la
    /// barra tiene que cambiar: hay que decirlo a mano y esto lo comprueba en la ventana
    /// montada, que es donde se vería el fallo.
    /// </remarks>
    [AvaloniaFact]
    public void La_barra_del_otro_mapa_dice_lo_que_hay_cogido()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        TileSetEditorViewModel tiles = main.OpenTileSet(new TileSet("Bosque"));

        MapEditorViewModel from = main.OpenMap(new TileMap("Trozo", 8, 6), tiles);
        MapEditorViewModel into = main.OpenMap(new TileMap("Entero", 16, 6), tiles);

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var told = new List<string>();

        into.PropertyChanged += (_, args) => told.Add(args.PropertyName ?? string.Empty);

        from.Selection = MapRegion.Between(0, 0, 3, 2);
        from.CopySelectionCommand.Execute(null);

        Dispatcher.UIThread.RunJobs();

        Assert.Contains(nameof(MapEditorViewModel.BrushLabel), told);
        Assert.Equal("Copia 4x3", into.BrushLabel);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Con otro juego de tiles no se comparte.
    /// </summary>
    /// <remarks>
    /// Un pincel son números de tile: llevárselo a un mapa de otro juego pegaría otro dibujo.
    /// Por eso vive en el juego y no en el espacio de trabajo entero.
    /// </remarks>
    [AvaloniaFact]
    public void Con_otro_juego_de_tiles_no_se_comparte()
    {
        (MapEditorViewModel from, _) = TwoMaps();

        var other = new MapEditorViewModel(
            new TileMap("De otro juego", 8, 6),
            new TileSetEditorViewModel(new TileSet("Cueva"), ColorPalette.CreateMsxStandard()));

        from.Selection = MapRegion.Between(0, 0, 1, 1);
        from.CopySelectionCommand.Execute(null);

        Assert.Equal("Copia 2x2", from.BrushLabel);
        Assert.Equal("Tile 0", other.BrushLabel);
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Dos mapas del mismo juego de tiles, como los trozos de una captura.</summary>
    private static (MapEditorViewModel From, MapEditorViewModel Into) TwoMaps()
    {
        var tiles = new TileSetEditorViewModel(
            new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

        return (
            new MapEditorViewModel(new TileMap("Trozo", 8, 6), tiles),
            new MapEditorViewModel(new TileMap("Entero", 16, 6), tiles));
    }
}
