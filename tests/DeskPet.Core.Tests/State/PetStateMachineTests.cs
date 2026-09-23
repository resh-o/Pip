using DeskPet.Core.Models;
using DeskPet.Core.State;
using static DeskPet.Core.State.PetEvent;

namespace DeskPet.Core.Tests.State;

public sealed class PetStateMachineTests
{
    [Fact]
    public void Starts_idle_and_resting_idle()
    {
        var machine = new PetStateMachine();
        Assert.Equal(PetState.Idle, machine.State);
        Assert.Equal(PetState.Idle, machine.Resting);
        Assert.False(machine.IsSuspended);
    }

    [Fact]
    public void Changed_is_raised_once_per_change_with_from_and_to()
    {
        var machine = new PetStateMachine();
        var seen = new List<(PetState, PetState)>();
        machine.Changed += (from, to) => seen.Add((from, to));

        machine.Fire(DistractionSeen);
        machine.Fire(DistractionSeen);
        machine.Fire(Grabbed);
        machine.Fire(ActionStarted);

        Assert.Equal([(PetState.Idle, PetState.Alert), (PetState.Alert, PetState.Walking)], seen);
    }

    [Fact]
    public void Changed_is_not_raised_when_suspension_hits_an_already_sleeping_pet()
    {
        var machine = Machine(IdleTimeout);
        int count = 0;
        machine.Changed += (_, _) => count++;

        Assert.False(machine.Fire(FullscreenEntered));
        Assert.Equal(0, count);
        Assert.True(machine.IsSuspended);
    }

    [Fact]
    public void Fullscreen_and_pause_are_independent_flags()
    {
        var machine = Machine(ActionStarted, ArrivedAtTarget, Grabbed);
        machine.Fire(FullscreenEntered);
        machine.Fire(Paused);

        Assert.False(machine.Fire(FullscreenExited));
        Assert.Equal(PetState.Sleeping, machine.State);
        Assert.True(machine.Fire(Resumed));
        Assert.Equal(PetState.Idle, machine.State);
    }

    [Fact]
    public void Clearing_in_reverse_order_also_waits_for_both()
    {
        var machine = new PetStateMachine();
        machine.Fire(Paused);
        machine.Fire(FullscreenEntered);

        Assert.False(machine.Fire(Resumed));
        Assert.True(machine.IsSuspended);
        Assert.True(machine.Fire(FullscreenExited));
        Assert.False(machine.IsSuspended);
    }

    [Theory]
    [InlineData(DistractionSeen)]
    [InlineData(ActionStarted)]
    [InlineData(UserActive)]
    [InlineData(IdleTimeout)]
    [InlineData(AnimationDone)]
    public void Suspension_blocks_ordinary_events(PetEvent e)
    {
        var machine = new PetStateMachine();
        machine.Fire(Paused);

        Assert.False(machine.Fire(e));
        Assert.Equal(PetState.Sleeping, machine.State);
    }

    [Fact]
    public void Mood_changes_during_suspension_are_remembered_for_wake()
    {
        var machine = new PetStateMachine();
        machine.Fire(FullscreenEntered);

        Assert.False(machine.Fire(FocusImproved));
        Assert.Equal(PetState.Sleeping, machine.State);
        machine.Fire(FullscreenExited);
        Assert.Equal(PetState.Working, machine.State);
    }

    [Fact]
    public void Stray_clear_does_not_wake_an_idle_doze()
    {
        var machine = Machine(IdleTimeout);
        Assert.False(machine.Fire(FullscreenExited));
        Assert.False(machine.Fire(Resumed));
        Assert.Equal(PetState.Sleeping, machine.State);
    }

    [Fact]
    public void Resuming_after_pausing_a_doze_wakes_the_pet()
    {
        var machine = Machine(IdleTimeout);
        machine.Fire(Paused);
        Assert.True(machine.Fire(Resumed));
        Assert.Equal(PetState.Idle, machine.State);
    }

    [Theory]
    [InlineData(FocusImproved, PetState.Working)]
    [InlineData(FocusDropped, PetState.Sad)]
    [InlineData(FocusSettled, PetState.Idle)]
    public void Celebration_returns_to_current_resting_mood(PetEvent mood, PetState expected)
    {
        var machine = Machine(ActionStarted, ArrivedAtTarget, Grabbed);
        machine.Fire(mood);
        machine.Fire(DragFinished);

        Assert.Equal(PetState.Celebrating, machine.State);
        machine.Fire(AnimationDone);
        Assert.Equal(expected, machine.State);
    }

    [Fact]
    public void Sheepish_reaction_returns_to_working_when_focused()
    {
        var machine = new PetStateMachine();
        machine.Fire(FocusImproved);
        machine.Fire(ActionStarted);
        machine.Fire(ArrivedAtTarget);
        machine.Fire(DragAbortedByUser);

        Assert.Equal(PetState.Sad, machine.State);
        Assert.False(machine.Fire(FocusImproved));
        Assert.True(machine.Fire(AnimationDone));
        Assert.Equal(PetState.Working, machine.State);
    }

    [Fact]
    public void Sheepish_reaction_in_sad_mood_stays_sad()
    {
        var machine = Machine(ActionStarted, ArrivedAtTarget);
        machine.Fire(FocusDropped);
        machine.Fire(DragAbortedByUser);

        Assert.False(machine.Fire(AnimationDone));
        Assert.Equal(PetState.Sad, machine.State);
    }

    [Fact]
    public void Focus_change_mid_action_does_not_interrupt_it()
    {
        var machine = Machine(ActionStarted);

        Assert.False(machine.Fire(FocusImproved));
        Assert.Equal(PetState.Walking, machine.State);
        Assert.Equal(PetState.Working, machine.Resting);
        machine.Fire(ActionCancelled);
        Assert.Equal(PetState.Working, machine.State);
    }

    [Fact]
    public void Doze_keeps_sleeping_through_mood_change_and_wakes_to_it()
    {
        var machine = Machine(IdleTimeout);

        Assert.False(machine.Fire(FocusImproved));
        Assert.True(machine.Fire(UserActive));
        Assert.Equal(PetState.Working, machine.State);
    }

    [Fact]
    public void Full_haul_cycle_ends_at_rest()
    {
        var machine = new PetStateMachine();
        PetEvent[] cycle = [FocusImproved, DistractionSeen, ActionStarted, ArrivedAtTarget, Grabbed, DragFinished, AnimationDone];

        foreach (PetEvent e in cycle)
            Assert.True(machine.Fire(e), e.ToString());
        Assert.Equal(PetState.Working, machine.State);
    }

    private static PetStateMachine Machine(params PetEvent[] events)
    {
        var machine = new PetStateMachine();
        foreach (PetEvent e in events)
            Assert.True(machine.Fire(e), $"setup failed at {e}");
        return machine;
    }
}
