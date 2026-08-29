using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Pasar un banco de sprites de una máquina a la otra.
/// </summary>
/// <remarks>
/// Un banco puede acabar con la que no es: un volcado de VRAM trae los dibujos pero no sus
/// colores, así que la máquina sale del modo de pantalla y ese no siempre está bien puesto en
/// el volcado. Cambiarla a mano evita tener que volver a importar y rehacer los grupos.
/// </remarks>
public class SpriteBankMachineTests
{
    /// <summary>
    /// Pasando a MSX1, cada dibujo se queda con el color de su primera línea.
    /// </summary>
    /// <remarks>
    /// Patrones y miembros de grupo por separado: un miembro se siembra del patrón al crearse
    /// y a partir de ahí va por su cuenta, que es lo que permite el mismo dibujo dos veces con
    /// colores distintos.
    /// </remarks>
    [AvaloniaFact]
    public void Pasando_a_msx1_cada_dibujo_se_queda_de_un_color()
    {
        SpriteBank bank = Motley();

        bank.ConvertTo(SpriteBank.SpriteType.MSX);

        Assert.Equal(SpriteBank.SpriteType.MSX, bank.Type);

        foreach (SpriteRow row in bank.SpritesList[0].ArraySpriteRows)
            Assert.Equal(3, row.Color);

        foreach (SpriteAttributeRow row in bank.Groups[0].Members[0].Rows)
            Assert.Equal(7, row.Color);
    }

    /// <summary>
    /// Y pasando a MSX2 no se toca ningún color.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es el camino ancho: allí el color por línea existe, así que no hay nada que decidir y
    /// aplanar sería tirar color a cambio de nada.
    /// </para>
    /// <para>
    /// El banco de partida trae colores por línea a propósito, que si no la prueba no prueba:
    /// con todas las líneas iguales, aplanar y no aplanar dan lo mismo. Un banco MSX1 puede
    /// traerlos de un fichero que venga tocado, y el sitio de perderlos es al entrar en MSX1,
    /// no al salir.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Pasando_a_msx2_no_se_pierde_nada()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX, "Bicho");

        foreach (SpriteRow row in bank.SpritesList[0].ArraySpriteRows)
            row.Color = 9;

        bank.SpritesList[0].ArraySpriteRows[4].Color = 12;

        bank.ConvertTo(SpriteBank.SpriteType.MSX2);

        Assert.Equal(SpriteBank.SpriteType.MSX2, bank.Type);
        Assert.Equal(12, bank.SpritesList[0].ArraySpriteRows[4].Color);
        Assert.Equal(9, bank.SpritesList[0].ArraySpriteRows[0].Color);
    }

    /// <summary>Se dice cuántos dibujos pierden color antes de tocar nada.</summary>
    [AvaloniaFact]
    public async Task Se_avisa_de_cuantos_dibujos_pierden_color()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = false };
        var main = new MainWindowViewModel(dialogs);

        main.OpenSpriteBank(Motley());

        await main.ConvertSpriteBankToMsx1Command.ExecuteAsync(null);

        // Dos: el patrón de varios colores y el miembro de varios colores. Comparando el
        // mensaje entero y no buscando el «2», que un «2» lo tiene también «MSX2».
        Assert.Equal(
            Localizer.Instance.Format("ToMsx1Body", "Bicho", 2),
            dialogs.LastConfirmMessage);
    }

    /// <summary>
    /// Y diciendo que no, el banco se queda como estaba.
    /// </summary>
    /// <remarks>
    /// Es lo que hace útil al aviso: si convirtiera igual, decir que no sería decorativo y el
    /// color perdido no se recupera.
    /// </remarks>
    [AvaloniaFact]
    public async Task Diciendo_que_no_el_banco_se_queda_como_estaba()
    {
        var dialogs = new TestDialogService { ConfirmAnswer = false };
        var main = new MainWindowViewModel(dialogs);

        SpriteBank bank = Motley();

        main.OpenSpriteBank(bank);

        await main.ConvertSpriteBankToMsx1Command.ExecuteAsync(null);

        Assert.Equal(SpriteBank.SpriteType.MSX2, bank.Type);
        Assert.Equal(11, bank.SpritesList[0].ArraySpriteRows[5].Color);
    }

    /// <summary>
    /// Al cambiar de máquina, la ventana cambia lo que enseña.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El color de un sprite se edita distinto en cada máquina —uno por línea en MSX2, uno por
    /// sprite en MSX1—, y son controles distintos. Las propiedades que los enseñan y los
    /// esconden se calculan del banco y no avisan por su cuenta, así que sin decírselo a mano
    /// el banco se convierte y el panel se queda enseñando lo de la máquina de antes.
    /// </para>
    /// <para>
    /// Por eso se mira el control de verdad en la ventana montada y no la propiedad: la
    /// propiedad ya vale lo que tiene que valer sin que nadie haya avisado, y una prueba que
    /// sólo la mire pasa igual con el aviso quitado.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Al_cambiar_de_maquina_la_ventana_cambia_lo_que_ensena()
    {
        var main = new MainWindowViewModel(new TestDialogService());

        SpritesEditorViewModel editor = main.OpenSpriteBank(Motley());

        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        StackPanel color = window.GetVisualDescendants()
            .OfType<StackPanel>()
            .Single(one => one.Name == "SpriteColorPanel");

        Assert.False(color.IsVisible);      // en MSX2 el color va por linea

        var told = new List<string>();

        editor.Groups[0].PropertyChanged += (_, args) => told.Add(args.PropertyName ?? string.Empty);

        editor.ConvertTo(SpriteBank.SpriteType.MSX);
        Dispatcher.UIThread.RunJobs();

        Assert.True(color.IsVisible);
        Assert.Contains(nameof(SpriteGroupViewModel.IsMsx1), told);

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Un volcado de una pantalla de sprites de un color da un banco MSX1.
    /// </summary>
    /// <remarks>
    /// El modo 2 de sprites —el del color por línea— sólo existe de GRAPHIC 3 en adelante, así
    /// que un volcado de SCREEN 1 o 2 es de sprites de un color. Antes salían todos como MSX2
    /// y había que convertirlos a mano.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(VdpRegisters.ScreenMode.Graphic1, SpriteBank.SpriteType.MSX)]
    [InlineData(VdpRegisters.ScreenMode.Graphic2, SpriteBank.SpriteType.MSX)]
    [InlineData(VdpRegisters.ScreenMode.Graphic3, SpriteBank.SpriteType.MSX2)]
    public void La_maquina_del_volcado_sale_del_modo_de_pantalla(
        VdpRegisters.ScreenMode mode, SpriteBank.SpriteType expected)
    {
        var vram = new byte[16384];

        VramImporter.VramImport import = VramImporter.Read(
            vram, new VramImporter.VramLayout { Mode = mode });

        Assert.NotNull(import.Sprites);
        Assert.Equal(expected, import.Sprites.Type);
    }

    // ------------------------------------------------------------------ los andamios

    /// <summary>Un banco MSX2 con un patrón y un miembro de varios colores.</summary>
    private static SpriteBank Motley()
    {
        var bank = new SpriteBank(SpriteBank.SpriteType.MSX2, "Bicho");

        foreach (SpriteRow row in bank.SpritesList[0].ArraySpriteRows)
            row.Color = 3;

        bank.SpritesList[0].ArraySpriteRows[5].Color = 11;

        SpriteGroup group = bank.NewGroup(0);
        SpriteGroupMember member = group.Members[0];

        foreach (SpriteAttributeRow row in member.Rows)
            row.Color = 7;

        member.Rows[2].Color = 4;

        return bank;
    }
}
