using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// Binding-friendly string indexer. Replace the instance after a culture change
/// so compiled Avalonia bindings re-query localized text.
/// </summary>
public sealed class LocalizedStrings
{
    private readonly ILocalizationService _localizationService;

    public LocalizedStrings(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public string this[string key] => _localizationService[key];
}
