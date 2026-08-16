using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Llevarse un patrón de un banco de sprites a otro.
/// </summary>
/// <remarks>
/// <para>
/// El gesto ya existía —copiar aquí, pegar allí— y lo único que le faltaba era cruzar la
/// pestaña. Aquí no hay nada que congelar al salir, a diferencia de los tiles: lo que se
/// copia ya es una copia suelta desde que se copia, así que basta con que el sitio donde se
/// guarda sea la ventana y no el banco.
/// </para>
/// <para>
/// Lo que sí cambia al cruzar es el tipo de banco. En MSX1 el color va en el byte de atributo
/// del sprite, uno para todo el patrón, así que un patrón MSX2 de varios colores no cabe
/// entero. Se avisa antes y se aplana, para que el lienzo enseñe lo que la máquina pinta.
/// </para>
/// </remarks>
public class CrossBankSpriteCopyTests
{
    /// <summary>Pinta un aspa reconocible con un color por línea, para saber qué ha llegado.</summary>
    private static void Draw(Sprite sprite, int firstColor)
    {
        for (int row = 0; row < Sprite.Rows; row++)
        {
            sprite.ArraySpriteRows[row].ArrayColumns[row] = true;
            sprite.ArraySpriteRows[row].Color = firstColor;
        }
    }

    /// <summary>Le da a cada línea un color distinto, que es lo que MSX1 no puede tener.</summary>
    private static void PaintEveryRow(Sprite sprite)
    {
        for (int row = 0; row < Sprite.Rows; row++)
            sprite.ArraySpriteRows[row].Color = 1 + (row % 8);
    }

