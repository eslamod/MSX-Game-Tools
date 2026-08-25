using MSX_GameTools.Entities;

namespace MSX_GameTools.Services;

/// <summary>
/// Saca juegos de tiles y patrones de sprite de un volcado de VRAM.
/// </summary>
/// <remarks>
/// <para>
/// El camino de vuelta del exportador: en lugar de escribir las tablas que espera el VDP, se
/// leen de una máquina que ya está corriendo. Con un volcado de openMSX se recupera lo que
/// dibujó otro, o lo propio cuando el fichero del editor se ha perdido.
/// </para>
/// <para>
/// <b>Screen 1 no tiene tercios.</b> Allí la tabla de patrones son 2048 bytes —los 256 tiles,
/// una vez— y la de colores 32: un par de colores por cada ocho tiles seguidos. Así que sale
/// un solo juego y el color se pone en el grupo, que ya se encarga de bajarlo a sus tiles.
/// </para>
/// <para>
/// <b>Los tres tercios.</b> En GRAPHIC 2 la tabla de patrones son 6144 bytes: tres bancos de
/// 256 tiles, uno por cada tercio de la pantalla. Muchos juegos repiten el mismo en los tres
/// —es lo que hacen las ROMs de prueba de aquí—, y entonces traerlos por separado daría tres
/// juegos idénticos. Así que se comparan: si los tres son iguales sale un juego, y si no,
/// tres. La comparación es de patrones y colores a la vez, que un tercio puede repetir el
/// dibujo y cambiarle el color.
/// </para>
/// </remarks>
public static class VramImporter
{
    /// <summary>Un tercio de pantalla: 256 tiles de 8 líneas.</summary>
    public const int ThirdBytes = TileSet.TileCount * Tile.Rows;

    /// <summary>Los tres, que es lo que ocupa la tabla entera en GRAPHIC 2.</summary>
    public const int TableBytes = ThirdBytes * 3;

    /// <summary>32 bytes por patrón de 16x16, y 2 KB de tabla.</summary>
    public const int SpriteTableBytes = 2048;

    /// <summary>Los 32 bytes de color de GRAPHIC 1: uno por cada ocho tiles.</summary>
    public const int ColorGroupBytes = TileSet.ColorGroupCount;

    /// <summary>Dónde está cada tabla dentro del volcado.</summary>
    /// <remarks>
    /// Los valores de partida son los de siempre en SCREEN 2, que son los que documentan las
    /// ROMs de prueba de <c>msx/test_rom</c>. En GRAPHIC 2 y 3 la base de patrones y la de
    /// colores no se eligen libremente: son <c>0000H</c> o <c>2000H</c>, porque los bits bajos
    /// de R#4 y R#3 son una máscara sobre los tercios y no parte de la dirección.
    /// </remarks>
    public sealed record VramLayout
    {
        public VdpRegisters.ScreenMode Mode { get; init; } = VdpRegisters.ScreenMode.Graphic2;

        /// <summary>Sprites de 16x16, que es lo único que sabe guardar un banco de aquí.</summary>
        public bool BigSprites { get; init; } = true;

        public int Patterns { get; init; }

        public int Colors { get; init; } = 0x2000;

        public int SpritePatterns { get; init; } = 0x3800;
    }

    /// <summary>Lo que dicen los registros del VDP, listo para leer el volcado.</summary>
    public static VramLayout LayoutOf(VdpRegisters.Layout registers) => new()
    {
        Mode = registers.Mode,
        BigSprites = registers.BigSprites,
        Patterns = registers.Patterns,
        Colors = registers.Colors,
        SpritePatterns = registers.SpritePatterns,
    };

    /// <summary>Lo que se ha podido sacar del volcado.</summary>
    public sealed record VramImport(
        IReadOnlyList<TileSet> TileSets,
        SpriteBank? Sprites,
        IReadOnlyList<string> Problems);

    /// <summary>
    /// Lee un volcado. Lo que no quepa en el fichero se deja fuera y se dice.
    /// </summary>
    /// <remarks>
    /// No se aborta por una tabla que se sale: un volcado de 16 KB no llega a los patrones de
    /// sprite en GRAPHIC 3, y aun así los tiles que sí están valen. Se trae lo que haya.
    /// </remarks>
    public static VramImport Read(byte[] vram, VramLayout layout)
    {
        var problems = new List<string>();

        IReadOnlyList<TileSet> tileSets = ReadTileSets(vram, layout, problems);
        SpriteBank? sprites = ReadSprites(vram, layout, problems);

        return new VramImport(tileSets, sprites, problems);
    }

    private static IReadOnlyList<TileSet> ReadTileSets(
        byte[] vram, VramLayout layout, List<string> problems) =>
        layout.Mode == VdpRegisters.ScreenMode.Graphic1
            ? ReadGraphic1(vram, layout, problems)
            : ReadGraphic2(vram, layout, problems);

    /// <summary>Un juego, y el color por grupos de ocho tiles.</summary>
    private static IReadOnlyList<TileSet> ReadGraphic1(
        byte[] vram, VramLayout layout, List<string> problems)
    {
        if (!Fits(vram, layout.Patterns, ThirdBytes) || !Fits(vram, layout.Colors, ColorGroupBytes))
        {
            problems.Add(Localization.Localizer.Instance.Format(
                "VramNoTiles", Hex(layout.Patterns), Hex(layout.Colors), vram.Length));

            return [];
        }

        var tileSet = new TileSet(
            Localization.Localizer.Instance["VramTileSetName"], TileSet.GraphicMode.Graphic1);

        ReadPatterns(vram, layout.Patterns, tileSet);

        for (int group = 0; group < TileSet.ColorGroupCount; group++)
        {
            byte color = vram[layout.Colors + group];

            // Al grupo y no a cada tile: el grupo se encarga de bajarselo a los suyos, que es
            // justo la limitacion del modo y lo que el editor ensena en vez de esconder.
            tileSet.ColorGroups[group].ForeColor = color >> 4;
            tileSet.ColorGroups[group].BackColor = color & 0x0F;
        }

        return [tileSet];
    }

