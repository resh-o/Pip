using DeskPet.Core.Abstractions;
using DeskPet.Core.Models;

namespace DeskPet.Core.Planning;

/// <summary>
/// Turns verdicts into pet actions, escalating Alert → Haul → PullForward, and is the one place every
/// safety rule, cooldown and rate limit is enforced.
/// </summary>
/// <remarks>
/// Physical actions (Haul, PullForward) are handed out one at a time: once returned, nothing physical is
/// planned until the controller reports <see cref="RecordExecuted"/> or <see cref="RecordAborted"/>.
/// Cooldown and rate budget are only spent on <see cref="RecordExecuted"/>, so an action the front-end
/// could not carry out (user dragging the window, pet busy) is not charged. If the controller never
/// reports back the pet simply stops moving windows, which is the safe way to fail.
/// Alerts move nothing, so they are fire-and-forget and limited to one per distraction episode.
/// </remarks>
public sealed class ActionPlanner
{
    private readonly IClock _clock;
    private readonly ISettings _settings;
    private readonly CooldownLedger _cooldowns;
    private readonly RateLimiter _rateLimiter;
    private readonly ForegroundTimeline _timeline = new();
    private SafetyPolicy _policy;
    private PetAction? _inFlight;

    public ActionPlanner(IClock clock, ISettings settings, string ownProcessName)
    {
        _clock = clock;
        _settings = settings;
        _cooldowns = new CooldownLedger(clock, settings);
        _rateLimiter = new RateLimiter(clock, settings);
        _policy = new SafetyPolicy(settings.Current, ownProcessName);
    }

    /// <summary>Call when a debounced foreground sample has been judged.</summary>
    public PetAction? OnJudged(
        WindowInfo foreground,
        Verdict verdict,
        IReadOnlyList<(WindowInfo Window, Verdict Verdict)> knownWindows,
        bool paused)
    {
        _timeline.Record(foreground, verdict, _clock.UtcNow);
        return Decide(foreground, knownWindows, paused);
    }

    /// <summary>Call on every poll so time-based escalation happens between judgements.</summary>
    public PetAction? OnTick(
        WindowInfo? foreground,
        IReadOnlyList<(WindowInfo Window, Verdict Verdict)> knownWindows,
        bool paused)
    {
        // A foreground that hasn't been judged yet has no verdict we could safely act on.
        if (foreground is not null && !_timeline.IsJudged(foreground))
        {
            return null;
        }

        return Decide(foreground, knownWindows, paused);
    }

    public void RecordExecuted(PetAction action)
    {
        if (!Equals(action, _inFlight))
        {
            return;
        }

        _inFlight = null;
        switch (action)
        {
            case PetAction.Haul haul:
                Charge(haul.Target);
                _timeline.HaulCompleted();
                break;
            case PetAction.PullForward pull:
                Charge(pull.Target);
                _timeline.PullCompleted(_clock.UtcNow);
                break;
        }
    }

    public void RecordAborted(PetAction action)
    {
        if (Equals(action, _inFlight))
        {
            _inFlight = null;
        }
    }

    /// <summary>For windows the platform refused to move (elevated / UIPI); they are never targeted again.</summary>
    public void MarkUntouchable(nint handle) => _policy.MarkUntouchable(handle);

    private PetAction? Decide(
        WindowInfo? foreground,
        IReadOnlyList<(WindowInfo Window, Verdict Verdict)> knownWindows,
        bool paused)
    {
        RefreshPolicy();
        if (_inFlight is not null || !_policy.ActionsAllowed(foreground, paused))
        {
            return null;
        }

        if (foreground is not null && _timeline.Verdict == Verdict.Distraction)
        {
            return PlanDistraction(foreground);
        }

        return PlanPull(foreground, knownWindows);
    }

    private PetAction? PlanDistraction(WindowInfo foreground)
    {
        if (!_policy.CanTouch(foreground))
        {
            return null;
        }

        if (!_timeline.Alerted)
        {
            _timeline.MarkAlerted();
            return new PetAction.Alert(foreground);
        }

        var haulDue = _timeline.DistractedFor(_clock.UtcNow) >= TimeSpan.FromSeconds(_settings.Current.HaulAfterSeconds);
        if (!haulDue || _cooldowns.IsCoolingDown(foreground.Handle) || !_rateLimiter.CanAct())
        {
            return null;
        }

        return Launch(new PetAction.Haul(foreground, EdgePicker.Nearest(foreground.Bounds, foreground.MonitorBounds)));
    }

    private PetAction? PlanPull(WindowInfo? foreground, IReadOnlyList<(WindowInfo Window, Verdict Verdict)> knownWindows)
    {
        // The pull after a haul finishes the same intervention, so it skips the min gap but not the budget.
        bool allowed;
        if (_timeline.PullPending)
        {
            allowed = _rateLimiter.HasBudget();
        }
        else
        {
            var idledOut = foreground is not null
                && _timeline.Verdict == Verdict.Neutral
                && _timeline.NeutralFor(_clock.UtcNow) >= TimeSpan.FromSeconds(_settings.Current.PullAfterSeconds);
            allowed = idledOut && _rateLimiter.CanAct();
        }

        var target = allowed ? PickPullTarget(foreground, knownWindows) : null;
        return target is null ? null : Launch(new PetAction.PullForward(target));
    }

    // Prefer the on-task window the user was last actually working in; otherwise the first eligible one.
    private WindowInfo? PickPullTarget(WindowInfo? foreground, IReadOnlyList<(WindowInfo Window, Verdict Verdict)> knownWindows)
    {
        WindowInfo? fallback = null;
        for (var i = 0; i < knownWindows.Count; i++)
        {
            var (window, verdict) = knownWindows[i];
            if (verdict != Verdict.OnTask || window.Handle == foreground?.Handle || !CanMove(window))
            {
                continue;
            }

            if (window.Handle == _timeline.LastOnTaskHandle)
            {
                return window;
            }

            fallback ??= window;
        }

        return fallback;
    }

    private bool CanMove(WindowInfo window) => _policy.CanTouch(window) && !_cooldowns.IsCoolingDown(window.Handle);

    private PetAction Launch(PetAction action)
    {
        _inFlight = action;
        return action;
    }

    private void Charge(WindowInfo target)
    {
        _cooldowns.Record(target.Handle);
        _rateLimiter.Record();
    }

    // Settings are immutable snapshots, so a reference change is exactly "the user edited settings".
    private void RefreshPolicy()
    {
        if (!ReferenceEquals(_policy.Settings, _settings.Current))
        {
            _policy = _policy.WithSettings(_settings.Current);
        }
    }
}
