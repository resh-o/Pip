using DeskPet.Core.Abstractions;
using DeskPet.Core.Models;

namespace DeskPet.Core.Sensing;

/// <summary>
/// Turns the raw ~4 Hz foreground poll into "judge now" / "enumerate now" decisions, so the brain
/// only sees windows the user actually settled on rather than every alt-tab flicker.
/// </summary>
public sealed class ForegroundDebouncer
{
    private readonly IClock _clock;
    private readonly ISettings _settings;

    private nint _handle;
    private string? _title;
    private DateTimeOffset _dwellStart;
    private bool _judged;
    private DateTimeOffset _lastEnumerate;

    public ForegroundDebouncer(IClock clock, ISettings settings)
    {
        _clock = clock;
        _settings = settings;
        // Counting from construction avoids a burst on startup; the first dwell enumerates anyway.
        _lastEnumerate = clock.UtcNow;
    }

    public DebounceResult Observe(WindowInfo? foreground)
    {
        var now = _clock.UtcNow;
        var result = TrackDwell(foreground, now);
        var enumerateDue = now - _lastEnumerate >= TimeSpan.FromSeconds(_settings.Current.EnumerateSeconds);
        if (result == DebounceResult.Judge || enumerateDue)
        {
            result |= DebounceResult.Enumerate;
            _lastEnumerate = now;
        }

        return result;
    }

    private DebounceResult TrackDwell(WindowInfo? foreground, DateTimeOffset now)
    {
        if (foreground is null)
        {
            Reset();
            return DebounceResult.None;
        }

        if (!IsCurrent(foreground))
        {
            StartDwell(foreground, now);
        }

        if (_judged || now - _dwellStart < TimeSpan.FromSeconds(_settings.Current.DwellSeconds))
        {
            return DebounceResult.None;
        }

        _judged = true;
        return DebounceResult.Judge;
    }

    // Title is part of identity: a browser tab switch keeps the handle but changes what the user is doing.
    private bool IsCurrent(WindowInfo foreground) =>
        _title is not null && foreground.Handle == _handle && string.Equals(foreground.Title, _title, StringComparison.Ordinal);

    private void StartDwell(WindowInfo foreground, DateTimeOffset now)
    {
        _handle = foreground.Handle;
        _title = foreground.Title;
        _dwellStart = now;
        _judged = false;
    }

    private void Reset()
    {
        _handle = 0;
        _title = null;
        _judged = false;
    }
}
