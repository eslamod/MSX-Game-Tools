using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El banco de 64 huecos fijos: duplicar en el primero libre y vaciar sin mover nada.
/// </summary>
/// <remarks>
/// La tabla de patrones del VDP es una región de tamaño fijo y el patrón N vive en un sitio
/// fijo. Por eso el banco no es una lista que crece y encoge: no hay ninguna operación que
/// pueda mover el número de un patrón, que es lo que descolocaba los grupos y, sobre todo,
/// el código del juego.
/// </remarks>
public class DuplicateSpriteTests
{
    private static SpritesEditorViewModel Bank(IDialogAnswers? dialogs = null)
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");

        return new SpritesEditorViewModel(
            bank, ColorPalette.CreateMsxStandard(), dialogs?.Service);
    }

    /// <summary>Envoltorio para poder pasar el servicio de diálogos sólo cuando hace falta.</summary>
    internal sealed record IDialogAnswers(TestDialogService Service);

    /// <summary>Pinta un aspa reconocible y le da color a la línea, para saber qué ha llegado.</summary>
    private static void Draw(Sprite sprite, int color)
    {
        for (int row = 0; row < Sprite.Rows; row++)
        {
            sprite.ArraySpriteRows[row].ArrayColumns[row] = true;
            sprite.ArraySpriteRows[row].Color = color;
        }
    }

    private static bool SameDrawing(Sprite one, Sprite other)
    {
        for (int row = 0; row < Sprite.Rows; row++)
        {
            if (one.ArraySpriteRows[row].Color != other.ArraySpriteRows[row].Color)
                return false;

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

    // ------------------------------------------------------------------ los 64 huecos

    /// <summary>
    /// Un banco tiene 64 patrones desde que nace, y ninguna operación cambia esa cuenta.
    /// </summary>
    /// <remarks>
    /// Es la propiedad de la que sale todo lo demás: si el número de patrones no cambia
    /// nunca, ningún índice se puede mover.
    /// </remarks>
    [AvaloniaFact]
    public void El_banco_tiene_siempre_sesenta_y_cuatro()
    {
        SpritesEditorViewModel editor = Bank();

        Assert.Equal(SpriteBank.MaxSprites, editor.SpritesBank.SpritesList.Count);

        editor.DuplicateSpriteCommand.Execute(null);

        Assert.Equal(SpriteBank.MaxSprites, editor.SpritesBank.SpritesList.Count);
    }

    /// <summary>Y nacen todos en blanco, que es lo que los distingue de los que se usan.</summary>
    [AvaloniaFact]
    public void Nacen_todos_en_blanco()
    {
        SpritesEditorViewModel editor = Bank();

        Assert.All(editor.SpritesBank.SpritesList, sprite => Assert.True(sprite.IsEmpty));

        Draw(editor.SpritesBank.SpritesList[3], color: 7);

        Assert.False(editor.SpritesBank.SpritesList[3].IsEmpty);
    }

    // ------------------------------------------------------------------ duplicar

    [AvaloniaFact]
    public void La_copia_lleva_el_dibujo_y_los_colores()
    {
        SpritesEditorViewModel editor = Bank();
        SpriteBank bank = editor.SpritesBank;

        Draw(bank.SpritesList[0], color: 7);

        editor.DuplicateSpriteCommand.Execute(null);

        Assert.True(SameDrawing(bank.SpritesList[0], bank.SpritesList[1]));
    }

    /// <summary>
    /// La copia es una copia: retocarla no toca el original.
    /// </summary>
    /// <remarks>
    /// Es lo único que hace útil duplicar. Compartiendo las filas, dibujar en la variación
    /// cambiaría el sprite del que salió y no se vería hasta mirar el otro.
    /// </remarks>
    [AvaloniaFact]
    public void Retocar_la_copia_no_toca_el_original()
    {
        SpritesEditorViewModel editor = Bank();
        SpriteBank bank = editor.SpritesBank;

        Draw(bank.SpritesList[0], color: 7);

        editor.DuplicateSpriteCommand.Execute(null);

        bank.SpritesList[1].ArraySpriteRows[0].ArrayColumns[15] = true;
        bank.SpritesList[1].ArraySpriteRows[0].Color = 3;

        Assert.False(bank.SpritesList[0].ArraySpriteRows[0].ArrayColumns[15]);
        Assert.Equal(7, bank.SpritesList[0].ArraySpriteRows[0].Color);
    }

    /// <summary>
    /// La copia cae en el primer hueco libre, saltándose los que ya se usan.
    /// </summary>
    /// <remarks>
    /// No detrás del original: detrás habría que correr los de atrás, y correr un índice es
    /// justo lo que este banco ya no hace.
    /// </remarks>
    [AvaloniaFact]
    public void La_copia_cae_en_el_primer_hueco_libre()
    {
        SpritesEditorViewModel editor = Bank();
        SpriteBank bank = editor.SpritesBank;

        Draw(bank.SpritesList[0], color: 7);
        Draw(bank.SpritesList[1], color: 4);
        Draw(bank.SpritesList[2], color: 5);

        editor.DuplicateSpriteCommand.Execute(null);

        Assert.True(SameDrawing(bank.SpritesList[0], bank.SpritesList[3]));

        // Y los que ya estaban puestos siguen donde estaban.
        Assert.Equal(4, bank.SpritesList[1].ArraySpriteRows[0].Color);
        Assert.Equal(5, bank.SpritesList[2].ArraySpriteRows[0].Color);
    }

    /// <summary>
    /// Duplicar no descoloca un grupo, apunte a donde apunte.
    /// </summary>
    /// <remarks>
    /// Los grupos señalan a sus patrones por índice. Se guarda el patrón concreto al que
    /// apunta y se comprueba que sigue resolviendo a ése: comparar el dibujo de una
    /// posición con el de la posición a la que apunta el grupo sería compararlo consigo
    /// mismo, y eso se cumple se mueva lo que se mueva.
    /// </remarks>
    [AvaloniaFact]
    public void Duplicar_no_descoloca_los_grupos()
    {
        SpritesEditorViewModel editor = Bank();
        SpriteBank bank = editor.SpritesBank;

        Draw(bank.SpritesList[0], color: 7);
        Draw(bank.SpritesList[2], color: 5);

        editor.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.Groups[0];

        group.AddMemberCommand.Execute(null);
        group.SelectedMember!.PatternIndex = 2;

        Sprite pointed = bank.SpritesList[group.SelectedMember.PatternIndex];

        editor.DuplicateSpriteCommand.Execute(null);

        Assert.Equal(2, group.SelectedMember!.PatternIndex);
        Assert.Same(pointed, bank.SpritesList[group.SelectedMember.PatternIndex]);
    }

    /// <summary>Después de duplicar se edita la copia, que es sobre la que se va a trabajar.</summary>
    [AvaloniaFact]
    public void Despues_de_duplicar_se_edita_la_copia()
    {
        SpritesEditorViewModel editor = Bank();

        Draw(editor.SpritesBank.SpritesList[0], color: 7);

        editor.DuplicateSpriteCommand.Execute(null);

        Assert.Equal(1, editor.CurrentSpriteIndex);
    }

    /// <summary>Y el banco queda marcado como sin guardar.</summary>
    [AvaloniaFact]
    public void Duplicar_deja_el_banco_sin_guardar()
    {
        SpritesEditorViewModel editor = Bank();

        editor.MarkClean();
        editor.DuplicateSpriteCommand.Execute(null);

        Assert.True(editor.IsModified);
    }

    /// <summary>Sin ningún hueco libre no se puede duplicar.</summary>
    [AvaloniaFact]
    public void Con_los_sesenta_y_cuatro_ocupados_no_se_duplica()
    {
        SpritesEditorViewModel editor = Bank();

        foreach (Sprite sprite in editor.SpritesBank.SpritesList)
            Draw(sprite, color: 7);

        Assert.False(editor.DuplicateSpriteCommand.CanExecute(null));
    }

    // ------------------------------------------------------------------ copiar y pegar

    /// <summary>
    /// Copiar y pegar lleva un patrón al hueco que se elija.
    /// </summary>
    /// <remarks>
    /// Duplicar sirve para «otro igual, donde quepa». Esto es para «éste, ahí», que es lo
    /// que hace falta cuando el número importa, porque es el número el que usa el juego.
    /// </remarks>
    [AvaloniaFact]
    public async Task Copiar_y_pegar_lleva_el_patron_al_hueco_elegido()
    {
        SpritesEditorViewModel editor = Bank(new IDialogAnswers(new TestDialogService()));
        SpriteBank bank = editor.SpritesBank;

        Draw(bank.SpritesList[0], color: 7);

        editor.CopySpriteCommand.Execute(null);

        GoToIndex(editor, 20);
        await editor.PasteSpriteCommand.ExecuteAsync(null);

        Assert.True(SameDrawing(bank.SpritesList[0], bank.SpritesList[20]));

        // Y no ha caído en ningún sitio más.
        Assert.True(bank.SpritesList[1].IsEmpty);
        Assert.True(bank.SpritesList[19].IsEmpty);
    }

    /// <summary>Lo copiado es una copia: retocar el original después no cambia lo que se pega.</summary>
    [AvaloniaFact]
    public async Task Lo_copiado_no_cambia_si_se_retoca_el_original()
    {
        SpritesEditorViewModel editor = Bank(new IDialogAnswers(new TestDialogService()));
        SpriteBank bank = editor.SpritesBank;

        Draw(bank.SpritesList[0], color: 7);

        editor.CopySpriteCommand.Execute(null);

        bank.SpritesList[0].ArraySpriteRows[0].Color = 2;

        GoToIndex(editor, 20);
        await editor.PasteSpriteCommand.ExecuteAsync(null);

        Assert.Equal(7, bank.SpritesList[20].ArraySpriteRows[0].Color);
        Assert.Equal(2, bank.SpritesList[0].ArraySpriteRows[0].Color);
    }

    /// <summary>Sin nada copiado no se puede pegar.</summary>
    [AvaloniaFact]
    public void Sin_nada_copiado_no_se_pega()
    {
        SpritesEditorViewModel editor = Bank();

        Assert.False(editor.PasteSpriteCommand.CanExecute(null));

        editor.CopySpriteCommand.Execute(null);

        Assert.True(editor.PasteSpriteCommand.CanExecute(null));
    }

    /// <summary>
    /// Pegar encima de un patrón con dibujo pregunta antes.
    /// </summary>
    /// <remarks>
    /// Se avisa en vez de no dejar: machacar un patrón a veces es justo lo que se quiere, y
    /// obligar a vaciarlo primero son dos pasos para el mismo resultado. Lo que hay que
    /// impedir es que pase sin querer, porque aquí no hay deshacer.
    /// </remarks>
    [AvaloniaFact]
    public async Task Pegar_encima_de_uno_con_dibujo_pregunta()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = false };
        SpritesEditorViewModel editor = Bank(new IDialogAnswers(dialogs));
        SpriteBank bank = editor.SpritesBank;

        Draw(bank.SpritesList[0], color: 7);
        Draw(bank.SpritesList[20], color: 3);

        editor.CopySpriteCommand.Execute(null);

        GoToIndex(editor, 20);
        await editor.PasteSpriteCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ConfirmCalls);
        Assert.Equal(3, bank.SpritesList[20].ArraySpriteRows[0].Color);
    }

    /// <summary>Y en un hueco vacío no pregunta nada: no hay nada que perder.</summary>
    [AvaloniaFact]
    public async Task Pegar_en_un_hueco_vacio_no_pregunta()
    {
        var dialogs = new TestDialogService();
        SpritesEditorViewModel editor = Bank(new IDialogAnswers(dialogs));

        Draw(editor.SpritesBank.SpritesList[0], color: 7);

        editor.CopySpriteCommand.Execute(null);

        GoToIndex(editor, 20);
        await editor.PasteSpriteCommand.ExecuteAsync(null);

        Assert.Equal(0, dialogs.ConfirmCalls);
        Assert.False(editor.SpritesBank.SpritesList[20].IsEmpty);
    }

    /// <summary>Deja el editor en ese hueco, contando desde 0 como cuenta el banco.</summary>
    private static void GoToIndex(SpritesEditorViewModel editor, int index)
    {
        while (editor.CurrentSpriteIndex < index)
            editor.NextSpriteCommand.Execute(null);
    }

    // ------------------------------------------------------------------ vaciar

    /// <summary>
    /// Vaciar deja el hueco donde estaba.
    /// </summary>
    /// <remarks>
    /// Es lo que sustituye a eliminar: el patrón se queda en blanco pero sigue ocupando su
    /// número, así que nada de lo de atrás se mueve.
    /// </remarks>
    [AvaloniaFact]
    public async Task Vaciar_no_mueve_los_numeros()
    {
        SpritesEditorViewModel editor = Bank(new IDialogAnswers(new TestDialogService()));
        SpriteBank bank = editor.SpritesBank;

        Draw(bank.SpritesList[0], color: 7);

        // El de en medio dibujado tambien: si se vacia uno que ya estaba en blanco, «queda
        // en blanco» se cumple sin que vaciar haga nada.
        Draw(bank.SpritesList[1], color: 3);
        Draw(bank.SpritesList[2], color: 5);

        Sprite third = bank.SpritesList[2];

        editor.NextSpriteCommand.Execute(null);

        await editor.ClearSpriteCommand.ExecuteAsync(null);

        Assert.Equal(SpriteBank.MaxSprites, bank.SpritesList.Count);
        Assert.Same(third, bank.SpritesList[2]);

        Assert.True(bank.SpritesList[1].IsEmpty);
        Assert.Equal(7, bank.SpritesList[0].ArraySpriteRows[0].Color);
        Assert.Equal(5, bank.SpritesList[2].ArraySpriteRows[0].Color);
    }

    /// <summary>Vaciar pregunta antes: en el editor de sprites no hay deshacer.</summary>
    [AvaloniaFact]
    public async Task Vaciar_pregunta_antes()
    {
        SpritesEditorViewModel editor =
            Bank(new IDialogAnswers(new TestDialogService { ConfirmAnswer = false }));

        Draw(editor.SpritesBank.SpritesList[0], color: 7);

        await editor.ClearSpriteCommand.ExecuteAsync(null);

        Assert.Equal(7, editor.SpritesBank.SpritesList[0].ArraySpriteRows[0].Color);
    }
}
