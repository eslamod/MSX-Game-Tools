using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MSX_GameTools.Entities;
using MSX_GameTools.Services;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// Los ocho atributos por tile: definirlos, marcarlos, guardarlos y exportarlos.
/// </summary>
/// <remarks>
/// Un atributo <b>es</b> un bit y su posición en la lista es el bit. Por eso no se
/// reordenan, y por eso lo que se define es sólo el nombre: lo que va al fichero y a la
/// máquina son los bits, que no dependen de cómo se llamen.
/// </remarks>
public class TileAttributesTests
{
    private static TileSet Named(params string[] names)
    {
        var tileSet = new TileSet("Bosque");

        for (int bit = 0; bit < names.Length; bit++)
            tileSet.AttributeNames.Define(bit, names[bit]);

        return tileSet;
    }

    // ------------------------------------------------------------------ el modelo

    [Fact]
    public void Un_atributo_esta_definido_si_tiene_nombre()
    {
        TileSet tileSet = Named("Sólido", "", "Escalera");

        Assert.True(tileSet.AttributeNames.IsDefined(0));
        Assert.False(tileSet.AttributeNames.IsDefined(1));
        Assert.Equal([0, 2], tileSet.AttributeNames.Defined);
    }

    /// <summary>Un nombre de sólo espacios no es un nombre.</summary>
    /// <remarks>
    /// Si colara, saldría una casilla sin rótulo al editar el tile y no habría forma de
    /// saber qué se está marcando.
    /// </remarks>
    [Fact]
    public void Un_nombre_en_blanco_no_define_nada()
    {
        TileSet tileSet = Named("   ");

        Assert.False(tileSet.AttributeNames.IsDefined(0));
        Assert.False(tileSet.AttributeNames.Any);
    }

    /// <summary>
    /// Quitarle el nombre a un atributo no borra lo marcado en los tiles.
    /// </summary>
    /// <remarks>
    /// Es la decisión que evita el problema de reordenar la paleta. Borrar un rótulo no
    /// puede borrar el trabajo de haber marcado doscientos tiles: el bit se queda donde
    /// estaba y vuelve a verse en cuanto se le ponga nombre otra vez.
    /// </remarks>
    [Fact]
    public void Quitar_el_nombre_no_desmarca_los_tiles()
    {
        TileSet tileSet = Named("Sólido");

        tileSet.ListOfTiles[7].SetAttribute(0, true);

        tileSet.AttributeNames.Define(0, string.Empty);

        Assert.False(tileSet.AttributeNames.IsDefined(0));
        Assert.True(tileSet.ListOfTiles[7].Has(0));

        tileSet.AttributeNames.Define(0, "Pared");

        Assert.True(tileSet.ListOfTiles[7].Has(0));
    }

    [Fact]
    public void Cada_atributo_es_su_bit()
    {
        var tile = new Tile();

        tile.SetAttribute(0, true);
        tile.SetAttribute(3, true);

        Assert.Equal(0b0000_1001, tile.Attributes);

        tile.SetAttribute(0, false);

        Assert.Equal(0b0000_1000, tile.Attributes);
    }

    /// <summary>
    /// Los atributos viajan con el dibujo al copiar y estampar.
    /// </summary>
    /// <remarks>
    /// Si un tile es sólido, la copia que se estampa en otro hueco también lo es. Copiar el
    /// dibujo y dejarse las banderas daría dos tiles iguales que se comportan distinto, y
    /// eso sólo se descubre jugando.
    /// </remarks>
    [AvaloniaFact]
    public void Estampar_un_tile_se_lleva_sus_atributos()
    {
        TileSet tileSet = Named("Sólido");

        tileSet.ListOfTiles[0].SetAttribute(0, true);

        var editor = new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard());

        editor.SelectRegion(0, 0, 1, 1);
        editor.StampAt(5, 0);

