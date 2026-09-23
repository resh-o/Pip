using DeskPet.Core.Abstractions;
using DeskPet.Core.Models;
using DeskPet.Core.Planning;
using DeskPet.Core.State;
using DeskPet.Platform;
using Godot;

namespace DeskPet.Pet;

/// <summary>Carries out planner actions, reports how they went, and keeps the undo history.</summary>
internal sealed class ActionRunner
{
    private readonly ActionPlanner _planner;
    private readonly WindowDragger _dragger;
    private readonly PetController _pet;
    private readonly IWindowService _windows;
    private readonly UndoHistory _undo = new();

    public ActionRunner(ActionPlanner planner, WindowDragger dragger, PetController pet, IWindowService windows)
    {
        _planner = planner;
        _dragger = dragger;
        _pet = pet;
        _windows = windows;
    }

    public void Execute(PetAction action)
    {
        if (action is PetAction.Alert alert)
        {
            DiagnosticLog.Write("action: Alert");
            if (_pet.Machine.Fire(PetEvent.DistractionSeen))
                _pet.LookTarget = new Vector2(alert.Target.Bounds.CenterX, alert.Target.Bounds.CenterY);
            return;
        }
        if (_dragger.IsBusy || !_pet.IsReadyForAction)
        {
            _planner.RecordAborted(action); // offered again on a later tick
            return;
        }
        var bounds = (action is PetAction.Haul h ? h.Target : ((PetAction.PullForward)action).Target).Bounds;
        DiagnosticLog.Write($"action: {action.GetType().Name} started");
        _pet.LookTarget = new Vector2(bounds.CenterX, bounds.CenterY);
        _dragger.Run(action, (outcome, original) => Finish(action, outcome, original));
    }

    /// <summary>Puts the most recently moved window back where and how it was.</summary>
    public bool Undo()
    {
        _dragger.Cancel();
        while (_undo.TryPop(out var placement))
            if (_windows.RestorePlacement(placement))
                return true;
        return false;
    }

    public void CancelAll() => _dragger.Cancel();

    private void Finish(PetAction action, DragOutcome outcome, WindowPlacement? original)
    {
        DiagnosticLog.Write($"action: {action.GetType().Name} -> {outcome}");
        switch (outcome)
        {
            case DragOutcome.Done:
                _undo.Push(original!);
                _planner.RecordExecuted(action);
                break;
            case DragOutcome.UserTookOver:
                // The user decided; charge the cooldown so the pet does not immediately try again.
                _undo.Forget(original!.Handle);
                _planner.RecordExecuted(action);
                break;
            case DragOutcome.Untouchable:
                _planner.MarkUntouchable(Handle(action));
                _planner.RecordAborted(action);
                break;
            default:
                _planner.RecordAborted(action);
                break;
        }
    }

    private static nint Handle(PetAction action) => action switch
    {
        PetAction.Haul h => h.Target.Handle,
        PetAction.PullForward p => p.Target.Handle,
        _ => 0,
    };
}
