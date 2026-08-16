using Avalonia.Headless.XUnit;
using MSX_GameTools.Entities;
using MSX_GameTools.ViewModels;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Duplicar un patrón dentro de su banco.
/// </summary>
/// <remarks>
/// Hacer una variación de un sprite —el mismo bicho mirando al otro lado— obligaba a
/// redibujarlo entero: no había copiar ni pegar en ninguna parte del editor de sprites.
/// </remarks>
public class DuplicateSpriteTests
{
    private static SpritesEditorViewModel Bank(SpriteBank.SpriteType type = SpriteBank.SpriteType.MSX2)
    {
        var bank = new SpriteBank(type, "Bichos");

        return new SpritesEditorViewModel(bank, ColorPalette.CreateMsxStandard());
    }

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

    [AvaloniaFact]
    public void La_copia_lleva_el_dibujo_y_los_colores()
    {
        SpritesEditorViewModel editor = Bank();
        SpriteBank bank = editor.SpritesBank;

        Draw(bank.SpritesList[0], color: 7);

        editor.DuplicateSpriteCommand.Execute(null);

        Assert.Equal(2, bank.SpritesList.Count);
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
    /// La copia va al final, no detrás del original.
    /// </summary>
    /// <remarks>
    /// Los grupos apuntan a sus patrones por índice. Metiendo la copia en medio, todos los
    /// de atrás correrían un puesto y los grupos seguirían apuntando al número de antes:
    /// un grupo compuesto se rompería sin tocarlo.
    /// </remarks>
    [AvaloniaFact]
    public void La_copia_va_al_final_y_no_descoloca_los_grupos()
    {
        SpritesEditorViewModel editor = Bank();
        SpriteBank bank = editor.SpritesBank;

        editor.AddSpriteCommand.Execute(null);
        editor.AddSpriteCommand.Execute(null);

        Draw(bank.SpritesList[2], color: 5);

        // Un grupo que señala al tercero.
        editor.AddGroupCommand.Execute(null);

        SpriteGroupViewModel group = editor.Groups[0];

        group.AddMemberCommand.Execute(null);
        group.SelectedMember!.PatternIndex = 2;

        // El patrón concreto al que apunta el grupo, antes de duplicar nada.
        Sprite pointed = bank.SpritesList[group.SelectedMember!.PatternIndex];

        // Se duplica el primero, que es el que tiene delante todo lo demas.
        editor.PreviousSpriteCommand.Execute(null);
        editor.DuplicateSpriteCommand.Execute(null);

        Assert.Equal(4, bank.SpritesList.Count);

        // El grupo sigue resolviendo al mismo patrón, no a otro que se haya corrido un
        // puesto. Se compara el objeto y no el dibujo: comparar el dibujo de la posición 2
        // con el de la posición a la que apunta el grupo es compararlo consigo mismo, y eso
        // se cumple se corra lo que se corra.
        Assert.Same(pointed, bank.SpritesList[group.SelectedMember.PatternIndex]);
    }

    /// <summary>Después de duplicar se edita la copia, que es sobre la que se va a trabajar.</summary>
    [AvaloniaFact]
    public void Despues_de_duplicar_se_edita_la_copia()
    {
        SpritesEditorViewModel editor = Bank();

        editor.DuplicateSpriteCommand.Execute(null);

        Assert.Equal(2, editor.CurrentSpritePosition);
        Assert.Equal(2, editor.ImagesMiniList.Count);
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

    // ------------------------------------------------------- vaciar y eliminar el ultimo

    /// <summary>
    /// Sólo se puede eliminar el último patrón.
    /// </summary>
    /// <remarks>
    /// Quitar uno de en medio corre un puesto a todos los de atrás. Eso descoloca los
    /// grupos, que apuntan por índice, pero sobre todo descoloca el juego: si el código de
    /// la máquina dibuja el sprite 12, después de borrar el 3 el 12 es otro dibujo. Y eso
    /// el editor no lo puede arreglar por nadie.
    /// </remarks>
    [AvaloniaFact]
    public void Solo_se_elimina_el_ultimo()
    {
        SpritesEditorViewModel editor = Bank();

        editor.AddSpriteCommand.Execute(null);
        editor.AddSpriteCommand.Execute(null);

        // Recien añadido, se esta en el ultimo.
        Assert.True(editor.DeleteSpriteCommand.CanExecute(null));

        editor.PreviousSpriteCommand.Execute(null);

        Assert.False(editor.DeleteSpriteCommand.CanExecute(null));
    }

    /// <summary>Y nunca el único que queda, que dejaría el lienzo sin nada que dibujar.</summary>
    [AvaloniaFact]
    public void El_unico_que_queda_no_se_elimina()
    {
        SpritesEditorViewModel editor = Bank();

        Assert.Equal(1, editor.SpritesBank.SpritesList.Count);
        Assert.False(editor.DeleteSpriteCommand.CanExecute(null));
    }

    /// <summary>
    /// Vaciar deja el hueco donde estaba.
    /// </summary>
    /// <remarks>
    /// Es lo que sustituye a eliminar en medio: el patrón se queda en blanco pero sigue
    /// ocupando su número, así que nada de lo de atrás se mueve.
    /// </remarks>
    [AvaloniaFact]
    public async Task Vaciar_no_mueve_los_numeros()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        var editor = new SpritesEditorViewModel(
            bank, ColorPalette.CreateMsxStandard(), new TestDialogService());

        editor.AddSpriteCommand.Execute(null);
        editor.AddSpriteCommand.Execute(null);

        Draw(bank.SpritesList[0], color: 7);

        // El de en medio dibujado tambien: si se vacia uno que ya estaba en blanco, «queda
        // en blanco» se cumple sin que vaciar haga nada.
        Draw(bank.SpritesList[1], color: 3);
        Draw(bank.SpritesList[2], color: 5);

        Sprite last = bank.SpritesList[2];

        // Se vacia el de en medio.
        editor.PreviousSpriteCommand.Execute(null);
        await editor.ClearSpriteCommand.ExecuteAsync(null);

        Assert.Equal(3, bank.SpritesList.Count);
        Assert.Same(last, bank.SpritesList[2]);

        // El vaciado queda en blanco y los demas intactos.
        Assert.True(SameDrawing(bank.SpritesList[1], new SpriteMSX2()));
        Assert.Equal(7, bank.SpritesList[0].ArraySpriteRows[0].Color);
    }

    /// <summary>Vaciar pregunta antes: en el editor de sprites no hay deshacer.</summary>
    [AvaloniaFact]
    public async Task Vaciar_pregunta_antes()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bichos");
        var editor = new SpritesEditorViewModel(
            bank, ColorPalette.CreateMsxStandard(), new TestDialogService { ConfirmAnswer = false });

        Draw(bank.SpritesList[0], color: 7);

        await editor.ClearSpriteCommand.ExecuteAsync(null);

        Assert.Equal(7, bank.SpritesList[0].ArraySpriteRows[0].Color);
    }

    /// <summary>Con el banco lleno no se puede duplicar, igual que no se puede añadir.</summary>
    [AvaloniaFact]
    public void Con_el_banco_lleno_no_se_duplica()
    {
        SpritesEditorViewModel editor = Bank();

        while (editor.AddSpriteCommand.CanExecute(null))
            editor.AddSpriteCommand.Execute(null);

        Assert.Equal(SpriteBank.MaxSprites, editor.SpritesBank.SpritesList.Count);
        Assert.False(editor.DuplicateSpriteCommand.CanExecute(null));
    }
}
