using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

public static class ScreenTranslatorSettingDescriptors
{
    public const string StKey = SettingsConstants.StKey;

    public const string ProfileName = SettingsConstants.ProfileName;
    public const string TargetProcess = SettingsConstants.TargetProcess;
    public const string TargetFps = SettingsConstants.TargetFps;
    public const string Ocr = SettingsConstants.Ocr;
    public const string Capture = SettingsConstants.Capture;
    public const string TextLayout = SettingsConstants.TextLayout;
    public const string Translator = SettingsConstants.Translator;
    public const string OverlayLayout = SettingsConstants.OverlayLayout;

    /// <summary>Sentinel engine id: OCR only, skip translation.</summary>
    public const string TranslatorNone = SettingsConstants.TranslatorNone;

    public const string DisplayModeTextWindow = AppConstants.DisplayMode.TextWindow;
    public const string DisplayModeOverlay = AppConstants.DisplayMode.Overlay;

    public const string EnableCache = SettingsConstants.EnableCache;
    public const string CacheTtlMinutes = SettingsConstants.CacheTtlMinutes;

    public const string FilterMinLength = SettingsConstants.FilterMinLength;
    public const string FilterRules = SettingsConstants.FilterRules;

    public const string RuleEnabled = SettingsConstants.RuleEnabled;
    public const string RulePattern = SettingsConstants.RulePattern;
    public const string RuleIsRegex = SettingsConstants.RuleIsRegex;
    public const string RuleIgnoreCase = SettingsConstants.RuleIgnoreCase;
    public const string RuleAction = SettingsConstants.RuleAction;
    public const string RuleDescription = SettingsConstants.RuleDescription;

    public const string ActionStrip = SettingsConstants.ActionStrip;
    public const string ActionSkip = SettingsConstants.ActionSkip;

    // Compact 16×16 glyphs (currentColor) for filter table headers.
    private const string IconEnabled =
        """<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18.36 6.64a9 9 0 1 1-12.73 0"/><line x1="12" y1="2" x2="12" y2="12"/></svg>""";

    private const string IconRegex =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 508 368" fill="currentColor"><g transform="translate(0,368) scale(0.1,-0.1)"><path d="M2813 2763 c-62 -8 -63 -23 -16 -292 l6 -34 -114 92 c-63 50 -121 91 -131 91 -36 0 -119 -137 -105 -173 3 -8 67 -38 143 -68 105 -41 132 -55 118 -61 -11 -5 -73 -30 -139 -56 -80 -32 -121 -54 -123 -65 -8 -39 74 -167 106 -167 10 0 66 39 125 86 59 48 110 85 112 82 2 -2 -5 -58 -15 -123 -27 -162 -26 -173 8 -182 15 -5 58 -8 96 -7 96 1 97 4 75 151 -10 65 -21 130 -24 145 -4 25 7 19 110 -63 82 -65 121 -90 137 -87 37 5 109 136 95 172 -3 7 -67 37 -142 66 l-136 53 128 51 c150 60 153 61 153 87 0 45 -76 159 -107 159 -7 0 -64 -40 -125 -89 -62 -49 -113 -89 -114 -88 -1 1 9 65 21 142 12 77 20 145 17 152 -7 20 -95 35 -159 26z"/><path d="M1353 2752 c-32 -20 -145 -308 -189 -479 -47 -185 -58 -285 -58 -503 0 -231 14 -342 69 -544 52 -189 154 -426 190 -440 21 -8 178 -8 199 0 9 3 16 14 16 24 0 10 -27 99 -59 197 -91 273 -131 507 -131 768 0 283 52 570 150 825 45 120 48 135 21 150 -23 12 -190 13 -208 2z"/><path d="M3564 2746 c-19 -14 -19 -15 0 -63 129 -332 177 -581 177 -913 0 -325 -45 -556 -178 -913 -18 -47 -18 -49 1 -63 21 -15 191 -20 212 -6 17 11 87 156 127 263 142 380 166 821 67 1217 -46 183 -151 452 -187 480 -24 18 -193 16 -219 -2z"/><path d="M1903 1485 c-53 -23 -68 -57 -68 -155 0 -140 29 -170 160 -170 130 0 165 36 165 171 0 87 -19 131 -65 153 -41 20 -148 20 -192 1z"/></g></svg>""";

