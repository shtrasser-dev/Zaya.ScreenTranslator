using System.Runtime.InteropServices;

namespace Zaya.ScreenTranslator.Impl.Shared.Native;

internal static class Win32WindowBounds
{
    private const int DwmwaExtendedFrameBounds = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Math.Max(0, Right - Left);
        public int Height => Math.Max(0, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

    public static bool IsValidWindow(IntPtr hWnd)
        => OperatingSystem.IsWindows()
           && hWnd != IntPtr.Zero
           && IsWindow(hWnd);

    public static bool IsMinimized(IntPtr hWnd)
        => IsValidWindow(hWnd) && IsIconic(hWnd);

    public static bool TryGetClientSize(IntPtr hWnd, out int width, out int height)
    {
        width = height = 0;
        if (!IsValidWindow(hWnd))
            return false;
        if (!GetClientRect(hWnd, out var rect))
            return false;

        width = rect.Width;
        height = rect.Height;
        return width > 0 && height > 0;
    }

    /// <summary>
    /// Metrics to map a full-window capture (WGC) onto the client area used by the overlay.
    /// Outer bounds prefer DWM extended frame (what WGC typically matches), else <c>GetWindowRect</c>.
    /// </summary>
    public static bool TryGetClientAlignMetrics(
        IntPtr hWnd,
        out int outerWidth,
        out int outerHeight,
        out int insetX,
        out int insetY,
        out int clientWidth,
        out int clientHeight)
    {
        outerWidth = outerHeight = insetX = insetY = clientWidth = clientHeight = 0;
        if (!IsValidWindow(hWnd))
            return false;
        if (!GetClientRect(hWnd, out var client))
            return false;

        clientWidth = client.Width;
        clientHeight = client.Height;
        if (clientWidth <= 0 || clientHeight <= 0)
            return false;

        if (!TryGetOuterScreenRect(hWnd, out var outer))
            return false;

        outerWidth = outer.Width;
        outerHeight = outer.Height;
        if (outerWidth <= 0 || outerHeight <= 0)
            return false;

        var clientOrigin = new Point { X = 0, Y = 0 };
        if (!ClientToScreen(hWnd, ref clientOrigin))
            return false;

        insetX = clientOrigin.X - outer.Left;
        insetY = clientOrigin.Y - outer.Top;
        return true;
    }

    private static bool TryGetOuterScreenRect(IntPtr hWnd, out Rect outer)
    {
        if (DwmGetWindowAttribute(hWnd, DwmwaExtendedFrameBounds, out outer, Marshal.SizeOf<Rect>()) == 0
            && outer.Width > 0
            && outer.Height > 0)
            return true;

        return GetWindowRect(hWnd, out outer) && outer.Width > 0 && outer.Height > 0;
    }
}
