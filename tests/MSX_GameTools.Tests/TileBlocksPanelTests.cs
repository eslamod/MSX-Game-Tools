using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>El panel de bloques: lista, rejilla de composición y selector de tiles.</summary>
public class TileBlocksPanelTests
{
    [AvaloniaFact]
    public void Arranca_sin_bloques_y_con_el_primer_tile_cogido()
    {
        TileBlocksViewModel panel = NewPanel();

        Assert.Empty(panel.Blocks);
        Assert.Null(panel.SelectedBlock);
        Assert.False(panel.DeleteBlockCommand.CanExecute(null));

        Assert.Equal(256, panel.Tiles.Count);
        Assert.Equal(TileBlock.MaxSide * TileBlock.MaxSide, panel.Cells.Count);

        // Con algo cogido de partida, pulsar en la rejilla hace algo desde el principio.
        Assert.Equal(0, panel.Selection[0, 0]);
        Assert.True(panel.Tiles[0].IsSelected);
    }

    [AvaloniaFact]
    public void Agregar_un_bloque_lo_deja_seleccionado_y_va_al_juego()
    {
        TileBlocksViewModel panel = NewPanel();

        panel.AddBlockCommand.Execute(null);
        panel.AddBlockCommand.Execute(null);

        Assert.Equal(["Bloque 1", "Bloque 2"], panel.Blocks.Select(block => block.Name));
        Assert.Equal("Bloque 2", panel.SelectedBlock!.Name);

        // El bloque vive en el juego de tiles, que es donde se guarda.
        Assert.Equal(2, panel.TileSet.Blocks.Count);
    }

    [AvaloniaFact]
    public void Eliminar_el_bloque_lo_quita_de_los_dos_sitios_y_mueve_la_seleccion()
    {
        TileBlocksViewModel panel = NewPanel();

        panel.AddBlockCommand.Execute(null);
        panel.AddBlockCommand.Execute(null);
        panel.DeleteBlockCommand.Execute(null);

        Assert.Single(panel.Blocks);
        Assert.Single(panel.TileSet.Blocks);
        Assert.Equal("Bloque 1", panel.SelectedBlock!.Name);

        panel.DeleteBlockCommand.Execute(null);

        Assert.Empty(panel.Blocks);
        Assert.Null(panel.SelectedBlock);
    }

    [AvaloniaFact]
    public void El_numero_libre_se_reutiliza_al_dar_nombre()
    {
        TileBlocksViewModel panel = NewPanel();

        panel.AddBlockCommand.Execute(null);
        panel.SelectedBlock!.Name = "Arbol";
        panel.AddBlockCommand.Execute(null);

        Assert.Equal("Bloque 1", panel.SelectedBlock!.Name);
    }

    // ------------------------------------------------------------------ componer

    [AvaloniaFact]
    public void Pintar_estampa_el_tile_cogido_y_estira_el_bloque()
    {
        TileBlocksViewModel panel = NewPanel();
        panel.AddBlockCommand.Execute(null);

        panel.SelectTile(37);
        panel.Paint(2, 1);

        Assert.Equal(37, panel.SelectedBlock!.Block[2, 1]);
        Assert.Equal(3, panel.SelectedBlock.Width);
        Assert.Equal(2, panel.SelectedBlock.Height);

        // Y la celda de la rejilla enseña la miniatura de ese tile.
        Assert.Same(panel.Tiles[37].Image, Cell(panel, 2, 1).Image);
        Assert.True(Cell(panel, 2, 1).IsInside);
        Assert.False(Cell(panel, 3, 1).IsInside);
    }

    /// <summary>Vaciar no es poner el tile 0: al estampar en el mapa son cosas distintas.</summary>
    [AvaloniaFact]
    public void Borrar_una_celda_la_deja_vacia_y_no_con_el_tile_0()
    {
        TileBlocksViewModel panel = NewPanel();
        panel.AddBlockCommand.Execute(null);

        panel.SelectTile(9);
        panel.Paint(0, 0);
        panel.Erase(0, 0);

        Assert.Null(panel.SelectedBlock!.Block[0, 0]);
        Assert.Null(Cell(panel, 0, 0).Image);
    }

