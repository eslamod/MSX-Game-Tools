using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MSX_GameTools.Entities;
using MSX_GameTools.Localization;
using MSX_GameTools.Services;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Un paso en la lista, con su sangría y sabiendo de qué lista cuelga.
/// </summary>
/// <remarks>
/// Los pasos son un árbol —un bucle lleva los suyos dentro— pero se editan en una lista plana
/// con sangría. Un árbol de verdad se arrastra peor y no compra nada aquí: la profundidad rara
/// vez pasa de dos, y lo que se hace todo el rato es mover un paso una fila arriba o abajo.
/// </remarks>
public sealed class AnimationStepViewModel : ObservableObject
{
    /// <summary>Lo que se mete por la izquierda por cada bucle que lo envuelve.</summary>
    private const int Step16 = 16;

    private readonly AnimationKind _kind;

    /// <summary>
    /// La fila se entera sola de que le han cambiado los números.
    /// </summary>
    /// <remarks>
    /// Sin esto habría que rehacer la lista entera al tocar una espera, y rehacerla vuelve a
    /// enlazar los controles que editan el paso, que vuelven a avisar de que han cambiado: se
    /// montaba un tiovivo de seis reconstrucciones por cada clic.
    /// </remarks>
    public AnimationStepViewModel(
        AnimationStep step, int depth, IList<AnimationStep> owner, AnimationKind kind)
    {
        Step = step;
        Depth = depth;
        Owner = owner;
        _kind = kind;

        step.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(DetailText));
            OnPropertyChanged(nameof(WaitText));
            OnPropertyChanged(nameof(OffsetText));
            OnPropertyChanged(nameof(EventText));

            OnPropertyChanged(nameof(Target));
            OnPropertyChanged(nameof(Wait));
            OnPropertyChanged(nameof(OffsetX));
            OnPropertyChanged(nameof(OffsetY));
            OnPropertyChanged(nameof(Event));
            OnPropertyChanged(nameof(Times));
        };
    }

    public AnimationStep Step { get; }

    /// <summary>Cuántos bucles lo envuelven, para sangrarlo.</summary>
    public int Depth { get; }

    /// <summary>
    /// La lista de la que cuelga.
    /// </summary>
    /// <remarks>
    /// Es lo que hace que añadir y mover se comporten como uno espera: estando dentro de un
    /// bucle, lo que se añade cae dentro, y subir un paso no lo saca del bucle de un salto.
    /// </remarks>
    public IList<AnimationStep> Owner { get; }

    public AnimationFrame? Frame => Step as AnimationFrame;

    public AnimationLoop? Loop => Step as AnimationLoop;

    public bool IsLoop => Loop is not null;

    public bool IsFrame => Frame is not null;

    /// <summary>La sangría, que es lo que enseña de qué bucle cuelga.</summary>
    public Avalonia.Thickness Indent => new(Depth * Step16, 0, 0, 0);

    /// <summary>De qué es el paso: un bucle, o un patrón o un grupo según la animación.</summary>
    public string Label => Localizer.Instance[IsLoop
        ? "AnimStepLoop"
        : _kind == AnimationKind.Groups ? "AnimStepGroup" : "AnimStepPattern"];

    /// <summary>Y a qué apunta: el número, o las vueltas si es un bucle.</summary>
    public string DetailText => IsLoop ? $"x{Loop!.Times}" : $"{Frame!.Target}";

    /// <summary>Lo que se queda en pantalla, que en un bucle no significa nada.</summary>
    public string WaitText => IsLoop ? string.Empty : $"{Frame!.Wait}";

    /// <summary>Dónde cae, si está desplazado. En blanco cuando está en su sitio.</summary>
    public string OffsetText => IsLoop || (Frame!.OffsetX == 0 && Frame.OffsetY == 0)
        ? string.Empty
        : $"{Frame.OffsetX:+#;-#;0},{Frame.OffsetY:+#;-#;0}";

    /// <summary>El aviso al juego, si este paso avisa de algo.</summary>
    public string EventText => IsLoop || Frame!.Event == 0 ? string.Empty : $"!{Frame.Event}";

    // ------------------------------------------------------------------ los numeros

    /// <summary>
    /// Los números del paso, admitiendo que estén vacíos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vacío no es cero: borrando el contenido de una casilla para escribir otro número, la
    /// casilla se queda un momento sin nada, y eso no puede llegar al paso. Poniendo cero se
    /// vería el patrón 0 de refilón cada vez que se teclea; reventando, que es lo que hacía,
    /// el editor se llena de un error de conversión mientras escribes.
    /// </para>
    /// <para>
    /// Así que el vacío se ignora y el paso se queda como estaba hasta que haya un número.
    /// </para>
    /// </remarks>
    public int? Target
    {
        get => Frame?.Target;
        set => Set(value, number => Frame!.Target = number);
    }

    /// <inheritdoc cref="Target"/>
    public int? Wait
    {
        get => Frame?.Wait;
        set => Set(value, number => Frame!.Wait = number);
    }

    /// <inheritdoc cref="Target"/>
    public int? OffsetX
    {
        get => Frame?.OffsetX;
        set => Set(value, number => Frame!.OffsetX = number);
    }

    /// <inheritdoc cref="Target"/>
    public int? OffsetY
    {
        get => Frame?.OffsetY;
        set => Set(value, number => Frame!.OffsetY = number);
    }

    /// <inheritdoc cref="Target"/>
    public int? Event
    {
        get => Frame?.Event;
        set => Set(value, number => Frame!.Event = number);
    }

    /// <inheritdoc cref="Target"/>
    public int? Times
    {
        get => Loop?.Times;
        set
        {
            if (value is { } number && Loop is not null)
                Loop.Times = number;
        }
    }

    private void Set(int? value, Action<int> apply)
    {
        if (value is { } number && Frame is not null)
            apply(number);
    }
}

