using System.Runtime.InteropServices;
using DeskPet.Core.Models;

namespace DeskPet.Platform;

/// <summary>Read-only desktop queries in physical pixels.</summary>
internal static class Desktop
{
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

    public static TimeSpan UserIdleTime()
    {
        var info = new NativeMethods.LastInputInfo { Size = (uint)Marshal.SizeOf<NativeMethods.LastInputInfo>() };
        if (!NativeMethods.GetLastInputInfo(ref info))
            return TimeSpan.Zero;
        // Both are 32-bit millisecond tick counts, so unsigned subtraction survives the 49-day wrap.
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time));
    }
}
