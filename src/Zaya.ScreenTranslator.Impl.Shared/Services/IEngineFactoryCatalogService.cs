using Zaya.PluginManager.Impl.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public interface IEngineFactoryCatalogService
{
    PluginEngineRegistration Find(string kind, string engineId);
}
