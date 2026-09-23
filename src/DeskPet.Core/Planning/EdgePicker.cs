using DeskPet.Core.Models;

namespace DeskPet.Core.Planning;

/// <summary>Chooses which monitor edge a window should be hauled off, so the drag is as short as possible.</summary>
public static class EdgePicker
{
    public static ScreenEdge Nearest(ScreenRect window, ScreenRect monitor)
    {
        var x = window.CenterX;
        var y = window.CenterY;
        var edge = ScreenEdge.Left;
        var best = x - monitor.Left;

        // Strict comparisons: on ties the earlier (horizontal) edge wins, since sideways drags read better.
        Consider(ScreenEdge.Right, monitor.Right - x, ref edge, ref best);
        Consider(ScreenEdge.Top, y - monitor.Top, ref edge, ref best);
        Consider(ScreenEdge.Bottom, monitor.Bottom - y, ref edge, ref best);
        return edge;
    }

    private static void Consider(ScreenEdge candidate, int distance, ref ScreenEdge edge, ref int best)
    {
        if (distance < best)
        {
            edge = candidate;
            best = distance;
        }
    }
}
