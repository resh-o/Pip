using Godot;

namespace DeskPet.Platform;

/// <summary>
/// Logs the pet window's render and visibility state at startup, then again whenever any of it
/// changes or rendering stalls, so an invisible pet can be explained from the log afterwards.
/// </summary>
internal sealed partial class OwnWindowWatch : Node
{
    private const double CheckSeconds = 2.0;

    private OwnWindow _window = null!;
    private Snapshot _last;
    private int _lastFrames;
    private double _checkIn;

    internal void Init(OwnWindow window)
    {
        _window = window;
        LogStartup();
    }

    public override void _Process(double delta)
    {
        _checkIn -= delta;
        if (_checkIn > 0)
            return;
        _checkIn = CheckSeconds;
        int frames = Engine.GetFramesDrawn();
        if (frames == _lastFrames)
            DiagnosticLog.Write("watch: no frames drawn in the last check interval");
        _lastFrames = frames;
        var now = Snapshot.Take(_window.Handle);
        if (now != _last)
            DiagnosticLog.Write("watch: " + now);
        _last = now;
    }

    private void LogStartup()
    {
        var root = GetWindow();
        DiagnosticLog.Write($"start: godot={Engine.GetVersionInfo()["string"]} renderer={RenderingServer.GetCurrentRenderingDriverName()}/{RenderingServer.GetCurrentRenderingMethod()} adapter={RenderingServer.GetVideoAdapterName()}");
        DiagnosticLog.Write($"start: perPixelAllowed={ProjectSettings.GetSetting("display/window/per_pixel_transparency/allowed")} " +
            $"flagTransparent={DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.Transparent)} transparentBg={root.TransparentBg} " +
            $"borderless={root.Borderless} alwaysOnTop={root.AlwaysOnTop} size={root.Size} mode={root.Mode} " +
            $"systemScale={_window.SystemScale} passthroughPoints={root.MousePassthroughPolygon.Length}");
        _last = Snapshot.Take(_window.Handle);
        DiagnosticLog.Write("start: " + _last);
    }

    private readonly record struct Snapshot(
        nint Hwnd, bool Visible, bool Minimised, int Cloaked, long ExStyle,
        NativeMethods.Rect Rect, int RegionType, NativeMethods.Rect RegionBox)
    {
        public static Snapshot Take(nint hwnd)
        {
            using var scope = new PhysicalPixelsScope();
            NativeMethods.GetWindowRect(hwnd, out var rect);
            NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DwmwaCloaked, out int cloaked, sizeof(int));
            int region = NativeMethods.GetWindowRgnBox(hwnd, out var box);
            return new Snapshot(hwnd, NativeMethods.IsWindowVisible(hwnd), NativeMethods.IsIconic(hwnd), cloaked,
                NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle), rect, region, box);
        }

        public override string ToString() =>
            $"hwnd=0x{Hwnd:X} visible={Visible} minimised={Minimised} cloaked={Cloaked} ex=0x{ExStyle:X} " +
            $"rect={Rect.Left},{Rect.Top},{Rect.Right},{Rect.Bottom} region={RegionName(RegionType)}:{RegionBox.Left},{RegionBox.Top},{RegionBox.Right},{RegionBox.Bottom}";

        private static string RegionName(int type) => type switch
        {
            1 => "EMPTY",
            2 => "simple",
            3 => "complex",
            _ => "none",
        };
    }
}
