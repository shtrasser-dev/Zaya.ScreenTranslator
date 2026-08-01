using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed record EngineInfo(string Id, string? DisplayName = null)
{
    public string Label => DisplayName ?? Id;
}

public interface ISettingsService
{
    IApplicationProfile BeginEdit();
    void CommitEdit(IApplicationProfile edited);
    void CommitEditAsNew(string name, IApplicationProfile edited);

    IReadOnlyList<EngineInfo> GetAvailableOcrEngines();
    IReadOnlyList<EngineInfo> GetAvailableCaptureEngines();
    IReadOnlyList<SettingDescriptor>? GetOcrDescriptors(string engineId);
    IReadOnlyList<SettingDescriptor>? GetCaptureDescriptors(string engineId);
    IReadOnlyList<EngineInfo> GetAvailableTextLayoutEngines();
    IReadOnlyList<SettingDescriptor>? GetTextLayoutDescriptors(string engineId);
    IReadOnlyList<EngineInfo> GetAvailableTranslatorEngines();
    IReadOnlyList<SettingDescriptor>? GetTranslatorDescriptors(string engineId);
    IReadOnlyList<EngineInfo> GetAvailableTranslatorCacheEngines();
    IReadOnlyList<SettingDescriptor>? GetTranslatorCacheDescriptors(string engineId);
    IReadOnlyList<EngineInfo> GetAvailableOverlayLayoutEngines();
    IReadOnlyList<SettingDescriptor>? GetOverlayLayoutDescriptors(string engineId);
}
