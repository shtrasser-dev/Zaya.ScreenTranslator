using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Zaya.ScreenTranslator.Impl.Shared;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        OleInitialize(IntPtr.Zero);

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var pluginsDir = Path.Combine(appData, "Zaya", "ScreenTranslator", "plugins");
        Directory.CreateDirectory(pluginsDir);

        App.PluginsDirectory = pluginsDir;

        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int OleInitialize(IntPtr reserved);
}