    /// <summary>
    /// Tocar el tamaño a mano tiene que verse al momento. Antes no se enteraba nadie y
    /// sólo se apreciaba cambiando de bloque y volviendo, así que mover el ancho o el
    /// alto no daba ninguna señal de estar haciendo algo.
    /// </summary>
    [AvaloniaFact]
    public void Cambiar_el_tamano_a_mano_se_ve_al_momento()
    {
        TileBlocksViewModel panel = NewPanel();
        panel.AddBlockCommand.Execute(null);

        Assert.Equal("1 x 1", panel.SizeLabel);
        Assert.False(Cell(panel, 2, 0).IsInside);

        panel.SelectedBlock!.Width = 3;

        Assert.Equal("3 x 1", panel.SizeLabel);
        Assert.True(Cell(panel, 2, 0).IsInside);
    }

    /// <summary>Encoger olvida lo que queda fuera, y la rejilla tiene que enseñarlo.</summary>
    [AvaloniaFact]
    public void Encoger_el_bloque_vacia_las_celdas_que_deja_fuera()
    {
        TileBlocksViewModel panel = NewPanel();
        panel.AddBlockCommand.Execute(null);

        panel.SelectTile(11);
        panel.Paint(2, 0);

        Assert.NotNull(Cell(panel, 2, 0).Image);

        panel.SelectedBlock!.Width = 1;

        Assert.Null(Cell(panel, 2, 0).Image);
        Assert.False(Cell(panel, 2, 0).IsInside);
    }

    /// <summary>
    /// El tamaño no encoge solo al borrar, para no romper un supertile con una esquina
    /// vacía. Cuando de verdad sobra sitio, se pide con el botón.
    /// </summary>
    [AvaloniaFact]
    public void Ajustar_encoge_el_bloque_hasta_los_tiles_puestos()
    {
        TileBlocksViewModel panel = NewPanel();
        panel.AddBlockCommand.Execute(null);

        panel.SelectTile(20);
        panel.Paint(3, 2);
        panel.Erase(3, 2);
        panel.Paint(1, 0);

        Assert.Equal("4 x 3", panel.SizeLabel);

        panel.FitToContentCommand.Execute(null);

        Assert.Equal("2 x 1", panel.SizeLabel);
        Assert.Equal(20, panel.SelectedBlock!.Block[1, 0]);
        Assert.False(Cell(panel, 2, 0).IsInside);
    }

    [AvaloniaFact]
    public void Ajustar_un_bloque_vacio_lo_deja_en_una_celda()
    {
        TileBlocksViewModel panel = NewPanel();
        panel.AddBlockCommand.Execute(null);

        panel.SelectedBlock!.Width = 5;
        panel.FitToContentCommand.Execute(null);

        Assert.Equal("1 x 1", panel.SizeLabel);
    }

