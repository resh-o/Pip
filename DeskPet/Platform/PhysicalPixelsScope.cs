namespace DeskPet.Platform;

/// <summary>
/// Godot makes the process only system-DPI aware, so Win32 coordinates on other-DPI monitors are
/// virtualised. Inside this scope the calling thread is per-monitor-v2 aware and sees raw pixels.
/// </summary>
internal readonly ref struct PhysicalPixelsScope
{
    private readonly nint _previous;

    public PhysicalPixelsScope() =>
        _previous = NativeMethods.SetThreadDpiAwarenessContext(NativeMethods.DpiAwarenessContextPerMonitorV2);

    public void Dispose() => NativeMethods.SetThreadDpiAwarenessContext(_previous);
}
