using System.Runtime.InteropServices;
using Godot;

// M0 spike: transparency, click-through, no taskbar entry, DPI. Deleted after M0.
public partial class Spike : Node2D
{
    [DllImport("user32.dll")] static extern nint GetWindowLongPtrW(nint h, int i);
    [DllImport("user32.dll")] static extern nint SetWindowLongPtrW(nint h, int i, nint v);
    [DllImport("user32.dll")] static extern bool ShowWindow(nint h, int c);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(nint h);
    [DllImport("user32.dll")] static extern nint GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] static extern int GetAwarenessFromDpiAwarenessContext(nint c);
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint h, nint a, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint h, out Rect r);
    [DllImport("user32.dll")] static extern nint WindowFromPoint(Pt p);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] static extern nint GetAncestor(nint h, uint f);

    struct Rect { public int L, T, R, B; }
    struct Pt { public int X, Y; }

    double _t;
    nint _hwnd;
    bool _probed;

    public override void _Ready()
    {
        _hwnd = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
        GD.Print($"hwnd=0x{_hwnd:X} awareness={GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext())} dpi={GetDpiForWindow(_hwnd)} renderer={RenderingServer.GetCurrentRenderingDriverName()}");
        GD.Print($"exstyle before=0x{GetWindowLongPtrW(_hwnd, -20):X}");

        // Hide from taskbar/Alt-Tab: TOOLWINDOW on, APPWINDOW off, applied across a hide/show.
        ShowWindow(_hwnd, 0);
        SetWindowLongPtrW(_hwnd, -20, (GetWindowLongPtrW(_hwnd, -20) | 0x80) & ~(nint)0x40000);
        ShowWindow(_hwnd, 8); // SW_SHOWNA
        GD.Print($"exstyle after=0x{GetWindowLongPtrW(_hwnd, -20):X}");

        var poly = new Vector2[24];
        for (var i = 0; i < poly.Length; i++)
            poly[i] = new Vector2(128, 128) + Vector2.FromAngle(Mathf.Tau * i / poly.Length) * 84;
        GetWindow().MousePassthroughPolygon = poly;
    }

    public override void _Process(double delta)
    {
        _t += delta;
        var x = 200 + (int)(Mathf.Sin((float)_t * 0.8f) * 150);
        SetWindowPos(_hwnd, 0, x, 300, 0, 0, 0x0001 | 0x0004 | 0x0010);
        QueueRedraw();
        if (!_probed && _t > 2) Probe();
    }

    void Probe()
    {
        _probed = true;
        GetWindowRect(_hwnd, out var r);
        nint Hit(int x, int y) => GetAncestor(WindowFromPoint(new Pt { X = x, Y = y }), 2);
        GD.Print($"rect={r.L},{r.T},{r.R},{r.B} screen={GetSystemMetrics(0)}x{GetSystemMetrics(1)} monitors={GetSystemMetrics(80)}");
        GD.Print($"centreHitsSelf={Hit((r.L + r.R) / 2, (r.T + r.B) / 2) == _hwnd} cornerHitsSelf={Hit(r.L + 5, r.T + 5) == _hwnd}");
        GD.Print($"staticMB={OS.GetStaticMemoryUsage() / 1048576} managedMB={System.GC.GetTotalMemory(false) / 1048576} workingSetMB={System.Environment.WorkingSet / 1048576}");
    }

    public override void _Draw()
    {
        DrawCircle(new Vector2(128, 128), 80, new Color(0.35f, 0.8f, 0.6f, 0.9f));
        DrawCircle(new Vector2(128, 128), 40, new Color(1, 1, 1, 0.3f));
    }
}
