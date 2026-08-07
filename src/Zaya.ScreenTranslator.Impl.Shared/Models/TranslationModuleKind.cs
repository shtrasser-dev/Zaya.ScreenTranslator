namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>
/// Pipeline modules whose engine/session should be recreated after settings change.
/// </summary>
[Flags]
public enum TranslationModuleKind
{
    None = 0,
    Capture = 1 << 0,
    Ocr = 1 << 1,
    TextLayout = 1 << 2,
    Translator = 1 << 3,
    Overlay = 1 << 4,
    /// <summary>Requires stopping the loop and starting a new session (window/process/display).</summary>
    FullRestart = 1 << 5,
}
