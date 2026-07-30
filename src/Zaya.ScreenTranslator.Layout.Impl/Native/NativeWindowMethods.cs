using System.Runtime.InteropServices;

namespace Zaya.ScreenTranslator.Layout.Impl.Native;

internal static class NativeWindowMethods
{
    public const int GwlExstyle = -20;
    public const int WsExTransparent = 0x00000020;
    public const int WsExToolwindow = 0x00000080;
    public const int WsExNoactivate = 0x08000000;
    public const int WsExLayered = 0x00080000;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);

    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        => IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32());

    public static bool TryGetClientScreenRect(IntPtr hWnd, out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
            return false;
        if (!GetClientRect(hWnd, out var rect))
            return false;

        var pt = new Point { X = 0, Y = 0 };
        if (!ClientToScreen(hWnd, ref pt))
            return false;

        x = pt.X;
        y = pt.Y;
        width = Math.Max(1, rect.Right - rect.Left);
        height = Math.Max(1, rect.Bottom - rect.Top);
        return true;
    }

    public static void EnableClickThrough(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        var ex = GetWindowLongPtr(hwnd, GwlExstyle).ToInt64();
        ex |= WsExTransparent | WsExToolwindow | WsExNoactivate | WsExLayered;
        SetWindowLongPtr(hwnd, GwlExstyle, new IntPtr(ex));
    }
}
