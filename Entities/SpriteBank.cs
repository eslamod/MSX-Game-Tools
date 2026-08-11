using System.Collections.ObjectModel;

namespace MSX_GameTools.Entities;

public class SpriteBank
{
    public enum SpriteType
    {
        MSX,
        MSX2,
    }

    /// <summary>Límite de sprites de un banco en el VDP.</summary>
    public const int MaxSprites = 64;

    /// <summary>
    /// Grupos que caben en el catálogo. No es un límite del hardware: los grupos son
    /// definiciones reutilizables, y cuántos planos hay en pantalla a la vez lo decide
    /// el juego, no el editor.
    /// </summary>
    public const int MaxGroups = 32;

    private readonly List<Sprite> _sprites = [];
    private readonly SpriteType _spriteType;

    // En la versión WPF el constructor con SpriteType no inicializaba la lista,
    // así que cualquier uso distinto del constructor por defecto reventaba.
    public SpriteBank(SpriteType spriteType = SpriteType.MSX, string name = "")
    {
        _spriteType = spriteType;
        Name = name;
        NewSprite();
    }

    /// <summary>
    /// Nombre del banco. Hasta ahora sólo vivía en la cabecera de la pestaña, pero el
    /// fichero lo necesita.
    /// </summary>
    public string Name { get; set; }

    public IReadOnlyList<Sprite> SpritesList => _sprites;

    /// <summary>Los personajes multicolor compuestos con los patrones de este banco.</summary>
    public ObservableCollection<SpriteGroup> Groups { get; } = [];

    public bool CanAddGroup => Groups.Count < MaxGroups;

    /// <summary>Crea un grupo con un único miembro. Devuelve <c>null</c> si ya no caben más.</summary>
    public SpriteGroup? NewGroup(int patternIndex)
    {
        if (!CanAddGroup || (uint)patternIndex >= (uint)_sprites.Count)
            return null;

        var group = new SpriteGroup(NextGroupName());
        group.Add(new SpriteGroupMember(patternIndex, _sprites[patternIndex]));

        Groups.Add(group);

        return group;
    }

    private string NextGroupName()
    {
        int number = 1;
        while (Groups.Any(g => g.Name == $"Group {number}"))
            number++;

        return $"Group {number}";
    }

    public SpriteType Type => _spriteType;

    /// <summary>Añade un sprite al banco. Devuelve <c>null</c> si el banco está lleno.</summary>
    public Sprite? NewSprite()
    {
        if (_sprites.Count >= MaxSprites)
            return null;

        Sprite sprite = _spriteType == SpriteType.MSX ? new SpriteMSX() : new SpriteMSX2();
        sprite.ImageMini = new ImageMini(ImageMini.ImagePreviewType.ImagePreview16x16);
        _sprites.Add(sprite);
        return sprite;
    }

    public void DeleteSprite(int pos)
    {
        if ((uint)pos < (uint)_sprites.Count)
            _sprites.RemoveAt(pos);
    }
}
