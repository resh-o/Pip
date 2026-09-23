using DeskPet.Core.Models;
using DeskPet.Core.State;
using static DeskPet.Core.State.PetEvent;

namespace DeskPet.Core.Tests.State;

/// <summary>Exhaustive (starting state × event) check against the documented transition list.</summary>
public sealed class PetStateMachineTableTests
{
    // Named starting points, all with the default (Idle) resting mood unless the name says otherwise.
    private static readonly Dictionary<string, PetEvent[]> Setups = new()
    {
        ["Idle"] = [],
        ["Working"] = [FocusImproved],
        ["SadMood"] = [FocusDropped],
        ["Alert"] = [DistractionSeen],
        ["Walking"] = [ActionStarted],
        ["Grabbing"] = [ActionStarted, ArrivedAtTarget],
        ["Dragging"] = [ActionStarted, ArrivedAtTarget, Grabbed],
        ["Celebrating"] = [ActionStarted, ArrivedAtTarget, Grabbed, DragFinished],
        ["SadReaction"] = [ActionStarted, ArrivedAtTarget, Grabbed, DragAbortedByUser],
        ["Dozing"] = [IdleTimeout],
        ["Fullscreen"] = [FullscreenEntered],
        ["Paused"] = [Paused],
    };

    // Everything not listed must be rejected, except that FullscreenEntered/Paused always put an
    // awake pet to sleep.
    private static readonly Dictionary<(string, PetEvent), PetState> Valid = new()
    {
        [("Idle", FocusImproved)] = PetState.Working,
        [("Idle", FocusDropped)] = PetState.Sad,
        [("Idle", DistractionSeen)] = PetState.Alert,
        [("Idle", ActionStarted)] = PetState.Walking,
        [("Idle", IdleTimeout)] = PetState.Sleeping,
        [("Working", FocusDropped)] = PetState.Sad,
        [("Working", FocusSettled)] = PetState.Idle,
        [("Working", DistractionSeen)] = PetState.Alert,
        [("Working", ActionStarted)] = PetState.Walking,
        [("Working", IdleTimeout)] = PetState.Sleeping,
        [("SadMood", FocusImproved)] = PetState.Working,
        [("SadMood", FocusSettled)] = PetState.Idle,
        [("SadMood", DistractionSeen)] = PetState.Alert,
        [("SadMood", ActionStarted)] = PetState.Walking,
        [("SadMood", IdleTimeout)] = PetState.Sleeping,
        [("Alert", ActionStarted)] = PetState.Walking,
        [("Alert", ActionCancelled)] = PetState.Idle,
        [("Alert", AnimationDone)] = PetState.Idle,
        [("Walking", ArrivedAtTarget)] = PetState.Grabbing,
        [("Walking", ActionCancelled)] = PetState.Idle,
        [("Grabbing", Grabbed)] = PetState.Dragging,
        [("Grabbing", DragAbortedByUser)] = PetState.Sad,
        [("Grabbing", ActionCancelled)] = PetState.Idle,
        [("Dragging", DragFinished)] = PetState.Celebrating,
        [("Dragging", DragAbortedByUser)] = PetState.Sad,
        [("Dragging", ActionCancelled)] = PetState.Idle,
        [("Celebrating", AnimationDone)] = PetState.Idle,
        [("SadReaction", AnimationDone)] = PetState.Idle,
        [("Dozing", UserActive)] = PetState.Idle,
        [("Fullscreen", FullscreenExited)] = PetState.Idle,
        [("Paused", Resumed)] = PetState.Idle,
    };

    public static IEnumerable<object[]> AllPairs()
    {
        foreach (string setup in Setups.Keys)
            foreach (PetEvent e in Enum.GetValues<PetEvent>())
                yield return [setup, e];
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Every_event_from_every_state_follows_the_table(string setup, PetEvent e)
    {
        var machine = Build(setup);
        PetState before = machine.State;
        bool suspending = e is FullscreenEntered or Paused && before != PetState.Sleeping;

        bool changed = machine.Fire(e);

        if (Valid.TryGetValue((setup, e), out PetState expected))
        {
            Assert.True(changed);
            Assert.Equal(expected, machine.State);
        }
        else if (suspending)
        {
            Assert.True(changed);
            Assert.Equal(PetState.Sleeping, machine.State);
        }
        else
        {
            Assert.False(changed);
            Assert.Equal(before, machine.State);
        }
    }

    private static PetStateMachine Build(string setup)
    {
        var machine = new PetStateMachine();
        foreach (PetEvent e in Setups[setup])
            Assert.True(machine.Fire(e), $"setup {setup} failed at {e}");
        return machine;
    }
}
