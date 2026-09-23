namespace DeskPet.Core.Models;

/// <summary>What the planner wants the pet to do. There is deliberately no close/kill/input action.</summary>
public abstract record PetAction
{
    private PetAction() { }

    /// <summary>Pet stares at a distracting window; nothing is moved.</summary>
    public sealed record Alert(WindowInfo Target) : PetAction;

    /// <summary>Drag the window off the given edge of its monitor, then minimise it.</summary>
    public sealed record Haul(WindowInfo Target, ScreenEdge Edge) : PetAction;

    /// <summary>Raise an on-task window and centre it on its monitor.</summary>
    public sealed record PullForward(WindowInfo Target) : PetAction;
}
