using System.Collections.ObjectModel;

namespace MSX_GameTools.Entities;

public class SpriteBank
{
    public enum SpriteType
    {
        MSX,
        MSX2,
    }

    /// <summary>
    /// Patrones de un banco. Son siempre estos, ni uno más ni uno menos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La tabla de patrones de sprites del VDP es una región de tamaño fijo —64 de 32
    /// bytes— y el patrón N vive en un sitio fijo dentro de ella, igual que los tiles. Por
    /// eso el banco no es una lista que crece y encoge: es la tabla.
    /// </para>
    /// <para>
    /// Antes se añadían y se quitaban, y quitar uno de en medio corría un puesto a todos
    /// los de atrás. Eso descolocaba los grupos, que apuntan por índice, y sobre todo el
    /// juego: si el código de la máquina dibuja el sprite 12, tras borrar el 3 el 12 es
    /// otro dibujo, y eso el editor no lo puede arreglar por nadie. Con los 64 siempre
    /// puestos no hay ningún número que se pueda mover.
    /// </para>
    /// </remarks>
    public const int MaxSprites = 64;

    /// <summary>
    /// Tamaños que puede tener un banco.
    /// </summary>
    /// <remarks>
    /// Los 64 son de la tabla de patrones de la VRAM, no del banco. Un juego que no mete
    /// todos los patrones en VRAM -los tiene en ROM y va redefiniendo los que necesita cada
    /// animación- puede tener muchos más: 256 patrones son 8 KB, que en una MegaROM no es
    /// nada. Por eso se puede pasar de 64, avisando de lo que significa.
    /// </remarks>
    public static IReadOnlyList<int> Capacities { get; } = [64, 128, 256];

    /// <summary>
    /// Cuántos patrones tiene este banco.
    /// </summary>
    /// <remarks>
    /// Los que pasan de 64 no caben en la tabla de patrones de la VRAM, así que sólo sirven
    /// para bancos que se quedan en ROM y se van volcando por partes. El editor no lo
    /// impide: lo avisa al crearlo y lo deja en manos de quien hace el juego.
    /// </remarks>
    public int Capacity { get; }

    /// <summary>
    /// Grupos que caben en el catálogo. No es un límite del hardware: los grupos son
    /// definiciones reutilizables, y cuántos planos hay en pantalla a la vez lo decide
    /// el juego, no el editor.
    /// </summary>
    public const int MaxGroups = 32;

    private readonly List<Sprite> _sprites = [];

    /// <summary>El siguiente número libre de grupo. No se reaprovechan los de los borrados.</summary>
    private int _nextGroupId;
    private SpriteType _spriteType;

