using DeskPet.Core.Models;
using DeskPet.Core.State;
using DeskPet.Platform;
using DeskPet.Ui;
using Godot;

namespace DeskPet.Pet;

/// <summary>Glue between the pure state machine and the pet's view, movement and frame pacing.</summary>
public partial class PetController : Node
{
    private static readonly TimeSpan SleepAfterIdle = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AwakeThreshold = TimeSpan.FromSeconds(1.5);
    private const double IdleCheckSeconds = 1.0;
    private const float EyeHeight = 55f;

    private readonly PetStateMachine _machine = new();
    private readonly FramePacer _pacer = new();
    private PetView _view = null!;
    private PetMover _mover = null!;
    private PassthroughUpdater _passthrough = null!;
    private double _idleCheckIn;
    private double _strollIn;
    private double _reactionLeft;

    internal void Init(PetView view, PetMover mover, PassthroughUpdater passthrough)
    {
        _view = view;
        _mover = mover;
        _passthrough = passthrough;
        _machine.Changed += OnStateChanged;
        _strollIn = NextStrollDelay();
        Show(_machine.State);
    }

    public override void _Process(double delta)
    {
        _pacer.Update(delta, _mover.IsWalking);
        TrackEyes();
        CountDownReaction(delta);
        CheckUserIdle(delta);
        MaybeStroll(delta);
    }

    private void OnStateChanged(PetState from, PetState to)
    {
        if (from == PetState.Walking)
            _mover.Stop();
        _reactionLeft = ReactionSeconds(to);
        Show(to);
    }

    private void Show(PetState state)
    {
        _view.ShowState(state);
        _pacer.OnStateChanged(state);
        _passthrough.Refresh();
    }

    // One-shot reactions end on a timer; Sad only counts as a reaction when it isn't the mood.
    private double ReactionSeconds(PetState state) => state switch
    {
        PetState.Celebrating => 1.7,
        PetState.Alert => 3.0,
        PetState.Sad when _machine.Resting != PetState.Sad => 2.2,
        _ => 0,
    };

    private void CountDownReaction(double delta)
    {
        if (_reactionLeft <= 0)
            return;
        _reactionLeft -= delta;
        if (_reactionLeft <= 0)
            _machine.Fire(PetEvent.AnimationDone);
    }

    private void TrackEyes()
    {
        if (_machine.State == PetState.Sleeping)
            return;
        var (x, y) = Desktop.CursorPosition();
        Vector2 eyes = _mover.Feet - new Vector2(0, EyeHeight * _mover.Scale);
        _view.LookToward((new Vector2(x, y) - eyes) / _mover.Scale);
    }

    private void CheckUserIdle(double delta)
    {
        _idleCheckIn -= delta;
        if (_idleCheckIn > 0)
            return;
        _idleCheckIn = IdleCheckSeconds;
        TimeSpan idle = Desktop.UserIdleTime();
        if (idle >= SleepAfterIdle)
            _machine.Fire(PetEvent.IdleTimeout);
        else if (idle < AwakeThreshold)
            _machine.Fire(PetEvent.UserActive);
    }

    private void MaybeStroll(double delta)
    {
        if (_machine.State != _machine.Resting || _machine.IsSuspended)
            return;
        _strollIn -= delta;
        if (_strollIn > 0)
            return;
        _strollIn = NextStrollDelay();
        float x = PickStrollX();
        if (!_machine.Fire(PetEvent.ActionStarted))
            return;
        _view.SetFacing(x - _mover.Feet.X);
        _mover.TravelTo(new Vector2(x, _mover.Feet.Y), () => _machine.Fire(PetEvent.StrollFinished));
    }

    private float PickStrollX()
    {
        Vector2 feet = _mover.Feet;
        ScreenRect work = Desktop.WorkAreaAt(Mathf.RoundToInt(feet.X), Mathf.RoundToInt(feet.Y) - 1);
        float margin = PetMover.CanvasSize.X * 0.5f * _mover.Scale;
        float span = Mathf.Min(420f * _mover.Scale, work.Width * 0.5f);
        float x = feet.X + (float)GD.RandRange(-span, span);
        return Mathf.Clamp(x, work.Left + margin, work.Right - margin);
    }

    private static double NextStrollDelay() => GD.RandRange(25.0, 70.0);
}
