using System.Runtime.InteropServices;
using DeskPet.Core.Models;

namespace DeskPet.Platform;

/// <summary>Read-only desktop queries in physical pixels.</summary>
internal static class Desktop
{
    private const uint MonitorDefaultToNull = 0;

    public static (int X, int Y) CursorPosition()
    {
        using var scope = new PhysicalPixelsScope();
        NativeMethods.GetCursorPos(out var p);
        return (p.X, p.Y);
    }

    /// <summary>Work area (monitor minus taskbar) of the monitor nearest to a physical point.</summary>
    public static ScreenRect WorkAreaAt(int x, int y)
    {
        using var scope = new PhysicalPixelsScope();
        nint monitor = NativeMethods.MonitorFromPoint(new NativeMethods.Point { X = x, Y = y }, NativeMethods.MonitorDefaultToNearest);
        var info = new NativeMethods.MonitorInfo { Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        NativeMethods.GetMonitorInfo(monitor, ref info);
        var w = info.Work;
        return new ScreenRect(w.Left, w.Top, w.Right, w.Bottom);
    }

    /// <summary>Whether any monitor lies just beyond <paramref name="edge"/> of <paramref name="monitor"/>, along the window's span.</summary>
    public static bool HasMonitorBeyond(ScreenRect monitor, ScreenRect window, ScreenEdge edge)
    {
        using var scope = new PhysicalPixelsScope();
        for (int i = 1; i <= 3; i++)
        {
            var p = edge switch
            {
                ScreenEdge.Left => new NativeMethods.Point { X = monitor.Left - 1, Y = window.Top + window.Height * i / 4 },
                ScreenEdge.Right => new NativeMethods.Point { X = monitor.Right, Y = window.Top + window.Height * i / 4 },
                ScreenEdge.Top => new NativeMethods.Point { X = window.Left + window.Width * i / 4, Y = monitor.Top - 1 },
                _ => new NativeMethods.Point { X = window.Left + window.Width * i / 4, Y = monitor.Bottom },
            };
            if (NativeMethods.MonitorFromPoint(p, MonitorDefaultToNull) != 0)
                return true;
        }
        return false;
    }

    public static TimeSpan UserIdleTime()
    {
        var info = new NativeMethods.LastInputInfo { Size = (uint)Marshal.SizeOf<NativeMethods.LastInputInfo>() };
        if (!NativeMethods.GetLastInputInfo(ref info))
            return TimeSpan.Zero;
        // Both are 32-bit millisecond tick counts, so unsigned subtraction survives the 49-day wrap.
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time));
    }
}
