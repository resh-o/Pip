namespace DeskPet.Core.Models;

/// <summary>
/// Saved window placement used for Undo. NormalBounds is in the platform's placement
/// coordinate space and must only be round-tripped, never mixed with screen coordinates.
/// </summary>
public sealed record WindowPlacement(nint Handle, ScreenRect NormalBounds, bool WasMaximized, bool WasMinimized);
