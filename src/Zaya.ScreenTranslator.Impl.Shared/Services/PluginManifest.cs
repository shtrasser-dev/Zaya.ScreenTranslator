using System.Text.Json.Serialization;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class PluginManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("interface")]
    public string Interface { get; set; } = string.Empty;

    [JsonPropertyName("interfaceVersion")]
    public string InterfaceVersion { get; set; } = string.Empty;

    [JsonPropertyName("pluginVersion")]
    public string PluginVersion { get; set; } = string.Empty;

    /// <summary>Legacy channel field (older plugins). Prefer <see cref="InterfaceChannel"/> / <see cref="UpdateChannel"/>.</summary>
    [JsonPropertyName("primitivesChannel")]
    public string PrimitivesChannel { get; set; } = string.Empty;

    /// <summary>Interface MAJOR.MINOR channel used by floating release tags (<c>plugin-{interface}-v{channel}-latest</c>).</summary>
    [JsonPropertyName("interfaceChannel")]
    public string InterfaceChannel { get; set; } = string.Empty;

    /// <summary>Alias for the update channel (docs name).</summary>
    [JsonPropertyName("updateChannel")]
    public string UpdateChannel { get; set; } = string.Empty;

    /// <summary>
    /// Channel for updater compatibility checks: explicit channel fields, else MAJOR.MINOR of <see cref="InterfaceVersion"/>.
    /// </summary>
    public string ResolveUpdateChannel()
    {
        if (!string.IsNullOrWhiteSpace(InterfaceChannel))
            return InterfaceChannel.Trim();
        if (!string.IsNullOrWhiteSpace(UpdateChannel))
            return UpdateChannel.Trim();
        if (!string.IsNullOrWhiteSpace(PrimitivesChannel))
            return PrimitivesChannel.Trim();
        if (Version.TryParse(InterfaceVersion, out var v))
            return $"{v.Major}.{v.Minor}";
        return string.Empty;
    }
}
