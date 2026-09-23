using DeskPet.Core.Models;

namespace DeskPet.Core.Abstractions;

/// <summary>
/// Everything the pet may do to other windows. Closing windows, killing processes and sending
/// input are intentionally absent so they cannot be done by accident. All coordinates are
/// physical pixels of the visible frame (without invisible resize borders).
/// </summary>
public interface IWindowService
{
    WindowInfo? GetForeground();
    IReadOnlyList<WindowInfo> Enumerate();
    bool TryGetBounds(nint handle, out ScreenRect bounds);
    ScreenRect WorkAreaOf(nint handle);

    /// <summary>True while the user is moving/sizing the window or holds a mouse button over it.</summary>
    bool IsUserHandling(nint handle);

    /// <summary>False when Windows won't let us reposition the window, e.g. an elevated process.</summary>
    bool CanControl(nint handle);

    bool Move(nint handle, int left, int top);

    /// <summary>Minimises without activating anything, remembering <paramref name="restoreTo"/> as the normal position.</summary>
    bool Minimise(WindowPlacement restoreTo);

    /// <summary>Raises the window above other normal windows without giving it focus.</summary>
    bool BringToFront(nint handle);

    WindowPlacement? CapturePlacement(nint handle);
    bool RestorePlacement(WindowPlacement placement);
}
