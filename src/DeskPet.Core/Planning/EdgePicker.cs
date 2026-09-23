using DeskPet.Core.Models;

namespace DeskPet.Core.Planning;

/// <summary>Chooses which monitor edge a window should be hauled off, so the drag is as short as possible.</summary>
public static class EdgePicker
{
    // Order matters: on ties the earlier (horizontal) edge wins, since sideways drags read better.
    private static readonly ScreenEdge[] Edges = [ScreenEdge.Left, ScreenEdge.Right, ScreenEdge.Top, ScreenEdge.Bottom];

    public static ScreenEdge Nearest(ScreenRect window, ScreenRect monitor) =>
        NearestOpen(window, monitor, static _ => true)!.Value;

    /// <summary>
    /// Nearest edge for which <paramref name="isOpen"/> holds, or null when none does. Used to avoid
    /// dragging a window onto a neighbouring monitor, where Windows rescales and repositions it.
    /// </summary>
    public static ScreenEdge? NearestOpen(ScreenRect window, ScreenRect monitor, Func<ScreenEdge, bool> isOpen)
    {
        ScreenEdge? best = null;
        int bestDistance = int.MaxValue;
        foreach (var edge in Edges)
        {
            int distance = Distance(window, monitor, edge);
            if (distance < bestDistance && isOpen(edge))
            {
                best = edge;
                bestDistance = distance;
            }
        }
        return best;
    }

    private static int Distance(ScreenRect window, ScreenRect monitor, ScreenEdge edge) => edge switch
    {
        ScreenEdge.Left => window.CenterX - monitor.Left,
        ScreenEdge.Right => monitor.Right - window.CenterX,
        ScreenEdge.Top => window.CenterY - monitor.Top,
        _ => monitor.Bottom - window.CenterY,
    };
}
