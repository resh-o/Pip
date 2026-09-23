using DeskPet.Core.Abstractions;

namespace DeskPet.Core.Brain;

/// <summary>Sliding one-minute cap on API calls, plus a server-requested pause (retry-after).</summary>
public sealed class ApiRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly IClock _clock;
    private readonly Queue<DateTimeOffset> _calls = new();
    private readonly object _gate = new();
    private DateTimeOffset _pausedUntil = DateTimeOffset.MinValue;

    public ApiRateLimiter(IClock clock) => _clock = clock;

    /// <summary>Records a call and returns true if one is allowed right now.</summary>
    public bool TryAcquire(int maxCallsPerMinute)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            if (now < _pausedUntil)
            {
                return false;
            }

            while (_calls.Count > 0 && now - _calls.Peek() >= Window)
            {
                _calls.Dequeue();
            }

            if (_calls.Count >= maxCallsPerMinute)
            {
                return false;
            }

            _calls.Enqueue(now);
            return true;
        }
    }

    /// <summary>Blocks calls for <paramref name="duration"/>; never shortens an existing, longer pause.</summary>
    public void PauseFor(TimeSpan duration)
    {
        lock (_gate)
        {
            var until = _clock.UtcNow + duration;
            if (until > _pausedUntil)
            {
                _pausedUntil = until;
            }
        }
    }
}
