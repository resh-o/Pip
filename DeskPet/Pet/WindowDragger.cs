using DeskPet.Core.Abstractions;
using DeskPet.Core.Models;
using DeskPet.Core.Planning;
using DeskPet.Core.State;
using DeskPet.Platform;
using Godot;

namespace DeskPet.Pet;

internal enum DragOutcome
{
    Done,
    UserTookOver,
    Untouchable,
    Cancelled,
}

/// <summary>
/// Performs one physical action: walk to the target's title bar, grip it, drag it with eased
/// moves, then minimise (haul) or raise (pull). Watches every frame for the user taking the
/// window back, and aborts at once if they do.
/// </summary>
internal sealed partial class WindowDragger : Node
{
    private const double GripSeconds = 0.45;
    private const double StrainSeconds = 0.7;
    private const float TravelSeconds = 2.5f;
    private const float DragSpeed = 520f; // art units per second, so drags feel the same at any DPI

    private enum Phase { Idle, Walking, Gripping, Dragging }

    private readonly DragGuard _guard = new();
    private IWindowService _windows = null!;
    private PetMover _mover = null!;
    private PetStateMachine _machine = null!;
    private Phase _phase;
    private PetAction? _action;
    private WindowPlacement? _original;
    private Action<DragOutcome, WindowPlacement?>? _done;
    private Tween? _tween;
    private ScreenEdge? _edge;
    private (int X, int Y) _grip;
    private ScreenRect _start;
    private ScreenRect _destination;

    public bool IsBusy => _phase != Phase.Idle;

    internal void Init(IWindowService windows, PetMover mover, PetStateMachine machine)
    {
        _windows = windows;
        _mover = mover;
        _machine = machine;
    }

    public void Run(PetAction action, Action<DragOutcome, WindowPlacement?> done)
    {
        nint handle = Target(action).Handle;
        if (!_windows.CanControl(handle))
        {
            done(DragOutcome.Untouchable, null);
            return;
        }
        _original = _windows.CapturePlacement(handle);
        if (_original is null || !_windows.TryGetBounds(handle, out var bounds) || !_machine.Fire(PetEvent.ActionStarted))
        {
            done(DragOutcome.Cancelled, null);
            return;
        }
        (_action, _done, _phase) = (action, done, Phase.Walking);
        _edge = action is PetAction.Haul haul ? OpenEdge(haul, bounds) : null;
        _grip = DragTargets.GripOffset(bounds, _edge);
        _mover.TravelTo(GripPoint(bounds), OnArrived, speedFactor: 1.6f, maxSeconds: TravelSeconds);
    }

    /// <summary>Stops immediately (pause, fullscreen); the window stays wherever it is now.</summary>
    public void Cancel()
    {
        if (_phase != Phase.Idle)
            Finish(DragOutcome.Cancelled, PetEvent.ActionCancelled);
    }

    public override void _Process(double delta)
    {
        if (_phase is not (Phase.Gripping or Phase.Dragging))
            return;
        nint handle = Target(_action!).Handle;
        if (!_windows.TryGetBounds(handle, out var actual))
            Finish(DragOutcome.Cancelled, PetEvent.ActionCancelled);
        else if (_guard.IsDisturbed(actual, _windows.IsUserHandling(handle)))
            Finish(DragOutcome.UserTookOver, PetEvent.DragAbortedByUser);
    }

    private void OnArrived()
    {
        nint handle = Target(_action!).Handle;
        if (!_windows.TryGetBounds(handle, out _start) || !_machine.Fire(PetEvent.ArrivedAtTarget))
        {
            Finish(DragOutcome.Cancelled, PetEvent.ActionCancelled);
            return;
        }
        _mover.PlaceAt(GripPoint(_start)); // the window may have shifted while the pet walked
        _guard.Begin(_start);
        _phase = Phase.Gripping;
        _tween = CreateTween();
        _tween.TweenInterval(GripSeconds);
        _tween.TweenCallback(Callable.From(StartDrag));
    }

    private void StartDrag()
    {
        _machine.Fire(PetEvent.Grabbed);
        _phase = Phase.Dragging;
        _destination = Destination(_action!, _start);
        float distance = new Vector2(_destination.Left - _start.Left, _destination.Top - _start.Top).Length();
        double seconds = distance < 1 ? StrainSeconds : Mathf.Clamp(distance / (DragSpeed * _mover.Scale), 0.6, 1.8);
        _tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        _tween.TweenMethod(Callable.From<float>(DragStep), 0f, 1f, seconds);
        _tween.TweenCallback(Callable.From(Complete));
    }

    private void DragStep(float t)
    {
        int left = Mathf.RoundToInt(Mathf.Lerp(_start.Left, _destination.Left, t));
        int top = Mathf.RoundToInt(Mathf.Lerp(_start.Top, _destination.Top, t));
        if (left == _start.Left && top == _start.Top && t > 0)
            return;
        _guard.Commanded(left, top);
        _windows.Move(Target(_action!).Handle, left, top);
        _mover.PlaceAt(new Vector2(left + _grip.X, top + _grip.Y));
    }

    private void Complete()
    {
        nint handle = Target(_action!).Handle;
        if (_action is PetAction.Haul)
            _windows.Minimise(_original!);
        else
            _windows.BringToFront(handle);
        Finish(DragOutcome.Done, PetEvent.DragFinished);
    }

    private void Finish(DragOutcome outcome, PetEvent petEvent)
    {
        _tween?.Kill();
        _mover.Stop();
        _phase = Phase.Idle;
        _machine.Fire(petEvent);
        var done = _done!;
        var original = _original;
        (_action, _done, _original) = (null, null, null);
        _mover.TravelTo(_mover.FloorBelow(), static () => { }, speedFactor: 1.6f, maxSeconds: TravelSeconds);
        done(outcome, original);
    }

    private Vector2 GripPoint(ScreenRect bounds) => new(bounds.Left + _grip.X, bounds.Top + _grip.Y);

    private ScreenRect Destination(PetAction action, ScreenRect bounds) => action switch
    {
        _ when Target(action).IsMaximized => bounds,
        PetAction.Haul haul when _edge is { } edge => DragTargets.HaulDestination(bounds, haul.Target.MonitorBounds, edge),
        PetAction.Haul => bounds, // boxed in by monitors on every side: tug in place, then minimise
        _ => DragTargets.CentreIn(bounds, _windows.WorkAreaOf(Target(action).Handle)),
    };

    // The planner picks the nearest edge; never drag across onto another monitor, where Windows
    // rescales and repositions the window mid-drag (and the move would look like the user's).
    private static ScreenEdge? OpenEdge(PetAction.Haul haul, ScreenRect bounds)
    {
        var monitor = haul.Target.MonitorBounds;
        return EdgePicker.NearestOpen(bounds, monitor, edge => !Desktop.HasMonitorBeyond(monitor, bounds, edge));
    }

    private static WindowInfo Target(PetAction action) => action switch
    {
        PetAction.Haul haul => haul.Target,
        PetAction.PullForward pull => pull.Target,
        _ => throw new ArgumentOutOfRangeException(nameof(action), "Only physical actions are dragged."),
    };
}
