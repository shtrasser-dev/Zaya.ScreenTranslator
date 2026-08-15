using Zaya.Logging.Models;
using Zaya.ScreenTranslator.Impl.Shared.Exceptions;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

public sealed class EngineFactoryCatalogService : IEngineFactoryCatalogService
{
    private readonly IPluginCatalog _pluginCatalog;

    public EngineFactoryCatalogService(IPluginCatalog pluginCatalog)
    {
        _pluginCatalog = pluginCatalog;
    }

    [Log(LogLevel.Debug, LogParameters = true)]
    public PluginEngineRegistration Find(PluginServiceKind kind, string engineId)
        => _pluginCatalog.Find(kind, engineId)
           ?? throw new EngineNotFoundException();
}
