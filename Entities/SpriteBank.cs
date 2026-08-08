namespace MSX_SpritesEditor.Entities;

public class SpriteBank
{
    public enum SpriteType
    {
        MSX,
        MSX2,
    }

    /// <summary>Límite de sprites de un banco en el VDP.</summary>
    public const int MaxSprites = 64;

    private readonly List<Sprite> _sprites = [];
    private readonly SpriteType _spriteType;

    // En la versión WPF el constructor con SpriteType no inicializaba la lista,
    // así que cualquier uso distinto del constructor por defecto reventaba.
    public SpriteBank(SpriteType spriteType = SpriteType.MSX)
    {
        _spriteType = spriteType;
        NewSprite();
    }

    public IReadOnlyList<Sprite> SpritesList => _sprites;

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