/// <summary>
/// Una animación en el panel: sus pasos en fila y lo que cuesta.
/// </summary>
/// <remarks>
/// La lista plana se rehace entera con cualquier cambio. Es barata —una animación son unos pocos
/// pasos— y ahorra tener que mantener sangrías y posiciones a mano, que es donde estarían los
/// fallos.
/// </remarks>
public partial class SpriteAnimationViewModel : ObservableObject
{
    private readonly SpriteBank _bank;

    [ObservableProperty]
    private AnimationStepViewModel? _selectedStep;

    public SpriteAnimationViewModel(SpriteAnimation animation, SpriteBank bank)
    {
        Animation = animation;
        _bank = bank;

        // Cambiar de patrones a grupos cambia lo que dice cada fila, asi que hay que rehacer
        // la lista y no solo volver a resolver.
        animation.PropertyChanged += (_, e) =>
        {
            // Reemitido, que si no la lista se queda con el nombre de cuando se creó: estas
            // propiedades son un paso a través y quien avisa de su cambio es la entidad.
            OnPropertyChanged(e.PropertyName);

            if (e.PropertyName == nameof(SpriteAnimation.Kind))
                Rebuild();
            else
                Refresh();
        };

        Rebuild();
    }

    private static Localizer Text => Localizer.Instance;

    public SpriteAnimation Animation { get; }

    /// <summary>Ha cambiado algo que afecta a lo que se ve.</summary>
    public event Action? Changed;

    /// <summary>Los pasos en fila, con su sangría.</summary>
    public ObservableCollection<AnimationStepViewModel> Steps { get; } = [];

    /// <summary>La animación resuelta, que es lo que se reproduce.</summary>
    public AnimationTimeline Timeline { get; private set; } = new([], false, false);

    public string Name
    {
        get => Animation.Name;
        set => Animation.Name = value;
    }

    public AnimationKind Kind
    {
        get => Animation.Kind;
        set => Animation.Kind = value;
    }

    public AnimationMode Mode
    {
        get => Animation.Mode;
        set => Animation.Mode = value;
    }

    /// <summary>Lo que hay que tener cargado para esta animación.</summary>
    /// <remarks>
    /// Con un banco en ROM que se vuelca a VRAM por animación, esto es lo que dice qué subir.
    /// </remarks>
    public string Cost
    {
        get
        {
            string what = Kind == AnimationKind.Groups ? Text["AnimUsesGroups"] : Text["AnimUsesPatterns"];

            return Text.Format(
                "AnimCost", Timeline.Frames.Count, Timeline.Ticks, Timeline.Targets.Count, what);
        }
    }

    /// <summary>Si hubo que dejar fotogramas fuera al resolverla.</summary>
    public bool IsTruncated => Timeline.Truncated;

    /// <summary>El máximo que se puede poner en un paso, que depende de a qué apunte.</summary>
    public int MaxTarget => Kind == AnimationKind.Groups
        ? Math.Max(0, _bank.Groups.Count - 1)
        : _bank.Capacity - 1;

