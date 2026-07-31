namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Binding-friendly string indexer. Replace the instance after a culture change
/// so compiled Avalonia bindings re-query localized text.
/// </summary>
public sealed class LocalizedStrings
{
    private readonly LocalizationService _localization;

    public LocalizedStrings(LocalizationService localization)
    {
        _localization = localization;
    }

    public string this[string key] => _localization[key];
}
