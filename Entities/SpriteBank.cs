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

    /// <summary>
    /// Cambia de índice los colores de todo el banco, para que siga viéndose igual
    /// después de haber movido los colores de sitio en la paleta.
    /// </summary>
    /// <param name="table">Del índice de antes al de ahora, tal cual lo da <see cref="PaletteSwaps.Table"/>.</param>
    /// <remarks>
    /// Los miembros de los grupos llevan <b>su propia</b> tabla de colores, que es lo que
    /// se edita cuando se monta un personaje multicolor. No basta con recorrer los
    /// patrones: hay que pasar también por las filas de cada miembro.
    /// </remarks>
    public void RemapColors(IReadOnlyList<int> table)
    {
        foreach (Sprite sprite in _sprites)
        {
            foreach (SpriteRow row in sprite.ArraySpriteRows)
                row.Color = table[row.Color];
        }

        foreach (SpriteGroup group in Groups)
        {
            foreach (SpriteGroupMember member in group.Members)
            {
                foreach (SpriteAttributeRow row in member.Rows)
                    row.Color = table[row.Color];
            }
        }
    }

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

    /// <summary>
    /// Añade una copia de ese patron al final del banco.
    /// </summary>
    /// <remarks>
    /// Al final y no justo detras del original, que seria lo natural de leer: los grupos
    /// apuntan a sus patrones <b>por indice</b>, y meter uno en medio correria todos los de
    /// atras sin que los grupos se enteren. Al final no se mueve ninguno.
    /// </remarks>
    /// <returns>La copia, o <c>null</c> si el banco esta lleno o el patron no existe.</returns>
    public Sprite? DuplicateSprite(int pos)
    {
        if ((uint)pos >= (uint)_sprites.Count)
            return null;

        Sprite? copy = NewSprite();

        copy?.CopyFrom(_sprites[pos]);

        return copy;
    }

    public void DeleteSprite(int pos)
    {
        if ((uint)pos < (uint)_sprites.Count)
            _sprites.RemoveAt(pos);
    }
}
