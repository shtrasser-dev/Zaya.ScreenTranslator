using System.Globalization;
using Zaya.PluginManager.Impl.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class LocalizationPluginUiCulture : IPluginUiCulture
{
    private readonly ILocalizationService _localizationService;

    public LocalizationPluginUiCulture(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public CultureInfo Current => _localizationService.CurrentCulture;
}
