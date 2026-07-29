using Zaya.Primitives;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

public sealed class ApplicationProfile : IApplicationProfile
{
    private Dictionary<string, Dictionary<string, object>> _settings = [];
    private SettingDescriptorList? _stSettings;

    public Dictionary<string, Dictionary<string, object>> Settings
    {
        get => _settings;
        init
        {
            _settings = value;
            EnsureScreenTranslatorSubDict();
        }
    }

    public SettingDescriptorList ScreenTranslatorSettings
    {
        get
        {
            if (_stSettings is null)
            {
                _stSettings = new SettingDescriptorList(ScreenTranslatorSettingDescriptors.All);
                _stSettings.Bind(Settings[ScreenTranslatorSettingDescriptors.StKey]);
            }
            return _stSettings;
        }
    }

    public ApplicationProfile()
    {
        _settings = new()
        {
            ["screenTranslator"] = new()
        };
    }

    private void EnsureScreenTranslatorSubDict()
    {
        if (!_settings.ContainsKey(ScreenTranslatorSettingDescriptors.StKey))
            _settings[ScreenTranslatorSettingDescriptors.StKey] = new();
    }
}
