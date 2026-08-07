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
    public const string TranslatorCache = "translatorCache";
    public const string OverlayLayout = "overlayLayout";

    public const string TranslatorNone = "none";
    /// <summary>Distinct from <see cref="TranslatorNone"/> so plugin setting bags do not collide.</summary>
    public const string TranslatorCacheNone = "cache-none";

    public const string CaptureRegions = "captureRegions";
    public const string IgnoreRegions = "ignoreRegions";

    public static class EngineDefaults
    {
        public const string ProfileName = "Default";
        public const string Ocr = "oneocr";
        public const string Capture = "graphics-capture";
        public const string TextLayout = "proximity-text-layout";
        public const string Translator = "yandex";
        public const string TranslatorCache = "memory-translator-cache";
        public const string OverlayLayout = "screen-overlay";
    }
}
