using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia.Media.Imaging;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>Loads and caches process executable icons for the window picker.</summary>
public sealed class ProcessIconLoader : IProcessIconLoader
{
    private readonly ConcurrentDictionary<string, Bitmap?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public Bitmap? GetIcon(Process process)
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

    public Bitmap? GetIcon(string? executablePath)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(executablePath))
            return null;

        return _cache.GetOrAdd(executablePath, path =>
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
