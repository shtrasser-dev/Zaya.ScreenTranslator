using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public sealed class BuiltinPluginCatalog : IBuiltinPluginCatalog
{
    private readonly Lazy<IReadOnlyList<BuiltinPluginEntry>> _entries;

    private readonly IJsonConfigurationService _jsonConfigurationService;

    public BuiltinPluginCatalog(IEmbeddedResourceService embeddedResourceService, IJsonConfigurationService jsonConfigurationService)
    {
        _entries = new Lazy<IReadOnlyList<BuiltinPluginEntry>>(() => Load(embeddedResourceService));
        _jsonConfigurationService = jsonConfigurationService;
    }

    public IReadOnlyList<BuiltinPluginEntry> Entries => _entries.Value;

    private IReadOnlyList<BuiltinPluginEntry> Load(IEmbeddedResourceService embeddedResourceService)
    {
        using var stream = embeddedResourceService.GetStream(EmbeddedResourceConstants.BuiltinPluginsJson);
        var list = _jsonConfigurationService.Read<List<BuiltinPluginEntry>>(stream);
        return list.AsReadOnly();
    }
}
