using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Mover el dibujo: espejos, giros y desplazamientos de un pixel.
/// </summary>
/// <remarks>
/// Lo que cambia de una operación a otra es qué pasa con los colores, que van por línea. El
/// espejo horizontal y el desplazamiento lateral no los tocan —nadie sale de su línea—; volcar
/// y desplazar arriba o abajo mueven las líneas enteras y se los llevan; y el giro no puede
/// llevárselos, porque acabarían en una columna y eso el VDP no sabe decirlo.
/// </remarks>
public class PatternMovesTests
{
    // ------------------------------------------------------------------ el tile

    [AvaloniaFact]
    public void El_espejo_horizontal_da_la_vuelta_a_cada_linea()
    {
        Tile tile = TileOf(
            "#..#....",
            "##......",
            "........",
            "........",
            "........",
            "........",
            "........",
            "........");

        tile.FlipHorizontal();

        Assert.Equal("....#..#", Line(tile, 0));
        Assert.Equal("......##", Line(tile, 1));
    }

    /// <summary>
    /// Volcar se lleva los colores de la línea.
    /// </summary>
    /// <remarks>
    /// Es la diferencia entre volcar el dibujo y volcar sólo su silueta: una figura con la
    /// cabeza roja y los pies azules tiene que volver con la cabeza abajo y roja.
    /// </remarks>
    [AvaloniaFact]
    public void El_espejo_vertical_se_lleva_los_colores_de_la_linea()
    {
        Tile tile = TileOf(
            "####....",
            "........",
            "........",
            "........",
            "........",
            "........",
            "........",
            "......##");

        tile.ArrayTileRows[0].ForeColor = 7;
        tile.ArrayTileRows[7].ForeColor = 2;

        tile.FlipVertical();

        Assert.Equal("......##", Line(tile, 0));
        Assert.Equal("####....", Line(tile, 7));

        Assert.Equal(2, tile.ArrayTileRows[0].ForeColor);
        Assert.Equal(7, tile.ArrayTileRows[7].ForeColor);
    }

    /// <summary>Girar a la derecha lleva la esquina de abajo a la izquierda arriba del todo.</summary>
    [AvaloniaFact]
    public void Girar_a_la_derecha_sube_la_esquina_de_abajo_a_la_izquierda()
    {
        Tile tile = TileOf(
            "........",
            "........",
            "........",
            "........",
            "........",
            "........",
            "........",
            "#.......");

        tile.Turn(clockwise: true);

        Assert.Equal("#.......", Line(tile, 0));
        Assert.Equal("........", Line(tile, 7));
    }

    /// <summary>Y girar al otro lado lo devuelve donde estaba.</summary>
    [AvaloniaFact]
    public void Girar_a_la_izquierda_deshace_el_giro_a_la_derecha()
    {
        string[] drawn =
        [
            "###.....",
            "#.......",
            "#..##...",
            "....#...",
            "........",
            ".....#..",
            "........",
            "#......#",
        ];

        Tile tile = TileOf(drawn);

        tile.Turn(clockwise: true);

        Assert.NotEqual(drawn, Lines(tile));

        tile.Turn(clockwise: false);

        Assert.Equal(drawn, Lines(tile));
    }

    /// <summary>
    /// Lo que sale por un lado vuelve a entrar por el otro.
    /// </summary>
    /// <remarks>
    /// En ocho pixeles es lo que hace útil el desplazamiento: se empuja el dibujo para ver
    /// cómo queda, y para dejarlo como estaba basta con empujarlo al revés. Perdiendo lo que
    /// se cae por el borde, ese camino de vuelta no existe.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(-1, 0, 7, 0)]
    [InlineData(1, 0, 1, 0)]
    [InlineData(0, -1, 0, 7)]
    [InlineData(0, 1, 0, 1)]
    public void Lo_que_sale_por_un_lado_entra_por_el_otro(int dx, int dy, int endX, int endY)
    {
        var tile = new Tile();

        tile.ArrayTileRows[0].ArrayPattern[0] = true;

        tile.Shift(dx, dy);

        Assert.True(
            tile.ArrayTileRows[endY].ArrayPattern[endX],
            $"Desplazando ({dx},{dy}) el pixel tenía que acabar en ({endX},{endY}) y está en "
            + string.Join(" ", Lines(tile)));

        Assert.Equal(1, Lines(tile).Sum(line => line.Count(pixel => pixel == '#')));
    }