    private static IReadOnlyList<TileSet> ReadGraphic2(
        byte[] vram, VramLayout layout, List<string> problems)
    {
        if (!Fits(vram, layout.Patterns, TableBytes) || !Fits(vram, layout.Colors, TableBytes))
        {
            problems.Add(Localization.Localizer.Instance.Format(
                "VramNoTiles", Hex(layout.Patterns), Hex(layout.Colors), vram.Length));

            return [];
        }

        // Se comparan los tres tercios enteros -dibujo y color- antes de montar nada: si son
        // el mismo, tres juegos identicos no le sirven a nadie.
        bool same = Same(vram, layout, 0, 1) && Same(vram, layout, 0, 2);

        return same
            ? [Third(vram, layout, 0, string.Empty)]
            : [.. Enumerable.Range(0, 3).Select(third => Third(vram, layout, third, $" {third + 1}"))];
    }

    private static bool Same(byte[] vram, VramLayout layout, int one, int other) =>
        Same(vram, layout.Patterns, one, other) && Same(vram, layout.Colors, one, other);

    private static bool Same(byte[] vram, int table, int one, int other)
    {
        for (int at = 0; at < ThirdBytes; at++)
        {
            if (vram[table + (one * ThirdBytes) + at] != vram[table + (other * ThirdBytes) + at])
                return false;
        }

        return true;
    }

    private static TileSet Third(byte[] vram, VramLayout layout, int third, string suffix)
    {
        var tileSet = new TileSet(
            Localization.Localizer.Instance["VramTileSetName"] + suffix,
            TileSet.GraphicMode.Graphic2);

        int patterns = layout.Patterns + (third * ThirdBytes);
        int colors = layout.Colors + (third * ThirdBytes);

        ReadPatterns(vram, patterns, tileSet);

        for (int index = 0; index < TileSet.TileCount; index++)
        {
            for (int row = 0; row < Tile.Rows; row++)
            {
                byte color = vram[colors + (index * Tile.Rows) + row];

                tileSet.ListOfTiles[index].ArrayTileRows[row].ForeColor = color >> 4;
                tileSet.ListOfTiles[index].ArrayTileRows[row].BackColor = color & 0x0F;
            }
        }

        return tileSet;
    }

    /// <summary>Los 2048 bytes de dibujo, que son los mismos en los dos modos.</summary>
    private static void ReadPatterns(byte[] vram, int at, TileSet tileSet)
    {
        for (int index = 0; index < TileSet.TileCount; index++)
        {
            for (int row = 0; row < Tile.Rows; row++)
            {
                Bits(
                    vram[at + (index * Tile.Rows) + row],
                    tileSet.ListOfTiles[index].ArrayTileRows[row].ArrayPattern);
            }
        }
    }

    private static SpriteBank? ReadSprites(byte[] vram, VramLayout layout, List<string> problems)
    {
        if (!layout.BigSprites)
        {
            // Un banco de aqui guarda sprites de 16x16 y nada mas, asi que los de 8x8 no
            // tienen donde ir. Mejor decirlo que traerlos partidos por la mitad.
            problems.Add(Localization.Localizer.Instance["VramSmallSprites"]);

            return null;
        }

        if (!Fits(vram, layout.SpritePatterns, SpriteTableBytes))
        {
            problems.Add(Localization.Localizer.Instance.Format(
                "VramNoSprites", Hex(layout.SpritePatterns), vram.Length));

            return null;
        }

        // MSX2 por lo que trae el volcado: los colores de sprite viven en otra tabla y aqui
        // solo se leen los dibujos, asi que el banco sale con los colores por defecto.
        var bank = new SpriteBank(
            SpriteBank.SpriteType.MSX2,
            Localization.Localizer.Instance["VramSpriteBankName"]);

        int patterns = SpriteTableBytes / SpriteBankExporter.PatternBytes;

        for (int index = 0; index < patterns && index < bank.SpritesList.Count; index++)
        {
            Sprite sprite = bank.SpritesList[index];
            int at = layout.SpritePatterns + (index * SpriteBankExporter.PatternBytes);

            // Los cuatro cuartos del manual del V9938: la mitad izquierda entera y luego la
            // derecha, que es como los escribe el exportador.
            for (int half = 0; half < 2; half++)
            {
                for (int row = 0; row < Sprite.Rows; row++)
                {
                    Bits(
                        vram[at + (half * Sprite.Rows) + row],
                        sprite.ArraySpriteRows[row].ArrayColumns,
                        half * TileRow.Columns);
                }
            }
        }

        return bank;
    }

    private static void Bits(byte value, bool[] into, int from = 0)
    {
        for (int bit = 0; bit < 8; bit++)
            into[from + bit] = ((value >> (7 - bit)) & 1) != 0;
    }

    private static bool Fits(byte[] vram, int at, int length) =>
        at >= 0 && length > 0 && at + length <= vram.Length;

    private static string Hex(int address) => $"{address:X4}H";
}
