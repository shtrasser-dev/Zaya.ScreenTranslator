namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public interface IBuiltinPluginCatalog
{
    IReadOnlyList<BuiltinPluginEntry> Entries { get; }
}