    [AvaloniaFact]
    public void Sin_bloque_no_se_puede_ajustar()
    {
        TileBlocksViewModel panel = NewPanel();

        Assert.False(panel.FitToContentCommand.CanExecute(null));

        panel.AddBlockCommand.Execute(null);

        Assert.True(panel.FitToContentCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Sin_bloque_seleccionado_no_hay_tamano_que_ensenar()
    {
        TileBlocksViewModel panel = NewPanel();

        Assert.Equal(string.Empty, panel.SizeLabel);

        panel.AddBlockCommand.Execute(null);
        panel.DeleteBlockCommand.Execute(null);

        Assert.Equal(string.Empty, panel.SizeLabel);
    }

    [AvaloniaFact]
    public void Sin_bloque_seleccionado_pintar_no_revienta()
    {
        TileBlocksViewModel panel = NewPanel();

        panel.Paint(0, 0);
        panel.Erase(0, 0);

        Assert.Null(panel.SelectedBlock);
    }

    [AvaloniaFact]
    public void Cambiar_de_bloque_cambia_lo_que_ensena_la_rejilla()
    {
        TileBlocksViewModel panel = NewPanel();

        panel.AddBlockCommand.Execute(null);
        panel.SelectTile(5);
        panel.Paint(0, 0);

        panel.AddBlockCommand.Execute(null);

        Assert.Null(Cell(panel, 0, 0).Image);

        panel.SelectedBlock = panel.Blocks[0];

        Assert.Same(panel.Tiles[5].Image, Cell(panel, 0, 0).Image);
    }

    // ------------------------------------------------------------------ seleccionar

    /// <summary>
    /// El rectángulo se coge sobre las 32 columnas del selector, que son las del editor y
    /// las del png. Los tiles 1 y 34 son las esquinas de un cuadrado de 2x2.
    /// </summary>
    [AvaloniaFact]
    public void Seleccionar_por_rectangulo_coge_el_cuadrado_de_la_disposicion_de_32()
    {
        TileBlocksViewModel panel = NewPanel();

        panel.SelectRange(1, 34);

        Assert.Equal(2, panel.Selection.Width);
        Assert.Equal(2, panel.Selection.Height);
        Assert.Equal(1, panel.Selection[0, 0]);
        Assert.Equal(2, panel.Selection[1, 0]);
        Assert.Equal(33, panel.Selection[0, 1]);
        Assert.Equal(34, panel.Selection[1, 1]);

        Assert.True(panel.Tiles[33].IsSelected);
        Assert.False(panel.Tiles[3].IsSelected);
    }

    [AvaloniaFact]
    public void El_rectangulo_sale_igual_arrastrando_hacia_atras()
    {
        TileBlocksViewModel panel = NewPanel();

        panel.SelectRange(34, 1);

        Assert.Equal(1, panel.Selection[0, 0]);
        Assert.Equal(34, panel.Selection[1, 1]);
    }

    [AvaloniaFact]
    public void Estampar_una_seleccion_de_varios_pone_todos_los_tiles()
    {
        TileBlocksViewModel panel = NewPanel();
        panel.AddBlockCommand.Execute(null);

        panel.SelectRange(0, 33);
        panel.Paint(1, 1);

        Assert.Equal(0, panel.SelectedBlock!.Block[1, 1]);
        Assert.Equal(1, panel.SelectedBlock.Block[2, 1]);
        Assert.Equal(32, panel.SelectedBlock.Block[1, 2]);
        Assert.Equal(33, panel.SelectedBlock.Block[2, 2]);
        Assert.Equal((3, 3), (panel.SelectedBlock.Width, panel.SelectedBlock.Height));
    }

    [AvaloniaFact]
    public void Coger_un_tile_suelto_deshace_la_seleccion_anterior()
    {
        TileBlocksViewModel panel = NewPanel();

        panel.SelectRange(0, 33);
        panel.SelectTile(7);

        Assert.Equal((1, 1), (panel.Selection.Width, panel.Selection.Height));
        Assert.Equal(7, panel.Selection[0, 0]);
        Assert.True(panel.Tiles[7].IsSelected);
        Assert.False(panel.Tiles[0].IsSelected);
    }

    /// <summary>Los bloques que ya traía el juego salen en la lista al abrir el panel.</summary>
    [AvaloniaFact]
    public void Un_juego_con_bloques_los_ensena_al_abrir()
    {
        var tileSet = new TileSet("Bosque");
        tileSet.Blocks.Add(new TileBlock("Arbol") { [1, 1] = 12 });

        var panel = new TileBlocksViewModel(new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard()));

        Assert.Equal("Arbol", Assert.Single(panel.Blocks).Name);
        Assert.Same(panel.Tiles[12].Image, Cell(panel, 1, 1).Image);
    }

    private static BlockCellViewModel Cell(TileBlocksViewModel panel, int column, int row) =>
        panel.Cells.Single(cell => cell.Column == column && cell.Row == row);

    private static TileBlocksViewModel NewPanel() =>
        new(new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard()));
}
