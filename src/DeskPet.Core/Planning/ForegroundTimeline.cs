using DeskPet.Core.Models;

namespace DeskPet.Core.Planning;

/// <summary>
/// The history of judged foreground samples the planner escalates on: the current distraction
/// episode, how long the same distracting window has been in front, and how long the user has idled.
/// </summary>
internal sealed class ForegroundTimeline
{
    private WindowInfo? _judged;
    private bool _inEpisode;
    private nint _distractionHandle;
    private DateTimeOffset _distractionSince;
    private DateTimeOffset? _neutralSince;

    public Verdict Verdict { get; private set; }

    /// <summary>Whether this distraction episode has already been alerted; alerts fire once per episode.</summary>
    public bool Alerted { get; private set; }

    /// <summary>A haul happened and no on-task window has come forward since.</summary>
    public bool PullPending { get; private set; }

    public nint LastOnTaskHandle { get; private set; }

    public void Record(WindowInfo foreground, Verdict verdict, DateTimeOffset now)
    {
        switch (verdict)
        {
            case Verdict.Distraction:
                ContinueDistraction(foreground.Handle, now);
                break;
            case Verdict.Neutral:
                EndEpisode();
                _neutralSince ??= now;
                break;
            case Verdict.OnTask:
                EndEpisode();
                _neutralSince = null;
                LastOnTaskHandle = foreground.Handle;
                PullPending = false;
                break;
        }

        _judged = foreground;
        Verdict = verdict;
    }

    /// <summary>
    /// True when <paramref name="foreground"/> is exactly what was last judged. Title counts because a
    /// tab switch changes what the window is, and we must not act on a stale verdict.
    /// </summary>
    public bool IsJudged(WindowInfo foreground) =>
        _judged is not null
        && foreground.Handle == _judged.Handle
        && string.Equals(foreground.Title, _judged.Title, StringComparison.Ordinal);

    public TimeSpan DistractedFor(DateTimeOffset now) => _inEpisode ? now - _distractionSince : TimeSpan.Zero;

    public TimeSpan NeutralFor(DateTimeOffset now) => _neutralSince is { } since ? now - since : TimeSpan.Zero;

    public void MarkAlerted() => Alerted = true;

    public void HaulCompleted() => PullPending = true;

    public void PullCompleted(DateTimeOffset now)
    {
        PullPending = false;
        // Restart idle time so a long neutral stretch earns one pull, not one per on-task window.
        if (_neutralSince is not null)
        {
            _neutralSince = now;
        }
    }

    // Hopping between distracting windows is one episode (no re-alert), but the haul clock is per window
    // so we only ever haul the thing that has actually been in front for the whole period.
    private void ContinueDistraction(nint handle, DateTimeOffset now)
    {
        _neutralSince = null;
        if (!_inEpisode)
        {
            _inEpisode = true;
            Alerted = false;
        }

        if (_distractionHandle != handle)
        {
            _distractionHandle = handle;
            _distractionSince = now;
        }
    }

    private void EndEpisode()
    {
        _inEpisode = false;
        _distractionHandle = 0;
    }
}
