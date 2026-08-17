using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El formulario de traer una hoja de sprites.
/// </summary>
/// <remarks>
/// Lo que lo hace usable es que el informe se rehaga con cada cambio: arrastras el rectángulo y
/// ves al momento si entra en los 64 huecos. Por eso casi todo lo que se comprueba aquí es que
/// los números de abajo siguen a lo que se elige arriba.
/// </remarks>
public class ImportSpriteSheetTests
{
    private const int Cell = 8;

    private static readonly Color White = Color.FromRgb(255, 255, 255);
    private static readonly Color Black = Color.FromRgb(0, 0, 0);
    private static readonly Color Red = Color.FromRgb(255, 0, 0);
    private static readonly Color Clear = Color.FromRgb(0, 255, 0);

    /// <summary>Una hoja de celdas de 8x8, cada una con los colores que se le digan.</summary>
    private static ImportSpriteSheetViewModel Form(MainWindowViewModel main, params Color[][] cells)
    {
        // Dos filas de alto: con una sola, las celdas de 16 no caben en la hoja y no se
        // podria comprobar que cambiar de lado de celda recoloca el rectangulo.
        var size = new PixelSize(cells.Length * Cell, Cell * 2);
        int[] pixels = new int[size.Width * size.Height];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Bgra(Clear);

        for (int cell = 0; cell < cells.Length; cell++)
        {
            for (int line = 0; line < Cell; line++)
            {
                for (int x = 0; x < cells[cell].Length; x++)
                    pixels[(line * size.Width) + (cell * Cell) + x] = Bgra(cells[cell][x]);
            }
        }

        // Un bitmap vacío del tamaño de la hoja: la vista lo enseña, pero para lo que se
        // comprueba aquí sólo hace falta que exista.
        var source = new WriteableBitmap(
            size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        var form = new ImportSpriteSheetViewModel(main, "bichos.png", pixels, size, source)
        {
            CellSize = Cell,
        };

        return form;
    }

    private static int Bgra(Color color) =>
        unchecked((int)0xFF000000) | (color.R << 16) | (color.G << 8) | color.B;

    private static MainWindowViewModel Main() => new(new TestDialogService());

    // ------------------------------------------------------------------ lo que trae puesto

    /// <summary>
    /// El transparente que sale elegido es el color más usado de la hoja.
    /// </summary>
    /// <remarks>
    /// Casi siempre es el fondo, así que la mayoría de las hojas no hay ni que tocarlas. Con
    /// otro por defecto, el fondo se llevaría un índice de paleta y un plano entero para nada.
    /// </remarks>
    [AvaloniaFact]
    public void El_transparente_que_sale_elegido_es_el_mas_usado()
    {
        ImportSpriteSheetViewModel form = Form(Main(), [White, Black, Red]);

        Assert.Equal(Clear, form.Transparent!.Color);
    }

    /// <summary>La retícula sale del lado de celda y del tamaño de la hoja.</summary>
    [AvaloniaFact]
    public void La_reticula_sale_del_lado_de_celda()
    {
        ImportSpriteSheetViewModel form = Form(Main(), [White], [Black], [Red]);

        Assert.Equal(3, form.Columns);
        Assert.Equal(2, form.Rows);

        form.CellSize = 16;

        Assert.Equal(1, form.Columns);
        Assert.Equal(1, form.Rows);
    }

    // ------------------------------------------------------------------ el rectangulo

    /// <summary>El rectángulo elegido no se sale de la hoja, ni de ancho ni de alto.</summary>
    /// <remarks>
    /// La hoja de la prueba son dos columnas por dos filas de celdas, así que pedir cinco por
    /// cinco desde la segunda columna tiene que quedarse en una columna y dos filas: se recorta
    /// por cada lado a lo que hay desde donde se empieza.
    /// </remarks>
    [AvaloniaFact]
    public void El_rectangulo_se_recorta_a_la_hoja()
    {
        ImportSpriteSheetViewModel form = Form(Main(), [White], [Black]);

        form.Select(1, 0, 5, 5);

        Assert.Equal(new SheetSelection(1, 0, 1, 2), form.Selection);

        // Y una esquina fuera de la hoja cae dentro, en la celda de más allá.
        form.Select(9, 9, 1, 1);

        Assert.Equal(new SheetSelection(1, 1, 1, 1), form.Selection);
    }

    /// <summary>
    /// Cambiar el lado de celda deja el rectángulo dentro de la retícula nueva.
    /// </summary>
    /// <remarks>
    /// Con celdas de 8 hay el doble de columnas que con 16. Sin recolocarlo, un rectángulo
    /// elegido en la retícula fina se quedaría señalando fuera de la gorda y el análisis
    /// diría que se sale, cuando lo que ha pasado es que has cambiado de retícula.
    /// </remarks>
    [AvaloniaFact]
    public void Cambiar_la_celda_recoloca_el_rectangulo()
    {
        ImportSpriteSheetViewModel form = Form(Main(), [White], [Black], [Red], [White]);

        form.Select(3, 0, 1, 1);
        form.CellSize = 16;

        Assert.True(form.Selection.Left < form.Columns);
        Assert.True(form.Analysis.Ok, string.Join(" / ", form.Analysis.Problems));
    }

    // ------------------------------------------------------------------ el informe

    /// <summary>El informe se rehace al mover el rectángulo.</summary>
    [AvaloniaFact]
    public void El_informe_sigue_al_rectangulo()
    {
        ImportSpriteSheetViewModel form = Form(Main(), [White, Black, Red], [White]);

        form.Select(1, 0, 1, 1);
        int alone = form.Analysis.Patterns;

        form.Select(0, 0, 2, 1);

        Assert.True(form.Analysis.Patterns > alone);
    }

    /// <summary>Y al cambiar el tope de planos, que es lo que decide si sale o no.</summary>
    [AvaloniaFact]
    public void El_informe_sigue_al_tope_de_planos()
    {
        ImportSpriteSheetViewModel form = Form(Main(), [White, Black, Red, Color.FromRgb(0, 0, 255)]);

        form.Select(0, 0, 1, 1);
        form.MaxPlanes = 2;

        Assert.False(form.Analysis.Ok);
        Assert.False(form.CanAccept);

        form.MaxPlanes = 3;

        Assert.True(form.Analysis.Ok, string.Join(" / ", form.Analysis.Problems));
        Assert.True(form.CanAccept);
    }

    /// <summary>Con un rectángulo que no cabe, el informe lo dice y no se deja aceptar.</summary>
    [AvaloniaFact]
    public void Lo_que_no_cabe_no_se_deja_aceptar()
    {
        // Cada celda con un dibujo distinto para que no se reaprovechen patrones.
        Color[][] cells = [.. Enumerable.Range(0, 40).Select(i => (Color[])
        [
            .. Enumerable.Repeat(White, (i % 5) + 1),
            .. Enumerable.Repeat(Black, (i % 3) + 1),
            Red,
        ])];

        ImportSpriteSheetViewModel form = Form(Main(), cells);

        form.MaxPlanes = 2;
        form.Select(0, 0, 40, 1);

        Assert.False(form.CanAccept);
        Assert.False(form.AcceptCommand.CanExecute(null));
        Assert.True(form.Analysis.Patterns > SpriteBank.MaxSprites);
    }

    // ------------------------------------------------------------------ aceptar

    /// <summary>Aceptar abre el banco con sus grupos y su paleta, y cierra el panel.</summary>
    [AvaloniaFact]
    public void Aceptar_abre_el_banco_con_sus_grupos()
    {
        MainWindowViewModel main = Main();
        ImportSpriteSheetViewModel form = Form(main, [White, Black, Red], [White, Black, Red]);

        form.Select(0, 0, 2, 1);

        Assert.True(form.CanAccept);

        form.AcceptCommand.Execute(null);

        SpritesEditorViewModel bank = Assert.Single(main.Tabs.OfType<SpritesEditorViewModel>());

        Assert.Equal(2, bank.SpritesBank.Groups.Count);
        Assert.Null(main.RightPanViewModel);
    }

    // ------------------------------------------------------------------ solo patrones

    /// <summary>
    /// En modo patrones no hay color: un bit donde la hoja pinta y nada donde no.
    /// </summary>
    /// <remarks>
    /// Es para cuando el dibujo se colorea en el juego, o cuando la hoja viene ya en blanco y
    /// negro y el color no significa nada.
    /// </remarks>
    [AvaloniaFact]
    public void En_modo_patrones_solo_viaja_el_dibujo()
    {
        MainWindowViewModel main = Main();
        ImportSpriteSheetViewModel form = Form(main, [White, Black, Red]);

        form.OnlyPatterns = true;
        form.Select(0, 0, 1, 1);
        form.AcceptCommand.Execute(null);

        SpritesEditorViewModel bank = Assert.Single(main.Tabs.OfType<SpritesEditorViewModel>());
        SpriteRow line = bank.SpritesBank.SpritesList[0].ArraySpriteRows[0];

        // Los tres colores de la celda son un bit cada uno; el resto de la línea, transparente.
        Assert.True(line.ArrayColumns[0]);
        Assert.True(line.ArrayColumns[1]);
        Assert.True(line.ArrayColumns[2]);
        Assert.False(line.ArrayColumns[3]);

        // Y sin grupos, que es lo que distingue una tabla de patrones de un personaje.
        Assert.Empty(bank.SpritesBank.Groups);
    }

    /// <summary>
    /// Un patrón por celda y en orden, sin saltarse las vacías ni juntar las repetidas.
    /// </summary>
    /// <remarks>
    /// Al revés que el modo de color, y a propósito: de una tabla se espera que el patrón
    /// número N sea la celda número N de lo que se eligió. Saltarse una rompería esa cuenta sin
    /// decir nada, y es una cuenta que el código del juego usa.
    /// </remarks>
    [AvaloniaFact]
    public void En_modo_patrones_el_numero_se_corresponde_con_la_celda()
    {
        MainWindowViewModel main = Main();

        // La segunda celda vacía y la tercera igual que la primera.
        ImportSpriteSheetViewModel form = Form(main, [White, Black], [], [White, Black]);

        form.OnlyPatterns = true;
        form.Select(0, 0, 3, 1);

        Assert.Equal(3, form.Analysis.Patterns);

        form.AcceptCommand.Execute(null);

        SpritesEditorViewModel bank = Assert.Single(main.Tabs.OfType<SpritesEditorViewModel>());

        Assert.True(bank.SpritesBank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[0]);
        Assert.False(bank.SpritesBank.SpritesList[1].ArraySpriteRows[0].ArrayColumns[0]);
        Assert.True(bank.SpritesBank.SpritesList[2].ArraySpriteRows[0].ArrayColumns[0]);
    }

    /// <summary>
    /// En modo patrones no importa cuántos colores traiga la hoja.
    /// </summary>
    /// <remarks>
    /// Es la razón de ser del modo: una hoja de cincuenta colores no se puede traer con color,
    /// y en blanco y negro entra sin problema.
    /// </remarks>
    [AvaloniaFact]
    public void En_modo_patrones_los_colores_no_estorban()
    {
        Color[] many = [.. Enumerable.Range(1, 16).Select(i => Color.FromRgb((byte)(i * 15), 0, 0))];

        ImportSpriteSheetViewModel form = Form(Main(), many);

        form.Select(0, 0, 1, 1);

        Assert.False(form.Analysis.Ok);

        form.OnlyPatterns = true;

        Assert.True(form.Analysis.Ok, string.Join(" / ", form.Analysis.Problems));
        Assert.True(form.CanAccept);
    }

    /// <summary>Y el tope de los 64 huecos sigue mandando igual.</summary>
    [AvaloniaFact]
    public void En_modo_patrones_tambien_hay_sesenta_y_cuatro_huecos()
    {
        Color[][] cells = [.. Enumerable.Range(0, 70).Select(_ => new[] { White })];

        ImportSpriteSheetViewModel form = Form(Main(), cells);

        form.OnlyPatterns = true;
        form.Select(0, 0, 70, 1);

        Assert.Equal(70, form.Analysis.Patterns);
        Assert.False(form.CanAccept);
    }

    /// <summary>Y cancelar no deja nada abierto.</summary>
    [AvaloniaFact]
    public void Cancelar_no_deja_nada()
    {
        MainWindowViewModel main = Main();
        ImportSpriteSheetViewModel form = Form(main, [White, Black, Red]);

        main.RightPanViewModel = form;

        form.CancelCommand.Execute(null);

        Assert.Null(main.RightPanViewModel);
        Assert.Empty(main.Tabs.OfType<SpritesEditorViewModel>());
    }
}
