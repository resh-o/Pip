using System.Runtime.InteropServices;
using DeskPet.Core.Models;

namespace DeskPet.Platform;

/// <summary>Turns window handles into <see cref="WindowInfo"/>, filtering out everything that isn't a real app window.</summary>
internal sealed class WindowReader
{
    private const int TextCapacity = 256;
    private static readonly string[] ShellClasses = ["Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW"];

    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private readonly ProcessNames _names = new();

    /// <summary>
    /// Alt-Tab-style eligibility: visible, not minimised or cloaked, an unowned non-tool window
    /// (or explicitly an app window), titled, non-empty, not the shell and not ours.
    /// </summary>
    public unsafe bool IsCandidate(nint hwnd)
    {
        if (!NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd) || IsCloaked(hwnd))
            return false;
        nint ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle);
        bool appWindow = (ex & NativeMethods.WsExAppWindow) != 0;
        bool hiddenKind = (ex & (NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate)) != 0
            || NativeMethods.GetWindow(hwnd, NativeMethods.GwOwner) != 0;
        if ((hiddenKind && !appWindow) || IsOwn(hwnd) || IsShell(hwnd))
            return false;
        char* probe = stackalloc char[2];
        return NativeMethods.GetWindowText(hwnd, probe, 2) > 0 && TryFrameBounds(hwnd, out var b) && !b.IsEmpty;
    }

    /// <summary>Reads a window, returning <paramref name="previous"/> itself when nothing changed (no allocation).</summary>
    public unsafe WindowInfo? Read(nint hwnd, WindowInfo? previous = null)
    {
        if (!TryFrameBounds(hwnd, out var bounds))
            return null;
        char* text = stackalloc char[TextCapacity];
        var title = new ReadOnlySpan<char>(text, NativeMethods.GetWindowText(hwnd, text, TextCapacity));
        bool maximised = NativeMethods.IsZoomed(hwnd);
        if (previous is not null && previous.Handle == hwnd && previous.Bounds == bounds
            && previous.IsMaximized == maximised && title.SequenceEqual(previous.Title))
            return previous;

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        return new WindowInfo(hwnd, _names.Of(pid), title.ToString(), ClassOf(hwnd), bounds,
            MonitorOf(hwnd), maximised, NativeMethods.IsIconic(hwnd));
    }

    public bool IsOwn(nint hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == _ownProcessId;
    }

    public unsafe bool IsShell(nint hwnd)
    {
        char* buffer = stackalloc char[TextCapacity];
        var cls = new ReadOnlySpan<char>(buffer, NativeMethods.GetClassName(hwnd, buffer, TextCapacity));
        foreach (var shell in ShellClasses)
            if (cls.SequenceEqual(shell))
                return true;
        return false;
    }

    /// <summary>Visible frame, excluding the invisible resize borders GetWindowRect includes.</summary>
    public static bool TryFrameBounds(nint hwnd, out ScreenRect bounds)
    {
        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DwmwaExtendedFrameBounds, out NativeMethods.Rect r, Marshal.SizeOf<NativeMethods.Rect>()) != 0
            && !NativeMethods.GetWindowRect(hwnd, out r))
        {
            bounds = default;
            return false;
        }
        bounds = new ScreenRect(r.Left, r.Top, r.Right, r.Bottom);
        return true;
    }

    public static ScreenRect MonitorOf(nint hwnd, bool workArea = false)
    {
        nint monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        var info = new NativeMethods.MonitorInfo { Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        NativeMethods.GetMonitorInfo(monitor, ref info);
        var m = workArea ? info.Work : info.Monitor;
        return new ScreenRect(m.Left, m.Top, m.Right, m.Bottom);
    }

    private static bool IsCloaked(nint hwnd) =>
        NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    private static unsafe string ClassOf(nint hwnd)
    {
        char* buffer = stackalloc char[TextCapacity];
        return new string(buffer, 0, NativeMethods.GetClassName(hwnd, buffer, TextCapacity));
    }
}
