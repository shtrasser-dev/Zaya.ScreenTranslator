namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class PluginManifest
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Interface { get; set; } = string.Empty;

    public string InterfaceVersion { get; set; } = string.Empty;

    public string PluginVersion { get; set; } = string.Empty;

    /// <summary>
    /// Fully qualified CLR type name of the engine entry point (public <c>ctor(ILoggingWrapper)</c>).
    /// </summary>
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>Legacy channel field (older plugins). Prefer <see cref="InterfaceChannel"/> / <see cref="UpdateChannel"/>.</summary>
    public string PrimitivesChannel { get; set; } = string.Empty;

    /// <summary>Interface MAJOR.MINOR channel used by floating release tags (<c>plugin-{interface}-v{channel}-latest</c>).</summary>
    public string InterfaceChannel { get; set; } = string.Empty;

    /// <summary>Alias for the update channel (docs name).</summary>
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
