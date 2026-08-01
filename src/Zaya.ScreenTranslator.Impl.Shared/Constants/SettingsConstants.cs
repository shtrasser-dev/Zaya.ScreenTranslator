namespace Zaya.ScreenTranslator.Impl.Shared.Constants;

/// <summary>Persisted setting dictionary keys for the screen-translator profile section.</summary>
internal static class SettingsConstants
{
    public const string StKey = "screenTranslator";

    public const string ProfileName = "profileName";
    public const string TargetProcess = "targetProcess";
    public const string FramePauseMs = "framePauseMs";
    public const string Ocr = "ocr";
    public const string Capture = "capture";
    public const string TextLayout = "textLayout";
    public const string Translator = "translator";
    public const string OverlayLayout = "overlayLayout";

    public const string TranslatorNone = "none";

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

    public static class EngineDefaults
    {
        public const string ProfileName = "Default";
        public const string Ocr = "oneocr";
        public const string Capture = "graphics-capture";
        public const string TextLayout = "proximity-text-layout";
        public const string Translator = "google";
        public const string OverlayLayout = "screen-overlay";
    }
}
