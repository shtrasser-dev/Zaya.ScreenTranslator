using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

/// <summary>Loads plugin setting descriptors for the selected engines.</summary>
internal sealed class SettingsEngineDescriptorLoader
{
    private readonly ISettingsService _settingsService;

    public SettingsEngineDescriptorLoader(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public IReadOnlyList<SettingDescriptor>? LoadOcr(IApplicationProfile profile)
        => _settingsService.GetOcrDescriptors(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));

    public IReadOnlyList<SettingDescriptor>? LoadCapture(IApplicationProfile profile)
        => _settingsService.GetCaptureDescriptors(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));

    public IReadOnlyList<SettingDescriptor>? LoadTextLayout(IApplicationProfile profile)
        => _settingsService.GetTextLayoutDescriptors(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));

    public IReadOnlyList<SettingDescriptor>? LoadTranslator(IApplicationProfile profile)
        => _settingsService.GetTranslatorDescriptors(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));

    public IReadOnlyList<SettingDescriptor>? LoadTranslatorCache(IApplicationProfile profile)
        => _settingsService.GetTranslatorCacheDescriptors(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TranslatorCache));

    public IReadOnlyList<SettingDescriptor>? LoadOverlayLayout(IApplicationProfile profile)
        => _settingsService.GetOverlayLayoutDescriptors(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout));
}
