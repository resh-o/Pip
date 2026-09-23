using DeskPet.Core.Models;

namespace DeskPet.Core.State;

/// <summary>
/// Pure, event-driven pet state machine.
/// <para>
/// Resting mood follows the last focus band: High → Working, Neutral → Idle, Low → Sad.
/// Sad therefore doubles as the droopy low-focus mood and the one-shot sheepish reaction to an
/// aborted drag; both look the same, and a reaction ends by returning to whatever the mood is.
/// The pet is "at rest" when its state equals the resting mood; only then do focus events move it
/// immediately, otherwise they just update the mood it will return to.
/// </para>
/// <para>
/// Fullscreen and Pause are tracked independently: either one forces Sleeping from any state and
/// blocks every event except its own clear (focus events still update the mood silently). The pet
/// wakes to its resting mood only once both are cleared; an interrupted action is abandoned.
/// </para>
/// <para>
/// Action flow: rest/Alert -ActionStarted→ Walking -ArrivedAtTarget→ Grabbing -Grabbed→ Dragging
/// -DragFinished→ Celebrating. Grabbing is the grip wind-up; Grabbed means the grip is established
/// and the drag begins.
/// </para>
/// </summary>
public sealed class PetStateMachine
{
    private bool _fullscreen;
    private bool _paused;

    public PetState State { get; private set; } = PetState.Idle;

    /// <summary>The mood that reactions, cancelled actions and wake-ups return to.</summary>
    public PetState Resting { get; private set; } = PetState.Idle;

    public bool IsSuspended => _fullscreen || _paused;

    /// <summary>Raised with (from, to) after every actual state change.</summary>
    public event Action<PetState, PetState>? Changed;

    /// <summary>Applies an event; returns true only if the state changed.</summary>
    public bool Fire(PetEvent e)
    {
        switch (e)
        {
            case PetEvent.FullscreenEntered:
                _fullscreen = true;
                return MoveTo(PetState.Sleeping);
            case PetEvent.Paused:
                _paused = true;
                return MoveTo(PetState.Sleeping);
            case PetEvent.FullscreenExited:
                return ClearSuspension(ref _fullscreen);
            case PetEvent.Resumed:
                return ClearSuspension(ref _paused);
            case PetEvent.FocusImproved:
                return SetMood(PetState.Working);
            case PetEvent.FocusSettled:
                return SetMood(PetState.Idle);
            case PetEvent.FocusDropped:
                return SetMood(PetState.Sad);
        }

        if (IsSuspended)
            return false;
        PetState? next = TransitionTable.Next(State, e, Resting);
        return next is { } target && MoveTo(target);
    }

    // A stray clear (flag already false) must not wake an idle doze, hence the wasSet check.
    private bool ClearSuspension(ref bool flag)
    {
        bool wasSet = flag;
        flag = false;
        return wasSet && !IsSuspended && MoveTo(Resting);
    }

    private bool SetMood(PetState mood)
    {
        bool atRest = State == Resting;
        Resting = mood;
        return atRest && !IsSuspended && MoveTo(mood);
    }

    private bool MoveTo(PetState next)
    {
        if (next == State)
            return false;
        PetState previous = State;
        State = next;
        Changed?.Invoke(previous, next);
        return true;
    }
}
