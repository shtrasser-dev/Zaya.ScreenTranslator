using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.OCR.Services;
using Zaya.Screenshot.Services;
using Zaya.Translator.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Loads plugin assemblies from the plugins folder into the current AppDomain.
/// Extracts .zip archives to a temp directory and loads from disk,
/// preserving <see cref="Assembly.Location"/> so satellite assemblies are found.
/// </summary>
public static class PluginLoader
{
    private static readonly List<Assembly> _loadedAssemblies = [];
    private static readonly HashSet<string> _loadedNames = new(StringComparer.OrdinalIgnoreCase);
    private static string? _pluginsRoot;

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Assemblies loaded from the plugins directory.
    /// Used by EngineFactory and SettingsService to discover engines.
    /// </summary>
    public static IReadOnlyList<Assembly> LoadedAssemblies => _loadedAssemblies.AsReadOnly();

    public static string ExtractRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Zaya", "ScreenTranslator", "temp", "plugins");

    public static void LoadPlugins(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return;

        _pluginsRoot = pluginsPath;

        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

        Directory.CreateDirectory(ExtractRoot);

        // 1. Extract new or updated zips (re-extract when stamp or zip contents diverge)
        foreach (var zip in Directory.EnumerateFiles(pluginsPath, "*.zip"))
        {
            PluginExtractCache.ExtractIfNeeded(zip, ExtractRoot);
        }

        // 2. Clean stale extracted dirs (no corresponding .zip)
        foreach (var dir in Directory.EnumerateDirectories(ExtractRoot))
        {
            var dirName = Path.GetFileName(dir);
            var expectedZip = Path.Combine(pluginsPath, dirName + ".zip");
            if (!File.Exists(expectedZip))
                PluginExtractCache.TryDeleteDirectory(dir);
        }

        // 3. Load DLLs from extracted dirs — root first, then satellites
        foreach (var zip in Directory.EnumerateFiles(pluginsPath, "*.zip"))
        {
            var zipName = Path.GetFileNameWithoutExtension(zip);
            var extractDir = Path.Combine(ExtractRoot, zipName);

            if (!Directory.Exists(extractDir))
                continue;

            var manifest = ReadManifest(Path.Combine(extractDir, PluginConstants.ManifestFileName));
            if (manifest is not null && !IsInterfaceCompatible(manifest))
            {
                Debug.WriteLine(
                    $"[PluginLoader] Skipping {zipName}: interface {manifest.Interface} " +
                    $"{manifest.InterfaceVersion} incompatible with host.");
                continue;
            }

            foreach (var dll in Directory.EnumerateFiles(extractDir, "*.dll", SearchOption.TopDirectoryOnly))
                TryLoad(dll);

            foreach (var dll in Directory.EnumerateFiles(extractDir, "*.dll", SearchOption.AllDirectories))
            {
                if (Path.GetDirectoryName(dll) == extractDir)
                    continue;

                TryLoad(dll);
            }
        }

        // 4. Load loose DLLs from pluginsPath
        foreach (var dll in Directory.EnumerateFiles(pluginsPath, "*.dll"))
            TryLoad(dll);
    }

    /// <summary>
    /// plugin.json interfaceVersion must match the three-part Version of the host-shipped abstractions assembly.
    /// </summary>
    private static bool IsInterfaceCompatible(PluginManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.InterfaceVersion))
            return true; // legacy plugins without the field

        if (!Version.TryParse(manifest.InterfaceVersion, out var required))
            return true;

        var hostAsm = ResolveHostInterfaceAssembly(manifest.Interface);
        if (hostAsm is null)
            return true;

        var hostVer = hostAsm.GetName().Version;
        if (hostVer is null)
            return true;

        var hostThree = new Version(hostVer.Major, hostVer.Minor, Math.Max(hostVer.Build, 0));
        var requiredThree = new Version(required.Major, required.Minor, Math.Max(required.Build, 0));
        return hostThree == requiredThree;
    }

    private static Assembly? ResolveHostInterfaceAssembly(string interfaceName) => interfaceName switch
    {
        "Zaya.OCR" => typeof(IOCRService).Assembly,
        "Zaya.Translator" => typeof(ITranslatorService).Assembly,
        "Zaya.Screenshot" => typeof(ICaptureService).Assembly,
        _ => null,
    };

    private static void TryLoad(string dll)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(dll);
            if (!_loadedNames.Add(name))
                return;

            _loadedAssemblies.Add(Assembly.LoadFrom(dll));
        }
        catch
        {
            _loadedNames.Remove(Path.GetFileNameWithoutExtension(dll));
        }
    }

    public static PluginManifest? ReadManifest(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PluginManifest>(json, ManifestJsonOptions);
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

        var libPath = Path.Combine(_pluginsRoot, "..", "lib", assemblyName + ".dll");
        if (File.Exists(libPath))
        {
            try { return Assembly.LoadFrom(libPath); }
            catch { /* ignore */ }
        }

        var path = Path.Combine(_pluginsRoot, assemblyName + ".dll");
        if (File.Exists(path))
        {
            try { return Assembly.LoadFrom(path); }
            catch { /* ignore */ }
        }

        return null;
    }
}
