using System.Reflection;
using Zaya.OCR.Services;
using Zaya.Screenshot.Services;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Layout.Services;
using Zaya.Translator.Services;
using Zaya.TranslatorCache.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

/// <summary>
/// Maps plugin interface names to host-shipped NuGet assemblies and update channels.
/// </summary>
public sealed class PluginHostCompatibility : IPluginHostCompatibility
{
    public Assembly? ResolveHostInterfaceAssembly(string interfaceName) => interfaceName switch
    {
        "Zaya.OCR" => typeof(IOCRService).Assembly,
        "Zaya.Translator" => typeof(ITranslatorService).Assembly,
        "Zaya.TranslatorCache" => typeof(ITranslatorCacheService).Assembly,
        "Zaya.Screenshot" => typeof(ICaptureService).Assembly,
        "Zaya.ScreenTranslator.Layout" => typeof(IOverlayLayoutService).Assembly,
        _ => null,
    };

    public string? ResolveUpdateChannel(string interfaceName)
    {
        var asm = ResolveHostInterfaceAssembly(interfaceName);
        var ver = asm?.GetName().Version;
        if (ver is null)
            return null;
        return $"{ver.Major}.{ver.Minor}";
    }

    public string? ResolveUpdateChannel(BuiltinPluginEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Interface))
            return ResolveUpdateChannel(entry.Interface);
        return null;
    }

    public bool IsInterfaceCompatible(PluginManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.InterfaceVersion))
            return true;

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
}
