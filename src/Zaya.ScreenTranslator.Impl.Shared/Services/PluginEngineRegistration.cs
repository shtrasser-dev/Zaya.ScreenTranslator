namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class PluginEngineRegistration
{
    public required string PluginId { get; init; }
    public required string PluginType { get; init; }
    public required string EngineId { get; init; }
    public string? DisplayName { get; init; }
    public required Type EntryType { get; init; }
    public required PluginServiceKind ServiceKind { get; init; }
}
