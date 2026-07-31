using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia.Media.Imaging;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>Loads and caches process executable icons for the window picker.</summary>
internal static class ProcessIconLoader
{
    private static readonly ConcurrentDictionary<string, Bitmap?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap? GetIcon(Process process)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        string? path = null;
        try
        {
            path = process.MainModule?.FileName;
        }
        catch
        {
            // Access denied for some system processes.
        }

        return GetIcon(path);
    }

    public static Bitmap? GetIcon(string? executablePath)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(executablePath))
            return null;

        return Cache.GetOrAdd(executablePath, path =>
            OperatingSystem.IsWindows() ? LoadIcon(path) : null);
    }

    [SupportedOSPlatform("windows")]
    private static Bitmap? LoadIcon(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null)
                return null;

            using var gdiBitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            gdiBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
