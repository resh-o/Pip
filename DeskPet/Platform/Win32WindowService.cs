using System.Runtime.InteropServices;
using DeskPet.Core.Abstractions;
using DeskPet.Core.Models;

namespace DeskPet.Platform;

/// <summary>
/// Win32 implementation of <see cref="IWindowService"/>. Every call runs per-monitor-DPI aware so
/// coordinates are physical pixels on all monitors. Queries (Enumerate, GetForeground) run on the
/// sensing thread; everything that changes a window runs on the main thread.
/// </summary>
internal sealed class Win32WindowService : IWindowService
{
    private const uint NoMoveSizeZOrder = NativeMethods.SwpNoMove | NativeMethods.SwpNoSize |
        NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate;

    private readonly WindowReader _reader = new();
    private readonly List<nint> _handles = new(128);
    private WindowInfo? _lastForeground;

    public WindowInfo? GetForeground()
    {
        using var scope = new PhysicalPixelsScope();
        nint hwnd = NativeMethods.GetForegroundWindow();
        // A window minimised without activation stays "foreground", but nothing is really in front.
        if (hwnd == 0 || _reader.IsOwn(hwnd) || _reader.IsShell(hwnd) || !NativeMethods.IsWindowVisible(hwnd) || NativeMethods.IsIconic(hwnd))
            return null;
        return _lastForeground = _reader.Read(hwnd, _lastForeground);
    }

    public unsafe IReadOnlyList<WindowInfo> Enumerate()
    {
        using var scope = new PhysicalPixelsScope();
        _handles.Clear();
        var state = GCHandle.Alloc(_handles);
        try
        {
            NativeMethods.EnumWindows(&Collect, GCHandle.ToIntPtr(state));
        }
        finally
        {
            state.Free();
        }
        var windows = new List<WindowInfo>(_handles.Count);
        foreach (nint hwnd in _handles)
            if (_reader.IsCandidate(hwnd) && _reader.Read(hwnd) is { } info)
                windows.Add(info);
        return windows;
    }

    public bool TryGetBounds(nint handle, out ScreenRect bounds)
    {
        using var scope = new PhysicalPixelsScope();
        return WindowReader.TryFrameBounds(handle, out bounds);
    }

    public ScreenRect WorkAreaOf(nint handle)
    {
        using var scope = new PhysicalPixelsScope();
        return WindowReader.MonitorOf(handle, workArea: true);
    }

    public bool IsUserHandling(nint handle)
    {
        uint thread = NativeMethods.GetWindowThreadProcessId(handle, out _);
        var info = new NativeMethods.GuiThreadInfo { Size = (uint)Marshal.SizeOf<NativeMethods.GuiThreadInfo>() };
        if (thread != 0 && NativeMethods.GetGUIThreadInfo(thread, ref info) && (info.Flags & NativeMethods.GuiInMoveSize) != 0)
            return true;
        return IsMouseButtonDown() && IsCursorOver(handle);
    }

    // A synchronous no-op reposition: UIPI refuses it for windows of higher-integrity processes.
    public bool CanControl(nint handle) =>
        NativeMethods.SetWindowPos(handle, 0, 0, 0, 0, 0, NoMoveSizeZOrder);

    public bool Move(nint handle, int left, int top)
    {
        using var scope = new PhysicalPixelsScope();
        if (!NativeMethods.GetWindowRect(handle, out var outer) || !WindowReader.TryFrameBounds(handle, out var frame))
            return false;
        // SetWindowPos places the outer rect, which includes the invisible resize borders.
        return NativeMethods.SetWindowPos(handle, 0, left - (frame.Left - outer.Left), top - (frame.Top - outer.Top), 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpAsyncWindowPos);
    }

    public bool Minimise(WindowPlacement restoreTo) =>
        Apply(restoreTo.Handle, restoreTo.NormalBounds, NativeMethods.SwShowMinNoActive);

    public bool BringToFront(nint handle)
    {
        // HWND_TOP is ignored for a background process (foreground lock); a topmost round trip
        // raises the window to the top of the normal band without activating it.
        if ((NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle) & NativeMethods.WsExTopmost) != 0)
            return true;
        const uint flags = NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate;
        return NativeMethods.SetWindowPos(handle, NativeMethods.HwndTopmost, 0, 0, 0, 0, flags)
            && NativeMethods.SetWindowPos(handle, NativeMethods.HwndNoTopmost, 0, 0, 0, 0, flags);
    }

    public WindowPlacement? CapturePlacement(nint handle)
    {
        using var scope = new PhysicalPixelsScope();
        var p = NewPlacement();
        if (!NativeMethods.GetWindowPlacement(handle, ref p))
            return null;
        var n = p.NormalPosition;
        return new WindowPlacement(handle, new ScreenRect(n.Left, n.Top, n.Right, n.Bottom),
            NativeMethods.IsZoomed(handle), NativeMethods.IsIconic(handle));
    }

    public bool RestorePlacement(WindowPlacement placement)
    {
        uint show = placement.WasMaximized ? NativeMethods.SwShowMaximized
            : placement.WasMinimized ? NativeMethods.SwShowMinNoActive
            : NativeMethods.SwShowNoActivate;
        return Apply(placement.Handle, placement.NormalBounds, show);
    }

    private static bool Apply(nint handle, ScreenRect normal, uint showCmd)
    {
        using var scope = new PhysicalPixelsScope();
        var p = NewPlacement();
        if (!NativeMethods.GetWindowPlacement(handle, ref p))
            return false;
        p.NormalPosition = new NativeMethods.Rect { Left = normal.Left, Top = normal.Top, Right = normal.Right, Bottom = normal.Bottom };
        p.ShowCmd = showCmd;
        return NativeMethods.SetWindowPlacement(handle, in p);
    }

    private static NativeMethods.WindowPlacement NewPlacement() =>
        new() { Length = (uint)Marshal.SizeOf<NativeMethods.WindowPlacement>() };

    // Physical buttons (swap-agnostic): either one held counts as the user taking hold.
    private static bool IsMouseButtonDown() =>
        (NativeMethods.GetAsyncKeyState(NativeMethods.VkLButton) & 0x8000) != 0
        || (NativeMethods.GetAsyncKeyState(NativeMethods.VkRButton) & 0x8000) != 0;

    private static bool IsCursorOver(nint handle)
    {
        using var scope = new PhysicalPixelsScope();
        NativeMethods.GetCursorPos(out var p);
        return NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(p), NativeMethods.GaRoot) == handle;
    }

    [UnmanagedCallersOnly]
    private static int Collect(nint hwnd, nint state)
    {
        ((List<nint>)GCHandle.FromIntPtr(state).Target!).Add(hwnd);
        return 1;
    }
}