    // En la versión WPF el constructor con SpriteType no inicializaba la lista,
    // así que cualquier uso distinto del constructor por defecto reventaba.
    public SpriteBank(SpriteType spriteType = SpriteType.MSX, string name = "", int capacity = MaxSprites)
    {
        _spriteType = spriteType;
        Name = name;
        Capacity = Capacities.Contains(capacity) ? capacity : MaxSprites;

        for (int index = 0; index < Capacity; index++)
        {
            var sprite = new Sprite();

            sprite.ImageMini = new ImageMini(ImageMini.ImagePreviewType.ImagePreview16x16);

            _sprites.Add(sprite);
        }
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

    /// <summary>
    /// Las animaciones hechas con los patrones o los grupos de este banco.
    /// </summary>
    /// <remarks>
    /// Cuelgan del banco porque no significan nada sin él: sus pasos apuntan a patrones o a
    /// grupos suyos, igual que los grupos apuntan a patrones.
    /// </remarks>
    public ObservableCollection<SpriteAnimation> Animations { get; } = [];

    /// <summary>
    /// Dónde cae un grupo en la lista, que es como lo direcciona el juego.
    /// </summary>
    /// <remarks>
    /// La traducción entre lo de dentro y lo de fuera: dentro los grupos se conocen por su
    /// número, que no se mueve; fuera se exportan en orden y el juego los cuenta. Devuelve -1 si
    /// ese grupo ya no está, que es justo lo que hay que poder decir.
    /// </remarks>
    public int OrdinalOfGroup(int groupId)
    {
        for (int index = 0; index < Groups.Count; index++)
        {
            if (Groups[index].Id == groupId)
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Las animaciones que usan un grupo, para poder avisar antes de borrarlo.
    /// </summary>
    /// <remarks>
    /// Borrarlo no las corrige ni las rompe en silencio: se quedan apuntando a un número que ya
    /// no existe, y eso se ve. Lo que hace falta es decirlo antes, que es para lo que está esto.
    /// </remarks>
    public IReadOnlyList<SpriteAnimation> AnimationsUsing(int groupId) =>
    [
        .. Animations.Where(animation =>
            animation.Kind == AnimationKind.Groups && Uses(animation.Steps, groupId)),
    ];

    private static bool Uses(IEnumerable<AnimationStep> steps, int groupId) => steps.Any(step => step switch
    {
        AnimationFrame frame => frame.Target == groupId,
        AnimationLoop loop => Uses(loop.Steps, groupId),
        _ => false,
    });

    /// <summary>Al abrir un banco, los números salen del fichero y hay que seguir por ahí.</summary>
    internal void ResumeGroupIds() =>
        _nextGroupId = Groups.Count == 0 ? 0 : Groups.Max(group => group.Id) + 1;

    public bool CanAddGroup => Groups.Count < MaxGroups;

    /// <summary>Crea un grupo con un único miembro. Devuelve <c>null</c> si ya no caben más.</summary>
    public SpriteGroup? NewGroup(int patternIndex)
    {
        if (!CanAddGroup || (uint)patternIndex >= (uint)_sprites.Count)
            return null;

        var group = new SpriteGroup(NextGroupName(), _nextGroupId++);
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

    /// <summary>Cuántos dibujos perderían color al pasar a MSX1.</summary>
    /// <remarks>
    /// Patrones y miembros de grupo, que llevan el suyo por separado: un miembro se siembra
    /// del patrón al crearse y a partir de ahí va por su cuenta.
    /// </remarks>
    public int ColorsAtStake =>
        _sprites.Count(sprite => sprite.HasSeveralColors)
        + Groups.Sum(group => group.Members.Count(member => member.HasSeveralColors));

    /// <summary>
    /// Pasa el banco de una máquina a la otra.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hacia MSX1 se pierde color: allí un sprite es de un color y aquí cada línea lleva el
    /// suyo, así que cada dibujo se queda con el de su primera línea. Es lo mismo que ya hacía
    /// un banco MSX1 con cualquier patrón que le llegara pegado, y conserva el color de los
    /// que ya iban de uno solo. Hacia MSX2 no se pierde nada: el color por línea existe y
    /// todas las líneas empiezan iguales.
    /// </para>
    /// <para>
    /// Los patrones no se tocan más que en el color: la máquina de un banco es
    /// <see cref="Type"/> y nada más. Hubo un <c>SpriteMSX</c> y un <c>SpriteMSX2</c>, dos
    /// clases vacías que no leía nadie, y con ellas convertir habría obligado a crear otros
    /// objetos y a dejar colgada cualquier referencia que tuviera puesta la ventana.
    /// </para>
    /// </remarks>
    public void ConvertTo(SpriteType type)
    {
        if (type == _spriteType)
            return;

        _spriteType = type;

        if (type != SpriteType.MSX)
            return;

        foreach (Sprite sprite in _sprites)
            sprite.FlattenColor();

        foreach (SpriteGroup group in Groups)
        {
            foreach (SpriteGroupMember member in group.Members)
                member.FlattenColor();
        }
    }

    /// <summary>El primer patrón sin dibujar, o -1 si están todos ocupados.</summary>
    public int FirstEmpty()
    {
        for (int index = 0; index < _sprites.Count; index++)
        {
            if (_sprites[index].IsEmpty)
                return index;
        }

        return -1;
    }

    /// <summary>
    /// El último patrón con algo dentro, o -1 si el banco está entero en blanco.
    /// </summary>
    /// <remarks>
    /// Es hasta donde llega la tabla que se exporta: escribir los 64 siempre son 2 KB
    /// aunque se usen cuatro, y eso pesa en una ROM de 32K. Los índices siguen siendo los
    /// mismos porque se corta por el final, no por el principio.
    /// </remarks>
    public int LastDrawn()
    {
        for (int index = _sprites.Count - 1; index >= 0; index--)
        {
            if (!_sprites[index].IsEmpty)
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Copia un patrón en el primer hueco libre.
    /// </summary>
    /// <remarks>
    /// En el primero libre y no justo detrás del original: detrás habría que correr los de
    /// atrás, y correr un índice es exactamente lo que este banco ya no hace.
    /// </remarks>
    /// <returns>Dónde ha caído la copia, o -1 si no queda ningún hueco.</returns>
    public int DuplicateSprite(int pos)
    {
        if ((uint)pos >= (uint)_sprites.Count)
            return -1;

        int free = FirstEmpty();

        if (free < 0)
            return -1;

        _sprites[free].CopyFrom(_sprites[pos]);

        return free;
    }
}
