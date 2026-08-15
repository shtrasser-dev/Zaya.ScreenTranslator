using System.IO.Compression;
using Zaya.Logging.Models;
using Zaya.ScreenTranslator.Impl.Shared.Exceptions;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// Keeps <c>temp/plugins/{zipName}</c> in sync with the corresponding plugin zip.
/// </summary>
public sealed class PluginExtractCache : IPluginExtractCache
{
    public const string StampFileName = ".zip-stamp";

    public void ExtractIfNeeded(string zipPath, string tempRoot)
    {
        var extractDir = Path.Combine(tempRoot, Path.GetFileNameWithoutExtension(zipPath));

        if (IsExtractUpToDate(zipPath, extractDir))
            return;

        var stagingDir = extractDir + ".staging";
        TryDeleteDirectory(stagingDir);

        try
        {
            Directory.CreateDirectory(stagingDir);
            ZipFile.ExtractToDirectory(zipPath, stagingDir);
            File.WriteAllText(Path.Combine(stagingDir, StampFileName), MakeStamp(zipPath));

            if (!TryDeleteDirectory(extractDir))
            {
                CopyExtractOver(stagingDir, extractDir);
                TryDeleteDirectory(stagingDir);
                return;
            }

            Directory.Move(stagingDir, extractDir);
        }
        catch (Exception ex)
        {
            TryDeleteDirectory(stagingDir);
            if (ex is PluginExtractException)
                throw;
            throw new PluginExtractException(PluginExtractReason.ExtractFailed, ex);
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
            catch (Exception)
            {
            }
        }

        try
        {
            File.WriteAllText(
                Path.Combine(destDir, StampFileName),
                File.ReadAllText(Path.Combine(sourceDir, StampFileName)));
        }
        catch (Exception)
        {
        }
    }

    public bool IsExtractUpToDate(string zipPath, string extractDir)
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

    public string MakeStamp(string zipPath)
    {
        var info = new FileInfo(zipPath);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    public bool ZipContentsMatchExtract(string zipPath, string extractDir)
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

    [Log(LogLevel.Debug, LogParameters = true)]
    public void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); }
                catch { /* best effort */ }
            }

            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            throw new PluginExtractException(PluginExtractReason.DeleteFailed, ex);
        }
    }

    private bool TryDeleteDirectory(string path)
    {
        try
        {
            DeleteDirectory(path);
            return true;
        }
        catch (PluginExtractException)
        {
            return false;
        }
    }

    private static bool IsDirectoryEntry(ZipArchiveEntry entry) =>
        string.IsNullOrEmpty(entry.Name)
        || entry.FullName.EndsWith('/')
        || entry.FullName.EndsWith('\\');
}
