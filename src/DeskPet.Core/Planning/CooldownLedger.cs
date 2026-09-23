using DeskPet.Core.Abstractions;

namespace DeskPet.Core.Planning;

/// <summary>Remembers when each window was last moved so the pet doesn't keep bullying the same one.</summary>
public sealed class CooldownLedger
{
    private readonly IClock _clock;
    private readonly ISettings _settings;
    private readonly Dictionary<nint, DateTimeOffset> _lastActed = [];

    public CooldownLedger(IClock clock, ISettings settings)
    {
        _clock = clock;
        _settings = settings;
    }

    internal int TrackedCount => _lastActed.Count;

    private TimeSpan Cooldown => TimeSpan.FromSeconds(_settings.Current.WindowCooldownSeconds);

    public bool IsCoolingDown(nint handle) =>
        _lastActed.TryGetValue(handle, out var at) && _clock.UtcNow - at < Cooldown;

    public void Record(nint handle)
    {
        var now = _clock.UtcNow;
        PruneExpired(now);
        _lastActed[handle] = now;
    }

    // Handles of closed windows are never looked up again, so expired entries would otherwise pile up.
    private void PruneExpired(DateTimeOffset now)
    {
        var cooldown = Cooldown;
        foreach (var (handle, at) in _lastActed)
        {
            if (now - at >= cooldown)
            {
                _lastActed.Remove(handle);
            }
        }
    }
}