    /// <summary>Desplazar arriba también se lleva los colores, que van con la línea.</summary>
    [AvaloniaFact]
    public void Desplazar_arriba_se_lleva_los_colores()
    {
        var tile = new Tile();

        tile.ArrayTileRows[0].ForeColor = 7;
        tile.ArrayTileRows[0].BackColor = 3;

        tile.Shift(0, -1);

        Assert.Equal(7, tile.ArrayTileRows[7].ForeColor);
        Assert.Equal(3, tile.ArrayTileRows[7].BackColor);
    }

    // ------------------------------------------------------------------ el patrón de sprite

    /// <summary>El sprite mide dieciséis, así que el espejo cruza las dieciséis columnas.</summary>
    [AvaloniaFact]
    public void El_espejo_horizontal_de_un_sprite_cruza_las_dieciseis_columnas()
    {
        var sprite = new Sprite();

        sprite.ArraySpriteRows[3].ArrayColumns[0] = true;

        sprite.FlipHorizontal();

        Assert.False(sprite.ArraySpriteRows[3].ArrayColumns[0]);
        Assert.True(sprite.ArraySpriteRows[3].ArrayColumns[SpriteRow.Columns - 1]);
    }

    [AvaloniaFact]
    public void El_espejo_vertical_de_un_sprite_se_lleva_el_color_de_la_linea()
    {
        var sprite = new Sprite();

        sprite.ArraySpriteRows[0].ArrayColumns[2] = true;
        sprite.ArraySpriteRows[0].Color = 7;

        sprite.FlipVertical();

        Assert.True(sprite.ArraySpriteRows[Sprite.Rows - 1].ArrayColumns[2]);
        Assert.Equal(7, sprite.ArraySpriteRows[Sprite.Rows - 1].Color);
    }

    /// <summary>
    /// El giro mueve los pixeles y deja los colores en su línea.
    /// </summary>
    /// <remarks>
    /// No es un olvido: el color va en la tabla de atributos, uno por línea, y un cuarto de
    /// vuelta lo mandaría a una columna. Un dibujo de un solo color gira exacto; uno con
    /// bandas de color gira con las bandas donde estaban, que es lo único que la máquina
    /// puede pintar. Queda escrito aquí para que no se «arregle» sin querer.
    /// </remarks>
    [AvaloniaFact]
    public void Girar_un_sprite_no_mueve_los_colores_de_linea()
    {
        var sprite = new Sprite();

        sprite.ArraySpriteRows[0].ArrayColumns[0] = true;
        sprite.ArraySpriteRows[0].Color = 7;
        sprite.ArraySpriteRows[15].Color = 2;

        sprite.Turn(clockwise: true);

        // El pixel sí se ha movido: estaba arriba a la izquierda y se va arriba a la derecha.
        Assert.False(sprite.ArraySpriteRows[0].ArrayColumns[0]);
        Assert.True(sprite.ArraySpriteRows[0].ArrayColumns[SpriteRow.Columns - 1]);

        Assert.Equal(7, sprite.ArraySpriteRows[0].Color);
        Assert.Equal(2, sprite.ArraySpriteRows[15].Color);
    }

    // ------------------------------------------------------------------ en el editor

    /// <summary>Un movimiento es un paso, y se deshace entero.</summary>
    [AvaloniaFact]
    public void Deshacer_devuelve_el_tile_como_estaba()
    {
        var editor = new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

        Draw(editor, (0, 0), (1, 0));

        editor.FlipHorizontalCommand.Execute(null);

        Assert.False(editor.PixelSurface.IsSet(0, 0));

        editor.UndoDrawingCommand.Execute(null);

        Assert.True(editor.PixelSurface.IsSet(0, 0));
        Assert.True(editor.PixelSurface.IsSet(1, 0));
        Assert.True(editor.CanRedoDrawing);
    }

