using DeskPet.Core.Models;

namespace DeskPet.Core.Planning;

/// <summary>Where a dragged window should end up, and where the pet takes hold of it.</summary>
public static class DragTargets
{
    /// <summary>Share of the window pushed past the edge before it is minimised.</summary>
    public const double HaulFraction = 0.8;

    /// <summary>How far in from a title bar's end the pet grips when pulling sideways.</summary>
    public const int GripInset = 70;

    public static ScreenRect HaulDestination(ScreenRect window, ScreenRect monitor, ScreenEdge edge)
    {
        int pastX = (int)(window.Width * HaulFraction);
        int pastY = (int)(window.Height * HaulFraction);
        return edge switch
        {
            ScreenEdge.Left => MoveTo(window, monitor.Left - pastX, window.Top),
            ScreenEdge.Right => MoveTo(window, monitor.Right - (window.Width - pastX), window.Top),
            ScreenEdge.Top => MoveTo(window, window.Left, monitor.Top - pastY),
            _ => MoveTo(window, window.Left, monitor.Bottom - (window.Height - pastY)),
        };
    }

    /// <summary>Centres the window in the work area; oversized windows align to its top-left.</summary>
    public static ScreenRect CentreIn(ScreenRect window, ScreenRect workArea)
    {
        int left = Math.Max(workArea.Left, workArea.CenterX - window.Width / 2);
        int top = Math.Max(workArea.Top, workArea.CenterY - window.Height / 2);
        return MoveTo(window, left, top);
    }

    /// <summary>
    /// Offset from the window's top-left to the pet's grip on the top edge: the end it pulls
    /// towards when hauling sideways, otherwise the middle.
    /// </summary>
    public static (int X, int Y) GripOffset(ScreenRect window, ScreenEdge? haulEdge)
    {
        int inset = Math.Min(GripInset, window.Width / 4);
        int x = haulEdge switch
        {
            ScreenEdge.Left => inset,
            ScreenEdge.Right => window.Width - inset,
            _ => window.Width / 2,
        };
        return (x, 0);
    }

    private static ScreenRect MoveTo(ScreenRect r, int left, int top) => r.Offset(left - r.Left, top - r.Top);
}
