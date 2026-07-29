using Zaya.Primitives;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

public static class ScreenTranslatorSettingDescriptors
{
    public const string StKey = "screenTranslator";

    public const string ProfileName = "profileName";
    public const string TargetProcess = "targetProcess";
    public const string TargetFps = "targetFps";
    public const string Ocr = "ocr";
    public const string Capture = "capture";
    public const string TextLayout = "textLayout";
    public const string Translator = "translator";
    public const string OverlayLayout = "overlayLayout";

    /// <summary>Sentinel engine id: OCR only, skip translation.</summary>
    public const string TranslatorNone = "none";

    public const string DisplayModeTextWindow = "textWindow";
    public const string DisplayModeOverlay = "overlay";

    public const string EnableCache = "enableCache";
    public const string CacheTtlMinutes = "cacheTtlMinutes";

    public const string FilterMinLength = "filterMinLength";
    public const string FilterRules = "filterRules";

    public const string RuleEnabled = "enabled";
    public const string RulePattern = "pattern";
    public const string RuleIsRegex = "isRegex";
    public const string RuleIgnoreCase = "ignoreCase";
    public const string RuleAction = "action";
    public const string RuleDescription = "description";

    public const string ActionStrip = "Strip";
    public const string ActionSkip = "Skip";

    private static LocalizedString Loc(string key)
        => new(key, culture => Properties.Resources.Instance.GetString(key, culture));

    public static readonly IReadOnlyList<SettingDescriptor> FilterDescriptors = BuildFilterDescriptors();

    public static readonly IReadOnlyList<SettingDescriptor> All = [
        new StringSettingDescriptor(ProfileName, LocalizedString.Invariant(ProfileName))
        {
            DefaultValue = "Default"
        },
        new StringSettingDescriptor(TargetProcess, LocalizedString.Invariant(TargetProcess))
        {
            DefaultValue = string.Empty
        },
        new IntegerSettingDescriptor(TargetFps, LocalizedString.Invariant(TargetFps))
        {
            DefaultValue = 15,
            MinValue = 1,
            MaxValue = 120
        },
        new StringSettingDescriptor(Ocr, LocalizedString.Invariant(Ocr))
        {
            DefaultValue = "oneocr"
        },
        new StringSettingDescriptor(Capture, LocalizedString.Invariant(Capture))
        {
            DefaultValue = "graphics-capture"
        },
        new StringSettingDescriptor(TextLayout, LocalizedString.Invariant(TextLayout))
        {
            DefaultValue = "proximity-text-layout"
        },
        new StringSettingDescriptor(Translator, LocalizedString.Invariant(Translator))
        {
            DefaultValue = "google"
        },
        new StringSettingDescriptor(OverlayLayout, LocalizedString.Invariant(OverlayLayout))
        {
            DefaultValue = "screen-overlay"
        },
        new BooleanSettingDescriptor(EnableCache, LocalizedString.Invariant("Enable Cache"))
        {
            DefaultValue = true,
        },
        new IntegerSettingDescriptor(CacheTtlMinutes, LocalizedString.Invariant("Cache TTL (min)"))
        {
            DefaultValue = 0,
            MinValue = 0,
            MaxValue = 10080,
        },
        ..FilterDescriptors,
    ];

    private static IReadOnlyList<SettingDescriptor> BuildFilterDescriptors()
    {
        return
        [
            new IntegerSettingDescriptor(FilterMinLength, Loc("Filter_MinLength"))
            {
                DefaultValue = 10,
                MinValue = 0,
                MaxValue = 10000,
            },
            new TableSettingDescriptor(FilterRules, Loc("Filter_Rules"))
            {
                Columns =
                [
                    new BooleanSettingDescriptor(RuleEnabled, Loc("Filter_Enabled"))
                    {
                        DefaultValue = true,
                    },
                    new StringSettingDescriptor(RulePattern, Loc("Filter_Pattern"))
                    {
                        DefaultValue = string.Empty,
                    },
                    new BooleanSettingDescriptor(RuleIsRegex, Loc("Filter_IsRegex"))
                    {
                        DefaultValue = false,
                    },
                    new BooleanSettingDescriptor(RuleIgnoreCase, Loc("Filter_IgnoreCase"))
                    {
                        DefaultValue = true,
                    },
                    new EnumSettingDescriptor(RuleAction, Loc("Filter_Action"))
                    {
                        DefaultValue = ActionSkip,
                        Options =
                        [
                            new EnumOption(ActionSkip, Loc("Filter_Action_Skip")),
                            new EnumOption(ActionStrip, Loc("Filter_Action_Strip")),
                        ],
                    },
                    new StringSettingDescriptor(RuleDescription, Loc("Filter_Description"))
                    {
                        DefaultValue = string.Empty,
                    },
                ],
            },
        ];
    }
}
