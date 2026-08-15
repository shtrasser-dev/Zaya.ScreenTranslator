using System.Runtime.InteropServices;
using Avalonia;
using Zaya.ScreenTranslator.Impl.Shared;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        OleInitialize(IntPtr.Zero);

        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new SkiaOptions { UseOpacitySaveLayer = true })
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int OleInitialize(IntPtr reserved);
}
