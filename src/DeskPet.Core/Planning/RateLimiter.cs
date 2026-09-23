using DeskPet.Core.Abstractions;

namespace DeskPet.Core.Planning;

/// <summary>Global cap on physical actions: a minimum gap between them and a ten-minute sliding budget.</summary>
public sealed class RateLimiter
{
    private static readonly TimeSpan BudgetWindow = TimeSpan.FromMinutes(10);

    private readonly IClock _clock;
    private readonly ISettings _settings;
    private readonly Queue<DateTimeOffset> _recent = new();
    private DateTimeOffset? _last;

    public RateLimiter(IClock clock, ISettings settings)
    {
        _clock = clock;
        _settings = settings;
    }

    public bool CanAct() => HasBudget() && GapElapsed();

    /// <summary>Only the ten-minute budget; used for the pull that completes a haul, which is one intervention.</summary>
    public bool HasBudget()
    {
        PruneOld(_clock.UtcNow);
        return _recent.Count < _settings.Current.MaxActionsPerTenMinutes;
    }

    public void Record()
    {
        var now = _clock.UtcNow;
        _recent.Enqueue(now);
        _last = now;
    }

    private bool GapElapsed() =>
        _last is not { } last || _clock.UtcNow - last >= TimeSpan.FromSeconds(_settings.Current.MinActionGapSeconds);

    private void PruneOld(DateTimeOffset now)
    {
        while (_recent.Count > 0 && now - _recent.Peek() >= BudgetWindow)
        {
            _recent.Dequeue();
        }
    }
}
