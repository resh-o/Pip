using System.Runtime.InteropServices;

namespace DeskPet.Platform;

internal static partial class NativeMethods
{
    public const int DwmwaExtendedFrameBounds = 9;
    public const int DwmwaCloaked = 14;

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmGetWindowAttribute(nint hwnd, int attribute, out Rect value, int size);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
}
