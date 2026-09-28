using Zaya.PluginManager.Impl.Update;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public sealed class BuiltinPluginCatalog : IBuiltinPluginCatalog
{
    private readonly Lazy<IReadOnlyList<BuiltinPluginEntry>> _entries;

    public BuiltinPluginCatalog(IEmbeddedResourceService embeddedResourceService)
    {
        _entries = new Lazy<IReadOnlyList<BuiltinPluginEntry>>(() => Load(embeddedResourceService));
    }

    public IReadOnlyList<BuiltinPluginEntry> Entries => _entries.Value;

    private static IReadOnlyList<BuiltinPluginEntry> Load(IEmbeddedResourceService embeddedResourceService)
    {
        using var stream = embeddedResourceService.GetStream(EmbeddedResourceConstants.BuiltinPluginsJson);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        return JsonBuiltinPluginCatalog.FromStream(copy).Entries;
    }
}
