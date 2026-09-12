using CommunityToolkit.Mvvm.ComponentModel;
using MSX_GameTools.Entities;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// The tile set of each band of the screen, for the forms that let you pick them.
/// </summary>
/// <remarks>
/// <para>
/// In GRAPHIC 2 each third of the screen reads its own bank of patterns, so a map that fits in
/// one screen can be drawn with a different tile set on each band. Two forms ask that same
/// question -the one that creates a map and the one that changes one that already exists- so
/// the question is written down once, here.
/// </para>
/// <para>
/// The first band is the tile set of the map, which is the one the rest of the program binds
/// to. The other two are only offered when there is room for them.
/// </para>
/// </remarks>
public partial class MapBandsViewModel : ObservableObject
{
    /// <param name="choices">The open tile sets, which are the ones there are to pick from.</param>
    /// <param name="rows">
    /// How tall the map is, or is going to be: it is what decides how many bands there are.
    /// </param>
    public MapBandsViewModel(IReadOnlyList<TileSetEditorViewModel> choices, int rows)
    {
        Choices = choices;
        _rows = rows;
    }

    public IReadOnlyList<TileSetEditorViewModel> Choices { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsMiddle))]
    [NotifyPropertyChangedFor(nameof(ShowsBottom))]
    private int _rows;

    /// <summary>The tile set of the map, which is the one of the top band.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsMiddle))]
    [NotifyPropertyChangedFor(nameof(ShowsBottom))]
    private TileSetEditorViewModel? _tileSet;

    /// <summary>The one of the middle band, when there is a middle band.</summary>
    [ObservableProperty]
    private TileSetEditorViewModel? _middle;

    /// <summary>And of the bottom one.</summary>
    [ObservableProperty]
    private TileSetEditorViewModel? _bottom;

    /// <summary>Whether the middle band gets a tile set of its own.</summary>
    public bool ShowsMiddle => Slots >= 2;

    /// <summary>And the bottom one.</summary>
    public bool ShowsBottom => Slots >= 3;

    /// <summary>
    /// How many tile sets this map can take, from one to three.
    /// </summary>
    /// <remarks>
    /// It comes out of the rows and of the tile set: a map taller than the screen does not
    /// split, and neither does one of GRAPHIC 1 or one of super tiles.
    /// </remarks>
    private int Slots => TileSet is { } tiles ? TileMap.SlotsOf(Rows, tiles.TileSet) : 1;

    /// <summary>
    /// The other bands follow the tile set of the map while nobody touches them.
    /// </summary>
    /// <remarks>
    /// Three bands of the same set is the ordinary map, so that is what is offered to begin
    /// with; and changing the map's set has to take the others along, or they would be left
    /// pointing at the one that is no longer there. It also means that what is read is what
    /// comes out, with no empty box meaning "the same as the one above".
    /// </remarks>
    partial void OnTileSetChanged(TileSetEditorViewModel? value)
    {
        Middle = value;
        Bottom = value;
    }

    /// <summary>The tile set of each band, the one of the map first.</summary>
    public IEnumerable<TileSetEditorViewModel> Chosen()
    {
        if (TileSet is not { } tiles)
            yield break;

        yield return tiles;

        if (ShowsMiddle)
            yield return Middle ?? tiles;

        if (ShowsBottom)
            yield return Bottom ?? tiles;
    }

    /// <summary>What the map has to be told: one reference per band and in order.</summary>
    public IReadOnlyList<TileSetRef> Refs() =>
        TileMap.BandsOf([.. Chosen().Select(band => new TileSetRef(band.TileSet.Id, band.TileSet.Name))]);

    /// <summary>
    /// The first band that does not share the palette, if there is one.
    /// </summary>
    /// <remarks>
    /// There is one palette on the screen, so three tile sets with three palettes is something
    /// the machine cannot paint -it would take changing it mid screen with a line interrupt,
    /// which is not what this is for-. The same palette and not the same colours: two that
    /// merely look alike today come apart the day one of them is edited.
    /// </remarks>
    public TileSetEditorViewModel? Clash() =>
        TileSet is { } tiles
            ? Chosen().FirstOrDefault(band => !ReferenceEquals(band.ColorPalette, tiles.ColorPalette))
            : null;
}