    /// <summary>
    /// Y no se lleva por delante lo que hubiera antes en la pila.
    /// </summary>
    /// <remarks>
    /// Mover dice que el juego ha cambiado, y ese aviso tira la historia de todo lo que no
    /// pasa por la pila. Sin abrir el paso antes de mover, el trazo de antes dejaría de
    /// poder deshacerse en cuanto se tocara cualquiera de estos botones.
    /// </remarks>
    [AvaloniaFact]
    public void Mover_no_tira_la_historia_del_trazo()
    {
        var editor = new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

        Draw(editor, (0, 0));

        editor.ShiftRightCommand.Execute(null);

        Assert.True(editor.PixelSurface.IsSet(1, 0));

        editor.UndoDrawingCommand.Execute(null);

        // El desplazamiento vuelve atrás...
        Assert.True(
            editor.PixelSurface.IsSet(0, 0),
            "Deshacer el desplazamiento no ha devuelto el pixel: el paso no llegó a anotarse.");

        // ...y el trazo de antes sigue en la pila, que es lo que de verdad se comprueba aquí:
        // sin abrir el paso antes de mover, a estas alturas no quedaría nada que deshacer.
        Assert.True(
            editor.CanUndoDrawing,
            "Mover se ha llevado por delante la historia del trazo de antes.");

        editor.UndoDrawingCommand.Execute(null);

        Assert.False(editor.PixelSurface.IsSet(0, 0));
        Assert.False(editor.CanUndoDrawing);
    }

    /// <summary>
    /// Las casillas de color se enteran de que su línea ha cambiado de color.
    /// </summary>
    /// <remarks>
    /// Leen la línea a la que están enganchadas y siguen enganchadas a la misma, así que
    /// nada más les dice que lo que enseñan ya no es lo que hay. Se mira el aviso y no el
    /// valor: el valor se calcula al leerlo y sale bien aunque nadie avise, que es
    /// exactamente lo que la pantalla no hace.
    /// </remarks>
    [AvaloniaFact]
    public void Las_casillas_de_color_se_enteran_del_espejo_vertical()
    {
        var editor = new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

        bool told = false;

        editor.RowColors[0].PropertyChanged += (_, e) =>
            told |= e.PropertyName == nameof(TileRowColorViewModel.Foreground);

        editor.FlipVerticalCommand.Execute(null);

        Assert.True(told, "Nadie ha avisado a las casillas de color de que su línea ha cambiado.");

        told = false;

        editor.UndoDrawingCommand.Execute(null);

        Assert.True(told, "Al deshacer tampoco se avisa, y los colores vuelven sin que se vea.");
    }

    /// <summary>El patrón que enseña un grupo se repinta al moverlo.</summary>
    /// <remarks>
    /// La composición del grupo se dibuja aparte, así que mover el patrón sin recomponerla
    /// dejaría el grupo enseñando el dibujo de antes.
    /// </remarks>
    [AvaloniaFact]
    public void El_grupo_que_usa_el_patron_se_repinta_al_moverlo()
    {
        var editor = new SpritesEditorViewModel(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos"), ColorPalette.CreateMsxStandard());

        editor.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.SelectedGroup!;

        editor.PixelSurface.Set(0, 0, true);
        editor.PixelSurface.EndStroke();

        int painted = PixelReader.At(group.Preview, 0, 0);

        editor.FlipHorizontalCommand.Execute(null);

        Assert.NotEqual(painted, PixelReader.At(group.Preview, 0, 0));
    }

    /// <summary>Y deshacer devuelve el patrón del banco.</summary>
    [AvaloniaFact]
    public void Deshacer_devuelve_el_patron_como_estaba()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        var editor = new SpritesEditorViewModel(bank, ColorPalette.CreateMsxStandard());

        editor.PixelSurface.Set(0, 0, true);
        editor.PixelSurface.EndStroke();

        editor.RotateRightCommand.Execute(null);

        Assert.False(editor.PixelSurface.IsSet(0, 0));

        editor.UndoDrawingCommand.Execute(null);

        Assert.True(editor.PixelSurface.IsSet(0, 0));
    }

