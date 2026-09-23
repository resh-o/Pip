using DeskPet.Core.Models;

namespace DeskPet.Core.Tests.Fakes;

public static class Windows
{
    public static readonly ScreenRect Monitor = new(0, 0, 1920, 1080);

    public static WindowInfo Make(
        nint handle,
        string process = "app",
        string title = "Some window",
        string className = "AppWindowClass",
        ScreenRect? bounds = null,
        ScreenRect? monitor = null,
        bool maximized = false,
        bool minimized = false) =>
        new(handle, process, title, className, bounds ?? new ScreenRect(100, 100, 900, 700), monitor ?? Monitor, maximized, minimized);

    public static WindowInfo Fullscreen(nint handle, string process = "game", string title = "Game") =>
        Make(handle, process, title, bounds: Monitor);
}
