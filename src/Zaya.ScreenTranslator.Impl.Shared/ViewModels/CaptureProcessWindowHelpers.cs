using Avalonia.Media.Imaging;
using System.Diagnostics;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

internal static class CaptureProcessWindowHelpers
{
    public static Bitmap? TryLoadIconForProcessName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return null;

        Process[]? processes = null;
        try
        {
            processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                var icon = ProcessIconLoader.GetIcon(process);
                if (icon is not null)
                    return icon;
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (processes is not null)
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }

        return null;
    }

    public static string NormalizeProcessName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            value = value[..^4];
        return value;
    }

    public static bool IsProcessRunning(string processName)
    {
        var name = NormalizeProcessName(processName);
        if (string.IsNullOrEmpty(name))
            return false;

        Process[]? processes = null;
        try
        {
            processes = Process.GetProcessesByName(name);
            return processes.Length > 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (processes is not null)
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }
    }

    public static bool TryFindProcessMainWindow(string processName, out nint handle, out string title)
    {
        handle = 0;
        title = string.Empty;
        Process[]? processes = null;
        try
        {
            processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                try
                {
                    if (process.MainWindowHandle == IntPtr.Zero)
                        continue;
                    if (string.IsNullOrEmpty(process.MainWindowTitle))
                        continue;

                    handle = process.MainWindowHandle;
                    title = process.MainWindowTitle;
                    return true;
                }
                catch
                {
                    // Some processes deny query access — skip them.
                }
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            if (processes is not null)
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }

        return false;
    }
}