    // ------------------------------------------------------------------ en la ventana

    /// <summary>
    /// El panel cuelga de la barra y llega al tile que se está editando.
    /// </summary>
    /// <remarks>
    /// Va montado de verdad porque lo que se comprueba es justo lo que no se ve leyendo: el
    /// panel vive dentro de un Flyout, y lo que tiene delante le llega heredado del botón
    /// del que cuelga. Sin esa herencia los ocho botones saldrían dibujados y no harían nada.
    /// </remarks>
    [AvaloniaFact]
    public void El_panel_de_movimientos_llega_al_tile_desde_la_barra()
    {
        var editor = new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());
        var view = new TileSetEditorView { DataContext = editor };
        var window = new Window { Content = view, Width = 1400, Height = 900 };

        window.Show();
        Pump();

        Draw(editor, (0, 0));

        var button = view.FindControl<Button>("MovesButton");

        Assert.NotNull(button);

        var flyout = (Flyout)button!.Flyout!;

        flyout.ShowAt(button);
        Pump();

        var pad = (PatternMovesPad)flyout.Content!;

        Assert.Same(editor, pad.DataContext);

        Button flip = pad.FindControl<Button>("FlipHorizontalButton")!;

        Assert.NotNull(flip.Command);

        flip.Command!.Execute(flip.CommandParameter);

        Assert.False(editor.PixelSurface.IsSet(0, 0));
        Assert.True(editor.PixelSurface.IsSet(TileRow.Columns - 1, 0));

        flyout.Hide();
        window.Close();
        Pump();
    }

    /// <summary>Y en el editor de sprites, donde el panel cuelga de la tira de abajo.</summary>
    [AvaloniaFact]
    public void El_panel_de_movimientos_llega_al_patron_desde_la_tira()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        var editor = new SpritesEditorViewModel(bank, ColorPalette.CreateMsxStandard());
        var view = new SpritesEditorView { DataContext = editor };
        var window = new Window { Content = view, Width = 1400, Height = 900 };

        window.Show();
        Pump();

        editor.PixelSurface.Set(0, 0, true);
        editor.PixelSurface.EndStroke();

        var button = view.FindControl<Button>("MovesButton");

        Assert.NotNull(button);

        var flyout = (Flyout)button!.Flyout!;

        flyout.ShowAt(button);
        Pump();

        var pad = (PatternMovesPad)flyout.Content!;

        Assert.Same(editor, pad.DataContext);

        // Otro botón que el del editor de tiles: así entre las dos pruebas se comprueban las
        // dos mitades del panel, la cruz y los espejos.
        Button down = pad.FindControl<Button>("ShiftDownButton")!;

        Assert.NotNull(down.Command);

        down.Command!.Execute(down.CommandParameter);

        Assert.False(editor.PixelSurface.IsSet(0, 0));
        Assert.True(editor.PixelSurface.IsSet(0, 1));

        flyout.Hide();
        window.Close();
        Pump();
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un trazo en el tile que esté en el lienzo.</summary>
    private static void Draw(TileSetEditorViewModel editor, params (int X, int Y)[] pixels)
    {
        foreach ((int x, int y) in pixels)
            editor.PixelSurface.Set(x, y, true);

        editor.PixelSurface.EndStroke();
    }

    /// <summary>Un tile dibujado con almohadillas, que es como se lee de un vistazo.</summary>
    private static Tile TileOf(params string[] lines)
    {
        var tile = new Tile();

        for (int row = 0; row < Tile.Rows; row++)
        {
            for (int column = 0; column < TileRow.Columns; column++)
                tile.ArrayTileRows[row].ArrayPattern[column] = lines[row][column] == '#';
        }

        return tile;
    }

    private static string Line(Tile tile, int row) =>
        string.Concat(tile.ArrayTileRows[row].ArrayPattern.Select(on => on ? '#' : '.'));

    private static string[] Lines(Tile tile) =>
        [.. Enumerable.Range(0, Tile.Rows).Select(row => Line(tile, row))];

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