    // ------------------------------------------------------------------ editar la lista

    /// <summary>
    /// Añade un fotograma detrás del que esté elegido, y en su misma lista.
    /// </summary>
    /// <remarks>
    /// En su misma lista y no siempre al final: estando dentro de un bucle, lo que se añade tiene
    /// que caer dentro. Es lo único que permite montar un bucle sin arrastrar después cada paso.
    /// </remarks>
    [RelayCommand]
    private void AddFrame() => Insert(new AnimationFrame());

    [RelayCommand]
    private void AddLoop() => Insert(new AnimationLoop());

    private void Insert(AnimationStep step)
    {
        // Dentro del bucle elegido y no detrás de él: quien acaba de crear un bucle lo que
        // quiere es meterle pasos, no ponerlos a su lado.
        if (SelectedStep?.Loop is { } loop)
        {
            loop.Steps.Add(step);
        }
        else if (SelectedStep is { } selected)
        {
            selected.Owner.Insert(selected.Owner.IndexOf(selected.Step) + 1, step);
        }
        else
        {
            Animation.Steps.Add(step);
        }

        Rebuild();

        SelectedStep = Steps.FirstOrDefault(row => ReferenceEquals(row.Step, step));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RemoveStep()
    {
        if (SelectedStep is not { } selected)
            return;

        int at = selected.Owner.IndexOf(selected.Step);

        selected.Owner.Remove(selected.Step);

        Rebuild();

        SelectedStep = Steps.FirstOrDefault(row =>
            ReferenceEquals(row.Owner, selected.Owner) && row.Owner.IndexOf(row.Step) == at)
            ?? Steps.LastOrDefault();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveUp() => Move(-1);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveDown() => Move(1);

    /// <summary>
    /// Mueve el paso dentro de su lista.
    /// </summary>
    /// <remarks>
    /// Dentro y no fuera: subiendo el primero de un bucle no se sale del bucle. Sacarlo sería
    /// otra cosa distinta —y con el mismo gesto—, así que en el borde no se mueve y ya está.
    /// </remarks>
    private void Move(int by)
    {
        if (SelectedStep is not { } selected)
            return;

        int at = selected.Owner.IndexOf(selected.Step);
        int to = at + by;

        if (at < 0 || (uint)to >= (uint)selected.Owner.Count)
            return;

        AnimationStep step = selected.Step;

        selected.Owner.RemoveAt(at);
        selected.Owner.Insert(to, step);

        Rebuild();

        SelectedStep = Steps.FirstOrDefault(row => ReferenceEquals(row.Step, step));
    }

    private bool HasSelection() => SelectedStep is not null;

    partial void OnSelectedStepChanged(AnimationStepViewModel? value)
    {
        RemoveStepCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    // ------------------------------------------------------------------ rehacer

    /// <summary>Rehace la lista plana y vuelve a resolver la animación.</summary>
    public void Rebuild()
    {
        Steps.Clear();

        Flatten(Animation.Steps, 0);
        Refresh();
    }

    /// <summary>Vuelve a resolver sin tocar la lista, para cuando sólo cambian los números.</summary>
    public void Refresh()
    {
        Timeline = AnimationResolver.Of(Animation);

        OnPropertyChanged(nameof(Timeline));
        OnPropertyChanged(nameof(Cost));
        OnPropertyChanged(nameof(IsTruncated));
        OnPropertyChanged(nameof(MaxTarget));

        Changed?.Invoke();
    }

    private void Flatten(IList<AnimationStep> steps, int depth)
    {
        foreach (AnimationStep step in steps)
        {
            Steps.Add(new AnimationStepViewModel(step, depth, steps, Kind));

            if (step is not AnimationLoop loop)
                continue;

            Watch(loop.Steps);
            Flatten(loop.Steps, depth + 1);
        }
    }

    /// <summary>
    /// Los pasos de dentro de un bucle también mueven la lista.
    /// </summary>
    /// <remarks>
    /// Se vuelve a enganchar en cada reconstrucción y no pasa nada: los objetos son los mismos y
    /// el evento se sustituye. Sin esto, meter un paso en un bucle no aparecería en la lista.
    /// </remarks>
    private void Watch(ObservableCollection<AnimationStep> steps)
    {
        steps.CollectionChanged -= OnStepsChanged;
        steps.CollectionChanged += OnStepsChanged;
    }

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
}
