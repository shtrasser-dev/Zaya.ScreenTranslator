using System.Reflection;
using Zaya.Logging.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

public sealed class PluginAssemblyLoader : IPluginAssemblyLoader
{
    private readonly IConfigurationPathService _configurationPathService;
    private readonly List<Assembly> _loadedAssemblies = [];
    private readonly HashSet<string> _loadedNames = new(StringComparer.OrdinalIgnoreCase);
    private bool _resolveHooked;

    public PluginAssemblyLoader(IConfigurationPathService configurationPathService)
    {
        _configurationPathService = configurationPathService;
    }

    public IReadOnlyList<Assembly> LoadedAssemblies => _loadedAssemblies.AsReadOnly();

    [Log(LogLevel.Debug, LogParameters = true)]
    public void Process()
    {
        if (_resolveHooked)
            return;

        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        _resolveHooked = true;
    }

    [Log(LogLevel.Debug, LogParameters = true)]
    public void TryLoad(string dllPath)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(dllPath);
            if (!_loadedNames.Add(name))
                return;

            _loadedAssemblies.Add(Assembly.LoadFrom(dllPath));
        }
        catch
        {
            _loadedNames.Remove(Path.GetFileNameWithoutExtension(dllPath));
        }
    }

    private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name).Name;
        if (assemblyName is null)
            return null;

        var libPath = Path.Combine(_configurationPathService.GetLibDirectory(), assemblyName + ".dll");
        if (File.Exists(libPath))
        {
            try { return Assembly.LoadFrom(libPath); }
            catch { /* ignore */ }
        }

        var pluginsPath = Path.Combine(_configurationPathService.GetPluginsDirectory(), assemblyName + ".dll");
        if (File.Exists(pluginsPath))
        {
            try { return Assembly.LoadFrom(pluginsPath); }
            catch { /* ignore */ }
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(asm.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                return asm;
        }

        return null;
    }
}