    private const string IconIgnoreCase =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 508 368" fill="currentColor"><g transform="translate(0,368) scale(0.1,-0.1)"><path d="M1536 2971 c-41 -6 -65 -16 -81 -32 -17 -17 -122 -307 -364 -1004 -187 -539 -345 -1001 -351 -1026 -24 -94 17 -113 222 -107 105 3 140 7 161 21 22 14 37 52 99 244 l74 228 406 3 406 2 77 -230 c64 -193 81 -234 103 -248 22 -14 53 -17 209 -17 175 0 183 1 205 22 20 21 21 26 10 71 -12 51 -687 1993 -704 2025 -19 37 -73 48 -247 52 -91 2 -192 0 -225 -4z m317 -874 c81 -243 147 -446 147 -450 0 -4 -135 -7 -300 -7 -165 0 -300 3 -300 7 0 15 295 893 300 893 3 0 72 -199 153 -443z"/><path d="M3435 2439 c-218 -29 -415 -111 -455 -189 -33 -64 -23 -207 18 -252 23 -27 50 -22 150 26 162 79 239 99 382 100 107 1 131 -2 171 -20 87 -40 119 -109 119 -250 l0 -94 -142 0 c-536 0 -799 -166 -798 -505 1 -285 177 -457 495 -481 159 -13 333 45 452 149 l58 51 3 -55 c4 -87 8 -97 50 -108 48 -14 196 -14 244 -1 21 6 43 20 49 31 7 14 9 194 7 592 -4 553 -5 575 -26 652 -26 94 -72 174 -127 222 -56 49 -167 100 -255 118 -96 19 -305 27 -395 14z m385 -1073 l0 -134 -57 -51 c-32 -28 -83 -64 -113 -79 -47 -23 -67 -27 -140 -27 -98 0 -148 21 -189 81 -47 69 -40 182 15 243 64 72 159 98 367 100 l117 1 0 -134z"/></g></svg>""";

    private static LocalizedString Loc(string key)
        => new(key, culture => Properties.Resources.Instance.GetString(key, culture));

    public static readonly IReadOnlyList<SettingDescriptor> FilterDescriptors = BuildFilterDescriptors();

    public static readonly IReadOnlyList<SettingDescriptor> All = [
        new StringSettingDescriptor(ProfileName, LocalizedString.Invariant(ProfileName))
        {
            DefaultValue = SettingsConstants.EngineDefaults.ProfileName
        },
        new StringSettingDescriptor(TargetProcess, Loc(LocalizationConstants.Settings.TargetProcess))
        {
            DefaultValue = string.Empty,
            Description = Loc(LocalizationConstants.Settings.TargetProcessDesc),
        },
        new IntegerSettingDescriptor(TargetFps, LocalizedString.Invariant(TargetFps))
        {
            DefaultValue = 1,
            MinValue = 1,
            MaxValue = 120
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
        new StringSettingDescriptor(OverlayLayout, LocalizedString.Invariant(OverlayLayout))
        {
            DefaultValue = SettingsConstants.EngineDefaults.OverlayLayout
        },
        new BooleanSettingDescriptor(EnableCache, Loc(LocalizationConstants.Settings.EnableCache))
        {
            DefaultValue = true,
        },
        new IntegerSettingDescriptor(CacheTtlMinutes, Loc(LocalizationConstants.Settings.CacheTtlMinutes))
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
            new IntegerSettingDescriptor(FilterMinLength, Loc(LocalizationConstants.Filter.MinLength))
            {
                DefaultValue = 10,
                MinValue = 0,
                MaxValue = 10000,
            },
            new TableSettingDescriptor(FilterRules, Loc(LocalizationConstants.Filter.Rules))
            {
                Columns =
                [
                    new BooleanSettingDescriptor(RuleEnabled, Loc(LocalizationConstants.Filter.Enabled))
                    {
                        DefaultValue = true,
                        IconSvg = IconEnabled,
                    },
                    new StringSettingDescriptor(RulePattern, Loc(LocalizationConstants.Filter.Pattern))
                    {
                        DefaultValue = string.Empty,
                    },
                    new BooleanSettingDescriptor(RuleIsRegex, Loc(LocalizationConstants.Filter.IsRegex))
                    {
                        DefaultValue = false,
                        IconSvg = IconRegex,
                    },
                    new BooleanSettingDescriptor(RuleIgnoreCase, Loc(LocalizationConstants.Filter.IgnoreCase))
                    {
                        DefaultValue = true,
                        IconSvg = IconIgnoreCase,
                    },
                    new EnumSettingDescriptor(RuleAction, Loc(LocalizationConstants.Filter.Action))
                    {
                        DefaultValue = ActionSkip,
                        Options =
                        [
                            new EnumOption(ActionSkip, Loc(LocalizationConstants.Filter.ActionSkip)),
                            new EnumOption(ActionStrip, Loc(LocalizationConstants.Filter.ActionStrip)),
                        ],
                    },
                    new StringSettingDescriptor(RuleDescription, Loc(LocalizationConstants.Filter.Description))
                    {
                        DefaultValue = string.Empty,
                    },
                ],
            },
        ];
    }
}
