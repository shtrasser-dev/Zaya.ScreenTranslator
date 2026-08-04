using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

public static class ScreenTranslatorSettingDescriptors
{
    public const string StKey = SettingsConstants.StKey;

    public const string ProfileName = SettingsConstants.ProfileName;
    public const string TargetProcess = SettingsConstants.TargetProcess;
    public const string FramePauseMs = SettingsConstants.FramePauseMs;
    public const string Ocr = SettingsConstants.Ocr;
    public const string Capture = SettingsConstants.Capture;
    public const string TextLayout = SettingsConstants.TextLayout;
    public const string Translator = SettingsConstants.Translator;
    public const string TranslatorCache = SettingsConstants.TranslatorCache;
    public const string OverlayLayout = SettingsConstants.OverlayLayout;

    /// <summary>Sentinel engine id: OCR only, skip translation.</summary>
    public const string TranslatorNone = SettingsConstants.TranslatorNone;

    /// <summary>Sentinel engine id: do not wrap translator sessions with a cache.</summary>
    public const string TranslatorCacheNone = SettingsConstants.TranslatorCacheNone;

    public const string DisplayModeTextWindow = AppConstants.DisplayMode.TextWindow;
    public const string DisplayModeOverlay = AppConstants.DisplayMode.Overlay;

    private static LocalizedString Loc(string key)
        => new(key, culture => Properties.Resources.Instance.GetString(key, culture));

    public static readonly IReadOnlyList<SettingDescriptor> All =
    [
        new StringSettingDescriptor(ProfileName, LocalizedString.Invariant(ProfileName))
        {
            DefaultValue = SettingsConstants.EngineDefaults.ProfileName
        },
        new StringSettingDescriptor(TargetProcess, Loc(LocalizationConstants.Settings.TargetProcess))
        {
            DefaultValue = string.Empty,
            Description = Loc(LocalizationConstants.Settings.TargetProcessDesc),
        },
        new IntegerSettingDescriptor(FramePauseMs, Loc(LocalizationConstants.Settings.FramePauseMs))
        {
            Description = Loc(LocalizationConstants.Settings.FramePauseMsDesc),
            DefaultValue = 100,
            MinValue = 0,
            MaxValue = 10000,
        },
        new StringSettingDescriptor(Ocr, LocalizedString.Invariant(Ocr))
        {
            DefaultValue = SettingsConstants.EngineDefaults.Ocr
        },
        new StringSettingDescriptor(Capture, LocalizedString.Invariant(Capture))
        {
            DefaultValue = SettingsConstants.EngineDefaults.Capture
        },
        new StringSettingDescriptor(TextLayout, LocalizedString.Invariant(TextLayout))
        {
            DefaultValue = SettingsConstants.EngineDefaults.TextLayout
        },
        new StringSettingDescriptor(Translator, LocalizedString.Invariant(Translator))
        {
            DefaultValue = SettingsConstants.EngineDefaults.Translator
        },
        new StringSettingDescriptor(TranslatorCache, LocalizedString.Invariant(TranslatorCache))
        {
            DefaultValue = SettingsConstants.EngineDefaults.TranslatorCache
        },
        new StringSettingDescriptor(OverlayLayout, LocalizedString.Invariant(OverlayLayout))
        {
            DefaultValue = SettingsConstants.EngineDefaults.OverlayLayout
        },
    ];
}
