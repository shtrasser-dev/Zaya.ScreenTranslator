using System.Diagnostics;
using System.IO.Compression;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Keeps <c>temp/plugins/{zipName}</c> in sync with the corresponding plugin zip.
/// </summary>
internal static class PluginExtractCache
{
    public const string StampFileName = ".zip-stamp";

    public static void ExtractIfNeeded(string zipPath, string tempRoot)
    {
        var zipName = Path.GetFileNameWithoutExtension(zipPath);
        var extractDir = Path.Combine(tempRoot, zipName);

        if (IsExtractUpToDate(zipPath, extractDir))
            return;

        // Prefer a side-by-side extract so a locked stale folder cannot block refresh.
        var stagingDir = extractDir + ".staging";
        TryDeleteDirectory(stagingDir);

        try
        {
            Directory.CreateDirectory(stagingDir);
            ZipFile.ExtractToDirectory(zipPath, stagingDir);
            File.WriteAllText(Path.Combine(stagingDir, StampFileName), MakeStamp(zipPath));

            if (!TryDeleteDirectory(extractDir))
            {
                // Stale folder still locked — keep staging as the live extract path via junction swap:
                // fall back to copying unlocked files over the stale tree.
                Debug.WriteLine(
                    $"[PluginLoader] Could not replace extract for {zipName}; copying into existing folder.");
                CopyExtractOver(stagingDir, extractDir);
                TryDeleteDirectory(stagingDir);
                return;
            }

            Directory.Move(stagingDir, extractDir);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginLoader] Extract failed for {zipName}: {ex.Message}");
            TryDeleteDirectory(stagingDir);
        }
    }

    private static void CopyExtractOver(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            try
            {
                File.Copy(file, dest, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginLoader] Could not refresh '{relative}': {ex.Message}");
            }
        }

        try
        {
            File.WriteAllText(
                Path.Combine(destDir, StampFileName),
                File.ReadAllText(Path.Combine(sourceDir, StampFileName)));
        }
        catch
        {
            /* ignore */
        }
    }

    public static bool IsExtractUpToDate(string zipPath, string extractDir)
    {
        if (!Directory.Exists(extractDir))
            return false;

        var stampPath = Path.Combine(extractDir, StampFileName);
        if (!File.Exists(stampPath))
            return false;

        try
        {
            if (!string.Equals(File.ReadAllText(stampPath).Trim(), MakeStamp(zipPath), StringComparison.Ordinal))
                return false;

            return ZipContentsMatchExtract(zipPath, extractDir);
        }
        catch
        {
            return false;
        }
    }

    public static string MakeStamp(string zipPath)
    {
        var info = new FileInfo(zipPath);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    /// <summary>
    /// Every zip entry must exist under <paramref name="extractDir"/> with the same length.
    /// Ignores the local stamp file.
    /// </summary>
    public static bool ZipContentsMatchExtract(string zipPath, string extractDir)
    {
        var extractFull = Path.GetFullPath(extractDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            if (IsDirectoryEntry(entry))
                continue;

            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var dest = Path.GetFullPath(Path.Combine(extractDir, relative));
            if (!dest.StartsWith(extractFull, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(Path.GetFileName(dest), StampFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!File.Exists(dest))
                return false;

            if (new FileInfo(dest).Length != entry.Length)
                return false;
        }

        return true;
    }

    public static bool TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return true;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); }
                catch { /* best effort */ }
            }

            Directory.Delete(path, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginLoader] Delete failed for {path}: {ex.Message}");
            return false;
        }
    }

    private static bool IsDirectoryEntry(ZipArchiveEntry entry) =>
        string.IsNullOrEmpty(entry.Name)
        || entry.FullName.EndsWith('/')
        || entry.FullName.EndsWith('\\');
}
