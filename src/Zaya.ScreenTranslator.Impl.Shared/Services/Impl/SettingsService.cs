using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

public sealed class SettingsService : ISettingsService
{
    private readonly IApplicationProfileService _applicationProfileService;
    private readonly IEngineFactory _engineFactory;
    private readonly IPluginCatalog _pluginCatalog;
    private readonly ILocalizationService _localizationService;

    public SettingsService(
        IApplicationProfileService applicationProfileService,
        IEngineFactory engineFactory,
        IPluginCatalog pluginCatalog,
        ILocalizationService localizationService)
    {
        _applicationProfileService = applicationProfileService;
        _engineFactory = engineFactory;
        _pluginCatalog = pluginCatalog;
        _localizationService = localizationService;
    }

    public IApplicationProfile BeginEdit()
    {
        var active = _applicationProfileService.ActiveProfile;
        if (active is null)
            return new ApplicationProfile();

        return new ApplicationProfile
        {
            Settings = DeepCloneSettings(active.Settings)
        };
    }

    private static Dictionary<string, Dictionary<string, object>> DeepCloneSettings(
        Dictionary<string, Dictionary<string, object>> source)
    {
        var result = new Dictionary<string, Dictionary<string, object>>(source.Count);
        foreach (var (pluginId, settings) in source)
        {
            var copy = new Dictionary<string, object>(settings.Count);
            foreach (var (key, value) in settings)
            {
                if (ManagedSettingKeys.IsEphemeralHostKey(key) || value is IntPtr or UIntPtr or nint or nuint)
                    continue;
                copy[key] = DeepCloneValue(value)!;
            }
            result[pluginId] = copy;
        }
        return result;
    }

    private static object? DeepCloneValue(object? value)
    {
        if (value is null)
            return null;

        if (value is string or bool or int or long or double or float or decimal)
            return value;

        if (value is Dictionary<string, object> dict)
        {
            var copy = new Dictionary<string, object>(dict.Count);
            foreach (var (k, v) in dict)
                copy[k] = DeepCloneValue(v)!;
            return copy;
        }

        if (value is System.Collections.IList list)
        {
            var copy = new List<object?>(list.Count);
            foreach (var item in list)
                copy.Add(DeepCloneValue(item));
            return copy;
        }

        return value;
    }

    public void CommitEdit(IApplicationProfile edited)
    {
        _applicationProfileService.Save(edited);
        _applicationProfileService.SetActiveProfile(edited);
    }

    public void CommitEditAsNew(string name, IApplicationProfile edited)
    {
        edited.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.ProfileName] = name;
        _applicationProfileService.Save(edited);
        _applicationProfileService.SetActiveProfile(edited);
    }

    public IReadOnlyList<EngineInfo> GetAvailableOcrEngines()
        => FromCatalog(PluginServiceKind.Ocr);

    public IReadOnlyList<SettingDescriptor>? GetOcrDescriptors(string engineId)
    {
        using var engine = _engineFactory.CreateOcr(engineId);
        return engine?.Settings;
    }

    public IReadOnlyList<EngineInfo> GetAvailableCaptureEngines()
        => FromCatalog(PluginServiceKind.Capture);

    public IReadOnlyList<SettingDescriptor>? GetCaptureDescriptors(string engineId)
    {
        using var engine = _engineFactory.CreateCapture(engineId);
        return engine?.Settings;
    }

    public IReadOnlyList<EngineInfo> GetAvailableTextLayoutEngines()
        => FromCatalog(PluginServiceKind.TextLayout);

    public IReadOnlyList<SettingDescriptor>? GetTextLayoutDescriptors(string engineId)
    {
        using var engine = _engineFactory.CreateTextLayout(engineId);
        return engine?.Settings;
    }

    public IReadOnlyList<EngineInfo> GetAvailableTranslatorEngines()
    {
        using var builtIn = _engineFactory.CreateTranslator(NoTranslationTranslatorService.EngineIdValue);
        var label = builtIn!.DisplayName.GetValue(_localizationService.CurrentCulture);
        var engines = new List<EngineInfo> { new(builtIn.EngineId, label) };
        engines.AddRange(FromCatalog(PluginServiceKind.Translator));
        return engines;
    }

    public IReadOnlyList<SettingDescriptor>? GetTranslatorDescriptors(string engineId)
    {
        using var engine = _engineFactory.CreateTranslator(engineId);
        return engine?.Settings;
    }

    public IReadOnlyList<EngineInfo> GetAvailableTranslatorCacheEngines()
    {
        using var builtIn = _engineFactory.CreateTranslatorCache(NoTranslatorCacheService.EngineIdValue);
        var label = builtIn!.DisplayName.GetValue(_localizationService.CurrentCulture);
        var engines = new List<EngineInfo>();
        engines.AddRange(FromCatalog(PluginServiceKind.TranslatorCache));
        engines.Add(new EngineInfo(builtIn.EngineId, label));
        return engines;
    }

    public IReadOnlyList<SettingDescriptor>? GetTranslatorCacheDescriptors(string engineId)
    {
        using var engine = _engineFactory.CreateTranslatorCache(engineId);
        return engine?.Settings;
    }

    public IReadOnlyList<EngineInfo> GetAvailableOverlayLayoutEngines()
        => FromCatalog(PluginServiceKind.OverlayLayout);

    public IReadOnlyList<SettingDescriptor>? GetOverlayLayoutDescriptors(string engineId)
    {
        using var engine = _engineFactory.CreateOverlayLayout(engineId);
        return engine?.Settings;
    }

    private IReadOnlyList<EngineInfo> FromCatalog(PluginServiceKind kind)
        => _pluginCatalog.List(kind)
            .Select(e => new EngineInfo(e.EngineId, e.DisplayName))
            .ToList();
}