        Assert.True(tileSet.ListOfTiles[5].Has(0));
    }

    // ------------------------------------------------------------------ el fichero

    [Fact]
    public void Los_nombres_y_las_banderas_van_y_vuelven()
    {
        TileSet tileSet = Named("Sólido", "Agua");

        tileSet.ListOfTiles[3].SetAttribute(1, true);

        LoadedTileSet read = TileSetSerializer.Deserialize(
            TileSetSerializer.Serialize(tileSet, ColorPalette.CreateMsxStandard()));

        Assert.Equal("Sólido", read.TileSet.AttributeNames[0]);
        Assert.Equal("Agua", read.TileSet.AttributeNames[1]);
        Assert.True(read.TileSet.ListOfTiles[3].Has(1));
        Assert.False(read.TileSet.ListOfTiles[3].Has(0));
    }

    /// <summary>
    /// Un tile en blanco pero marcado se guarda.
    /// </summary>
    /// <remarks>
    /// Los tiles sin tocar no se escriben, para no meter 256 entradas iguales en cada
    /// fichero. «Sin tocar» miraba sólo el dibujo, así que una pared invisible —un tile en
    /// blanco marcado como sólido, que es una cosa que se usa— se daba por vacía y se
    /// perdía al guardar sin decir nada.
    /// </remarks>
    [Fact]
    public void Un_tile_en_blanco_pero_marcado_no_se_pierde()
    {
        TileSet tileSet = Named("Sólido");

        tileSet.ListOfTiles[9].SetAttribute(0, true);

        LoadedTileSet read = TileSetSerializer.Deserialize(
            TileSetSerializer.Serialize(tileSet, ColorPalette.CreateMsxStandard()));

        Assert.True(read.TileSet.ListOfTiles[9].Has(0));
    }

    /// <summary>Un fichero de antes de los atributos sigue abriéndose.</summary>
    [Fact]
    public void Un_fichero_de_la_version_anterior_sigue_valiendo()
    {
        TileSet tileSet = Named("Sólido");

        tileSet.ListOfTiles[3].SetAttribute(0, true);

        string json = TileSetSerializer.Serialize(tileSet, ColorPalette.CreateMsxStandard())
            .Replace("\"version\": 5", "\"version\": 4");

        LoadedTileSet read = TileSetSerializer.Deserialize(json);

        // Se lee lo que traiga: la versión sólo dice qué esperar, no obliga a ignorarlo.
        Assert.Equal("Bosque", read.TileSet.Name);
    }

    // ------------------------------------------------------------------ la exportación

    [Fact]
    public void La_tabla_binaria_es_un_byte_por_tile()
    {
        TileSet tileSet = Named("Sólido", "Agua");

        tileSet.ListOfTiles[0].SetAttribute(0, true);
        tileSet.ListOfTiles[1].SetAttribute(1, true);

        byte[] table = TileSetExporter.AttributesToBinary(tileSet);

        Assert.Equal(TileSet.TileCount, table.Length);
        Assert.Equal(0b0000_0001, table[0]);
        Assert.Equal(0b0000_0010, table[1]);
        Assert.Equal(0, table[2]);
    }

    /// <summary>
    /// El asm trae las máscaras como constantes, no sólo los nombres en comentarios.
    /// </summary>
    /// <remarks>
    /// Es lo que se usa de verdad: un comentario hay que traducirlo a mano a un
    /// <c>bit 2, a</c> cada vez que se escribe código, y ahí es donde se cuela el error de
    /// un bit.
    /// </remarks>
    [Fact]
    public void El_asm_trae_las_mascaras_como_constantes()
    {
        TileSet tileSet = Named("Sólido", "", "Escalera");

        string asm = TileSetExporter.AttributesToAssembler(tileSet);

        Assert.Contains("equ %00000001", asm);
        Assert.Contains("equ %00000100", asm);

        // El nombre puesto, para saber cuál es cuál.
        Assert.Contains("Escalera", asm);

        // Y el que no tiene nombre no sale: no hay nada que llamarle.
        Assert.DoesNotContain("equ %00000010", asm);
    }

    // ------------------------------------------------------------------ la interfaz

    /// <summary>Sin definir ninguno, el editor no enseña nada de esto.</summary>
    [AvaloniaFact]
    public void Sin_atributos_definidos_no_se_ensena_nada()
    {
        var editor = new TileSetEditorViewModel(new TileSet("Bosque"), ColorPalette.CreateMsxStandard());

        Assert.False(editor.HasAttributes);
        Assert.Empty(editor.TileAttributes);
    }

    /// <summary>Definirlos en propiedades los hace aparecer al editar los tiles.</summary>
    [AvaloniaFact]
    public void Definirlos_en_propiedades_los_ensena_en_el_editor()
    {
        var main = new MainWindowViewModel();
        TileSetEditorViewModel editor = main.OpenTileSet(new TileSet("Bosque"));

        var properties = new EditPropertiesViewModel(main, editor);

        properties.Attributes[0].Name = "Sólido";
        properties.Attributes[2].Name = "Escalera";
        properties.AcceptPropertiesCommand.Execute(null);

        Assert.True(editor.HasAttributes);
        Assert.Equal(["0 · Sólido", "2 · Escalera"], editor.TileAttributes.Select(flag => flag.Label));
    }

    /// <summary>Marcar una casilla marca el tile de delante, y sólo ése.</summary>
    [AvaloniaFact]
    public void Marcar_la_casilla_marca_el_tile_de_delante()
    {
        TileSet tileSet = Named("Sólido");
        var editor = new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard());

        editor.GoTo(4);
        editor.TileAttributes[0].IsOn = true;

        Assert.True(tileSet.ListOfTiles[4].Has(0));
        Assert.False(tileSet.ListOfTiles[0].Has(0));
    }

    /// <summary>Y las casillas enseñan lo del tile al que se llega.</summary>
    [AvaloniaFact]
    public void Al_cambiar_de_tile_las_casillas_ensenan_las_suyas()
    {
        TileSet tileSet = Named("Sólido");

        tileSet.ListOfTiles[4].SetAttribute(0, true);

        var editor = new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard());

        editor.GoTo(4);

        Assert.True(editor.TileAttributes[0].IsOn);

        editor.GoTo(5);

        Assert.False(editor.TileAttributes[0].IsOn);
    }

    /// <summary>
    /// La lista de atributos no se come el lienzo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Con los ocho definidos, la lista se llevaba una fila entera y el lienzo se quedaba
    /// desplazándose a zoom alto. La prioridad es al revés: del lienzo se edita píxel a
    /// píxel y verlo entero es lo que importa; de la lista se marca una casilla de vez en
    /// cuando, así que es ella la que se desplaza.
    /// </para>
    /// <para>
    /// Se mide el alto de verdad y no el tope escrito: el tope es un <c>MaxHeight</c> y lo
    /// que decide es cuánto pide el contenido, que depende de a cuántas columnas se
    /// reparta y de lo que abulte una casilla.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void La_lista_de_atributos_no_se_come_el_lienzo()
    {
        TileSet tileSet = Named("Colisión", "Carretera", "Borde", "Hierba",
                                "Agua", "Daño", "Rompible", "Meta");

        var view = new TileSetEditorView
        {
            DataContext = new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard()),
        };

        var window = new Window { Content = view, Width = 1100, Height = 800 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var grid = (Grid)view.GetVisualDescendants().First(v => v is Grid { Name: "EditorGrid" });

        double taken = grid.RowDefinitions[2].ActualHeight;

        Assert.True(
            taken <= MostTheAttributesMayTake,
            $"Los ocho atributos le quitan {taken:0.0} al lienzo y el tope está en "
            + $"{MostTheAttributesMayTake}.");

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Lo más que se le consiente quitarle al lienzo, con los ocho definidos.</summary>
    /// <remarks>
    /// A dos columnas son cuatro filas de casillas más el rótulo y su aire. A una columna
    /// eran ocho filas y se pasaba de aquí, que es de donde sale el número.
    /// </remarks>
    private const double MostTheAttributesMayTake = 160;

    /// <summary>
    /// Pasear por los tiles no deja el juego marcado como sin guardar.
    /// </summary>
    /// <remarks>
    /// Poner las casillas al cambiar de tile pasa por la misma propiedad que marcarlas a
    /// mano. Sin distinguir las dos cosas, recorrer los 256 tiles con las flechas dejaba el
    /// asterisco de «sin guardar» puesto sin haber tocado nada, y luego el aviso al salir.
    /// </remarks>
    [AvaloniaFact]
    public void Pasear_por_los_tiles_no_ensucia_el_juego()
    {
        TileSet tileSet = Named("Sólido");

        tileSet.ListOfTiles[4].SetAttribute(0, true);

        var editor = new TileSetEditorViewModel(tileSet, ColorPalette.CreateMsxStandard());

        editor.MarkClean();

        for (int index = 0; index < 10; index++)
            editor.GoTo(index);

        Assert.False(editor.IsModified);
    }
}