    private static bool SameDrawing(Sprite one, Sprite other)
    {
        for (int row = 0; row < Sprite.Rows; row++)
        {
            for (int column = 0; column < SpriteRow.Columns; column++)
            {
                if (one.ArraySpriteRows[row].ArrayColumns[column]
                    != other.ArraySpriteRows[row].ArrayColumns[column])
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Deja el editor en ese hueco, contando desde 0 como cuenta el banco.</summary>
    private static void GoToIndex(SpritesEditorViewModel editor, int index)
    {
        while (editor.CurrentSpriteIndex < index)
            editor.NextSpriteCommand.Execute(null);
    }

    /// <summary>Lo que se pidió: copiar un patrón en un banco y pegarlo en otro.</summary>
    [AvaloniaFact]
    public async Task Lo_copiado_en_un_banco_se_pega_en_otro()
    {
        var main = new MainWindowViewModel();

        SpritesEditorViewModel from = main.OpenSpriteBank(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos"));

        SpritesEditorViewModel to = main.OpenSpriteBank(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Naves"));

        Draw(from.SpritesBank.SpritesList[0], firstColor: 7);

        main.SelectedTab = from;
        from.CopySpriteCommand.Execute(null);

        main.SelectedTab = to;
        GoToIndex(to, 20);
        await to.PasteSpriteCommand.ExecuteAsync(null);

        Assert.True(SameDrawing(from.SpritesBank.SpritesList[0], to.SpritesBank.SpritesList[20]));
        Assert.Equal(7, to.SpritesBank.SpritesList[20].ArraySpriteRows[0].Color);
    }

    /// <summary>
    /// Y el botón de pegar se enciende en el banco de destino.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El banco de destino ya estaba abierto cuando se copió, así que nadie le ha dicho que
    /// ahora hay algo que pegar. Sin avisarle al ponerlo delante, el botón se queda apagado y
    /// la función no existe por mucho que el portapapeles esté lleno.
    /// </para>
    /// <para>
    /// Lo que se mira es el aviso y no CanExecute: el predicado se evalúa en cada llamada, así
    /// que preguntándoselo a mano sale que sí aunque nadie haya avisado. Un botón enlazado no
    /// vuelve a preguntar por su cuenta, y era ahí donde estaba el fallo.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Pegar_se_enciende_en_el_banco_de_destino()
    {
        var main = new MainWindowViewModel();

        SpritesEditorViewModel from = main.OpenSpriteBank(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos"));

        SpritesEditorViewModel to = main.OpenSpriteBank(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Naves"));

        Assert.False(to.PasteSpriteCommand.CanExecute(null));

        main.SelectedTab = from;
        from.CopySpriteCommand.Execute(null);

        int notices = 0;
        to.PasteSpriteCommand.CanExecuteChanged += (_, _) => notices++;

        main.SelectedTab = to;

        Assert.True(notices > 0, "el botón de pegar no se ha enterado de que hay algo que pegar");
        Assert.True(to.PasteSpriteCommand.CanExecute(null));
        Assert.True(to.HasCopiedSprite);
    }

    /// <summary>Lo copiado sigue siendo una copia suelta también cruzando de banco.</summary>
    [AvaloniaFact]
    public async Task Retocar_el_original_no_cambia_lo_que_se_pega()
    {
        var main = new MainWindowViewModel();

        SpritesEditorViewModel from = main.OpenSpriteBank(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos"));

        SpritesEditorViewModel to = main.OpenSpriteBank(
            new SpriteBank(SpriteBank.SpriteType.MSX2, "Naves"));

        Draw(from.SpritesBank.SpritesList[0], firstColor: 7);

        main.SelectedTab = from;
        from.CopySpriteCommand.Execute(null);

        from.SpritesBank.SpritesList[0].ArraySpriteRows[0].Color = 2;

        main.SelectedTab = to;
        await to.PasteSpriteCommand.ExecuteAsync(null);

        Assert.Equal(7, to.SpritesBank.SpritesList[0].ArraySpriteRows[0].Color);
    }

    // ------------------------------------------------------------------ de MSX2 a MSX1

    /// <summary>
    /// Pegar un patrón de varios colores en un banco MSX1 avisa y lo deja de un color.
    /// </summary>
    /// <remarks>
    /// Se deja pegar en vez de impedirlo: llevarse el dibujo de un sprite MSX2 a un banco
    /// MSX1 para recolorearlo a mano es un caso legítimo, y es el dibujo lo que cuesta de
    /// hacer. Aplanarlo al entrar y no al exportar es lo que hace que el lienzo enseñe lo que
    /// la máquina va a pintar.
    /// </remarks>
    [AvaloniaFact]
    public async Task De_msx2_a_msx1_avisa_y_deja_un_solo_color()
    {
        var dialogs = new TestDialogService();
        var clipboard = new SpriteClipboard();

        var msx2 = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        var msx1 = new SpriteBank(SpriteBank.SpriteType.MSX, "Naves");

        var from = new SpritesEditorViewModel(
            msx2, ColorPalette.CreateMsxStandard(), dialogs, null, null, clipboard);

        var to = new SpritesEditorViewModel(
            msx1, ColorPalette.CreateMsxStandard(), dialogs, null, null, clipboard);

        Draw(msx2.SpritesList[0], firstColor: 7);
        PaintEveryRow(msx2.SpritesList[0]);

        int first = msx2.SpritesList[0].ArraySpriteRows[0].Color;

        from.CopySpriteCommand.Execute(null);

        await to.PasteSpriteCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ConfirmCalls);

        // El dibujo llega entero; el color, el de la primera línea en las 16.
        Assert.True(SameDrawing(msx2.SpritesList[0], msx1.SpritesList[0]));
        Assert.All(msx1.SpritesList[0].ArraySpriteRows, row => Assert.Equal(first, row.Color));
    }

    /// <summary>Y si se dice que no, el destino se queda como estaba.</summary>
    [AvaloniaFact]
    public async Task De_msx2_a_msx1_decir_que_no_no_pega_nada()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = false };
        var clipboard = new SpriteClipboard();

        var msx2 = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        var msx1 = new SpriteBank(SpriteBank.SpriteType.MSX, "Naves");

        var from = new SpritesEditorViewModel(
            msx2, ColorPalette.CreateMsxStandard(), dialogs, null, null, clipboard);

        var to = new SpritesEditorViewModel(
            msx1, ColorPalette.CreateMsxStandard(), dialogs, null, null, clipboard);

        Draw(msx2.SpritesList[0], firstColor: 7);
        PaintEveryRow(msx2.SpritesList[0]);

        from.CopySpriteCommand.Execute(null);

        await to.PasteSpriteCommand.ExecuteAsync(null);

        Assert.True(msx1.SpritesList[0].IsEmpty);
    }

    /// <summary>
    /// Un patrón que ya iba de un color entra en un banco MSX1 sin avisar de nada.
    /// </summary>
    /// <remarks>
    /// El aviso es por lo que se pierde, no por el tipo del banco. Avisar cuando no se pierde
    /// nada enseña a contestar que sí sin leer, y entonces el aviso deja de servir para el
    /// caso en que sí importa.
    /// </remarks>
    [AvaloniaFact]
    public async Task De_msx2_a_msx1_sin_perder_color_no_avisa()
    {
        var dialogs = new TestDialogService();
        var clipboard = new SpriteClipboard();

        var msx2 = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        var msx1 = new SpriteBank(SpriteBank.SpriteType.MSX, "Naves");

        var from = new SpritesEditorViewModel(
            msx2, ColorPalette.CreateMsxStandard(), dialogs, null, null, clipboard);

        var to = new SpritesEditorViewModel(
            msx1, ColorPalette.CreateMsxStandard(), dialogs, null, null, clipboard);

        Draw(msx2.SpritesList[0], firstColor: 7);

        from.CopySpriteCommand.Execute(null);

        await to.PasteSpriteCommand.ExecuteAsync(null);

        Assert.Equal(0, dialogs.ConfirmCalls);
        Assert.Equal(7, msx1.SpritesList[0].ArraySpriteRows[0].Color);
    }

    /// <summary>De MSX1 a MSX2 tampoco avisa: allí cabe todo lo que venga.</summary>
    [AvaloniaFact]
    public async Task De_msx1_a_msx2_no_avisa()
    {
        var dialogs = new TestDialogService();
        var clipboard = new SpriteClipboard();

        var msx1 = new SpriteBank(SpriteBank.SpriteType.MSX, "Naves");
        var msx2 = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");

        var from = new SpritesEditorViewModel(
            msx1, ColorPalette.CreateMsxStandard(), dialogs, null, null, clipboard);

        var to = new SpritesEditorViewModel(
            msx2, ColorPalette.CreateMsxStandard(), dialogs, null, null, clipboard);

        Draw(msx1.SpritesList[0], firstColor: 4);

        from.CopySpriteCommand.Execute(null);

        await to.PasteSpriteCommand.ExecuteAsync(null);

        Assert.Equal(0, dialogs.ConfirmCalls);
        Assert.True(SameDrawing(msx1.SpritesList[0], msx2.SpritesList[0]));
    }

    /// <summary>
    /// Machacar un dibujo y perder los colores caben en un solo aviso.
    /// </summary>
    /// <remarks>
    /// Dos preguntas seguidas por la misma pulsación se contestan que sí sin leer la segunda,
    /// que es justo lo que el aviso trata de evitar.
    /// </remarks>
    [AvaloniaFact]
    public async Task Los_dos_motivos_caben_en_un_solo_aviso()
    {
        var dialogs = new TestDialogService();
        var clipboard = new SpriteClipboard();

        var msx2 = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        var msx1 = new SpriteBank(SpriteBank.SpriteType.MSX, "Naves");

        var from = new SpritesEditorViewModel(
            msx2, ColorPalette.CreateMsxStandard(), dialogs, null, null, clipboard);

        var to = new SpritesEditorViewModel(
            msx1, ColorPalette.CreateMsxStandard(), dialogs, null, null, clipboard);

        Draw(msx2.SpritesList[0], firstColor: 7);
        PaintEveryRow(msx2.SpritesList[0]);

        // El destino tiene dibujo, así que también habría aviso por machacarlo.
        Draw(msx1.SpritesList[0], firstColor: 3);

        from.CopySpriteCommand.Execute(null);

        await to.PasteSpriteCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ConfirmCalls);
    }

    /// <summary>
    /// Al pegar, la columna de colores por línea enseña los que acaban de llegar.
    /// </summary>
    /// <remarks>
    /// Las casillas se crean una vez y se reutilizan, así que leen el color de la línea a la
    /// que estén apuntando. Pegar cambia esas 16 líneas por debajo sin que la casilla se
    /// entere, y la columna se quedaba con los colores de antes hasta cambiar de patrón y
    /// volver.
    /// </remarks>
    [AvaloniaFact]
    public async Task Pegar_refresca_la_columna_de_colores()
    {
        var dialogs = new TestDialogService();
        var clipboard = new SpriteClipboard();

        var origin = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        var target = new SpriteBank(SpriteBank.SpriteType.MSX2, "Naves");

        var from = new SpritesEditorViewModel(
            origin, ColorPalette.CreateMsxStandard(), dialogs, null, null, clipboard);

        var to = new SpritesEditorViewModel(
            target, ColorPalette.CreateMsxStandard(), dialogs, null, null, clipboard);

        PaintEveryRow(origin.SpritesList[0]);

        from.CopySpriteCommand.Execute(null);

        SpriteRowColorViewModel cell = to.RowColors[5];

        int notices = 0;
        cell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SpriteRowColorViewModel.Color))
                notices++;
        };

        await to.PasteSpriteCommand.ExecuteAsync(null);

        Assert.Equal(
            origin.SpritesList[0].ArraySpriteRows[5].Color,
            target.SpritesList[0].ArraySpriteRows[5].Color);

        Assert.True(notices > 0, "la casilla no se ha enterado de que su línea ha cambiado de color");
    }
}
