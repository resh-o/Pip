using DeskPet.Core.Models;

namespace DeskPet.Core.State;

/// <summary>
/// Ordinary transitions, i.e. everything except suspension and mood events. <c>resting</c> is the
/// mood the pet returns to after reactions; being "at rest" means the current state equals it.
/// </summary>
internal static class TransitionTable
{
    public static PetState? Next(PetState from, PetEvent e, PetState resting)
    {
        bool atRest = from == resting;
        return e switch
        {
            PetEvent.DistractionSeen when atRest => PetState.Alert,
            PetEvent.ActionStarted when atRest || from == PetState.Alert => PetState.Walking,
            PetEvent.ArrivedAtTarget when from == PetState.Walking => PetState.Grabbing,
            PetEvent.StrollFinished when from == PetState.Walking => resting,
            PetEvent.Grabbed when from == PetState.Grabbing => PetState.Dragging,
            PetEvent.DragFinished when from == PetState.Dragging => PetState.Celebrating,
            PetEvent.DragAbortedByUser when IsHolding(from) => PetState.Sad,
            PetEvent.ActionCancelled when IsOnAction(from) => resting,
            PetEvent.AnimationDone when IsReaction(from) => resting,
            PetEvent.IdleTimeout when atRest => PetState.Sleeping,
            PetEvent.UserActive when from == PetState.Sleeping => resting,
            _ => null,
        };
    }

    private static bool IsHolding(PetState s) => s is PetState.Grabbing or PetState.Dragging;

    private static bool IsOnAction(PetState s) => s is PetState.Alert or PetState.Walking || IsHolding(s);

    private static bool IsReaction(PetState s) => s is PetState.Alert or PetState.Celebrating or PetState.Sad;
}
