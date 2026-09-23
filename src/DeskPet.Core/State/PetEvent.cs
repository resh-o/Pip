namespace DeskPet.Core.State;

public enum PetEvent
{
    /// <summary>Focus score entered the high band; resting mood becomes Working.</summary>
    FocusImproved,

    /// <summary>Focus score entered the low band; resting mood becomes Sad.</summary>
    FocusDropped,

    /// <summary>Focus score entered the neutral band; resting mood becomes Idle.</summary>
    FocusSettled,

    DistractionSeen,

    /// <summary>The pet sets off towards a target window.</summary>
    ActionStarted,

    ArrivedAtTarget,

    /// <summary>The pet has a grip on the window and starts dragging it.</summary>
    Grabbed,

    DragFinished,

    /// <summary>The user took the window back mid-grab or mid-drag.</summary>
    DragAbortedByUser,

    /// <summary>The action ended without a result (target vanished, planner gave up).</summary>
    ActionCancelled,

    FullscreenEntered,
    FullscreenExited,
    Paused,
    Resumed,

    /// <summary>Long quiet period; the pet dozes off.</summary>
    IdleTimeout,

    /// <summary>User activity after an idle doze; the pet wakes.</summary>
    UserActive,

    /// <summary>A one-shot reaction (Alert, Celebrating, Sad) finished playing.</summary>
    AnimationDone,
}
