using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Loads plugin assemblies from the plugins folder into the current AppDomain.
/// Extracts .zip archives to a temp directory and loads from disk,
/// preserving <see cref="Assembly.Location"/> so satellite assemblies are found.
/// </summary>
public static class PluginLoader
{
    private static readonly List<Assembly> _loadedAssemblies = [];
    private static string? _pluginsRoot;

    /// <summary>
    /// Assemblies loaded from the plugins directory.
    /// Used by EngineFactory and SettingsService to discover engines.
    /// </summary>
    public static IReadOnlyList<Assembly> LoadedAssemblies => _loadedAssemblies.AsReadOnly();

    public static void LoadPlugins(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return;

        _pluginsRoot = pluginsPath;

        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

        var tempRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zaya", "ScreenTranslator", "temp", "plugins");

        Directory.CreateDirectory(tempRoot);

        // 1. Extract new or updated zips
        foreach (var zip in Directory.EnumerateFiles(pluginsPath, "*.zip"))
        {
            ExtractIfNeeded(zip, tempRoot);
        }

        // 2. Clean stale extracted dirs (no corresponding .zip)
        foreach (var dir in Directory.EnumerateDirectories(tempRoot))
        {
            var dirName = Path.GetFileName(dir);
            var expectedZip = Path.Combine(pluginsPath, dirName + ".zip");
            if (!File.Exists(expectedZip))
            {
                try { Directory.Delete(dir, true); }
                catch { }
            }
        }

        // 3. Load DLLs from extracted dirs — root first, then satellites
        foreach (var zip in Directory.EnumerateFiles(pluginsPath, "*.zip"))
        {
            var zipName = Path.GetFileNameWithoutExtension(zip);
            var extractDir = Path.Combine(tempRoot, zipName);

            if (!Directory.Exists(extractDir))
                continue;

            // Main assemblies first (root level)
            foreach (var dll in Directory.EnumerateFiles(extractDir, "*.dll", SearchOption.TopDirectoryOnly))
            {
                try { _loadedAssemblies.Add(Assembly.LoadFrom(dll)); }
                catch { }
            }

            // Satellite assemblies second (subdirectories)
            foreach (var dll in Directory.EnumerateFiles(extractDir, "*.dll", SearchOption.AllDirectories))
            {
                if (Path.GetDirectoryName(dll) == extractDir)
                    continue; // already loaded above

                try { _loadedAssemblies.Add(Assembly.LoadFrom(dll)); }
                catch { }
            }
        }

        // 4. Load loose DLLs from pluginsPath
        foreach (var dll in Directory.EnumerateFiles(pluginsPath, "*.dll"))
        {
            try { _loadedAssemblies.Add(Assembly.LoadFrom(dll)); }
            catch { }
        }
    }

    private static void ExtractIfNeeded(string zipPath, string tempRoot)
    {
        var zipName = Path.GetFileNameWithoutExtension(zipPath);
        var extractDir = Path.Combine(tempRoot, zipName);

        if (Directory.Exists(extractDir) &&
            Directory.GetCreationTime(extractDir) >= File.GetLastWriteTime(zipPath))
        {
            return; // already extracted and up-to-date
        }

        try
        {
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);
        }
        catch
        {
            return; // locked files, skip this plugin
        }

        try
        {
            ZipFile.ExtractToDirectory(zipPath, extractDir);
        }
        catch { }
    }

    public static PluginManifest? ReadManifest(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PluginManifest>(json);
        }
        catch
        {
            return null;
        }
    }

    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name).Name;
        if (assemblyName is null || _pluginsRoot is null)
            return null;

        // Search lib\ subfolder (app dependencies moved there)
        var libPath = Path.Combine(_pluginsRoot, "..", "lib", assemblyName + ".dll");
        if (File.Exists(libPath))
        {
            try { return Assembly.LoadFrom(libPath); }
            catch { }
        }

        // Search loose DLLs in the root plugins folder
        var path = Path.Combine(_pluginsRoot, assemblyName + ".dll");
        if (File.Exists(path))
        {
            try { return Assembly.LoadFrom(path); }
            catch { }
        }

        return null;
    }
}

public sealed class PluginManifest
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Interface { get; set; } = string.Empty;
    public string InterfaceVersion { get; set; } = string.Empty;
    public string PluginVersion { get; set; } = string.Empty;
}
