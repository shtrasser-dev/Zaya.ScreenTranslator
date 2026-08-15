namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public interface IEngineFactoryCatalogService
{
    PluginEngineRegistration Find(PluginServiceKind kind, string engineId);
}
