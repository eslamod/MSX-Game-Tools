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
public sealed class AnimationStepViewModel(AnimationStep step, int depth, IList<AnimationStep> owner)
{
    public AnimationStep Step { get; } = step;

    /// <summary>Cuántos bucles lo envuelven, para sangrarlo.</summary>
    public int Depth { get; } = depth;

    /// <summary>
    /// La lista de la que cuelga.
    /// </summary>
    /// <remarks>
    /// Es lo que hace que añadir y mover se comporten como uno espera: estando dentro de un
    /// bucle, lo que se añade cae dentro, y subir un paso no lo saca del bucle de un salto.
    /// </remarks>
    public IList<AnimationStep> Owner { get; } = owner;

    public AnimationFrame? Frame => Step as AnimationFrame;

    public AnimationLoop? Loop => Step as AnimationLoop;

    public bool IsLoop => Loop is not null;

    public bool IsFrame => Frame is not null;
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

        animation.PropertyChanged += (_, _) => Refresh();

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
            Steps.Add(new AnimationStepViewModel(step, depth, steps));

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
