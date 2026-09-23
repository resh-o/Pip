using Godot;

namespace DeskPet.Platform;

/// <summary>The pet's own native window: taskbar hiding, DPI scale and physical-pixel placement.</summary>
internal sealed class OwnWindow
{
    private readonly nint _hwnd;

    public nint Handle => _hwnd;

    public OwnWindow() =>
        _hwnd = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);

    /// <summary>DPI scale Godot renders at. The process is system-DPI aware, so this is the system DPI.</summary>
    public float SystemScale
    {
        get
        {
            uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
            return dpi == 0 ? 1f : dpi / 96f;
        }
    }

    /// <summary>Actual width in physical pixels; DWM rescales the window on monitors of other DPI.</summary>
    public int PhysicalWidth
    {
        get
        {
            using var scope = new PhysicalPixelsScope();
            NativeMethods.GetWindowRect(_hwnd, out var r);
            return r.Right - r.Left;
        }
    }

    /// <summary>
    /// Godot marks its window WS_EX_APPWINDOW, which forces a taskbar button even for tool windows,
    /// so that bit is cleared too. Style changes only reach the taskbar across a hide/show.
    /// </summary>
    public void HideFromTaskbar()
    {
        nint style = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GwlExStyle);
        nint wanted = (style | NativeMethods.WsExToolWindow) & ~NativeMethods.WsExAppWindow;
        if (wanted == style)
            return;
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SwHide);
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GwlExStyle, wanted);
        NativeMethods.SetWindowPos(_hwnd, 0, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder |
            NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SwShowNa);
    }

    /// <summary>Moves the window's top-left corner to a physical screen position.</summary>
    public void MoveTo(int left, int top)
    {
        using var scope = new PhysicalPixelsScope();
        NativeMethods.SetWindowPos(_hwnd, 0, left, top, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }
}
