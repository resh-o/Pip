using DeskPet.Core.Models;

namespace DeskPet.Core.Abstractions;

/// <summary>
/// Everything the pet may do to other windows. Closing windows, killing processes and
/// sending input are intentionally absent so they cannot be done by accident.
/// </summary>
public interface IWindowService
{
    WindowInfo? GetForeground();
    IReadOnlyList<WindowInfo> Enumerate();
    bool TryGetBounds(nint handle, out ScreenRect bounds);
    bool IsUserMovingOrSizing(nint handle);
    bool Move(nint handle, int left, int top);
    bool Minimise(nint handle);
    bool BringToFront(nint handle);
    WindowPlacement? CapturePlacement(nint handle);
    bool RestorePlacement(WindowPlacement placement);
}
