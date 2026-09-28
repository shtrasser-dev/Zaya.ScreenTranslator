using Zaya.PluginManager.Impl.Services;
using Zaya.ScreenTranslator.Layout.Impl;
using Zaya.ScreenTranslator.Layout.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public static class HostBundledPlugins
{
    public static void RegisterOverlayLayout(IPluginCatalog catalog)
    {
        var entryType = typeof(ScreenOverlayLayoutService);
        var ifaceVer = typeof(IOverlayLayoutService).Assembly.GetName().Version;
        var pluginVer = entryType.Assembly.GetName().Version;
        var ifaceThree = FormatThreePart(ifaceVer);
        var pluginThree = FormatThreePart(pluginVer) ?? ifaceThree;

        catalog.Register(
            new PluginManifest
            {
                Id = "ScreenOverlay",
                Type = "overlaylayout",
                Interface = "Zaya.ScreenTranslator.Layout",
                InterfaceVersion = ifaceThree ?? string.Empty,
                PluginVersion = pluginThree ?? string.Empty,
                EntryPoint = entryType.FullName!,
            },
            [entryType.Assembly]);
    }

    private static string? FormatThreePart(Version? ver)
        => ver is null ? null : $"{ver.Major}.{ver.Minor}.{Math.Max(ver.Build, 0)}";
}
