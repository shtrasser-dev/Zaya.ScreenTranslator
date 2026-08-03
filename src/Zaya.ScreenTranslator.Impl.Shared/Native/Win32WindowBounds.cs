using System.Runtime.InteropServices;

namespace Zaya.ScreenTranslator.Impl.Shared.Native;

internal static class Win32WindowBounds
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    public static bool IsMinimized(IntPtr hWnd)
        => OperatingSystem.IsWindows()
           && hWnd != IntPtr.Zero
           && IsWindow(hWnd)
           && IsIconic(hWnd);

    public static bool TryGetClientSize(IntPtr hWnd, out int width, out int height)
    {
        width = height = 0;
        if (!OperatingSystem.IsWindows() || hWnd == IntPtr.Zero || !IsWindow(hWnd))
            return false;
        if (!GetClientRect(hWnd, out var rect))
            return false;

        width = Math.Max(0, rect.Right - rect.Left);
        height = Math.Max(0, rect.Bottom - rect.Top);
        return width > 0 && height > 0;
    }
}
