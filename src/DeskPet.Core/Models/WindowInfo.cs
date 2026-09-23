namespace DeskPet.Core.Models;

/// <summary>Snapshot of a top-level window, already filtered to "real" app windows by the platform layer.</summary>
public sealed record WindowInfo(
    nint Handle,
    string ProcessName,
    string Title,
    string ClassName,
    ScreenRect Bounds,
    ScreenRect MonitorBounds,
    bool IsMaximized,
    bool IsMinimized)
{
    // Maximised windows stop at the work area, so covering the whole monitor means true fullscreen.
    public bool IsFullscreen => !IsMinimized && Bounds.Covers(MonitorBounds);
}
