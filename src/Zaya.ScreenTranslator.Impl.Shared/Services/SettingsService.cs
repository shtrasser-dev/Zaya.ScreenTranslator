using System.Reflection;
using Zaya.OCR.Services;
using Zaya.Primitives;
using Zaya.Screenshot.Services;
using Zaya.Translator.Services;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Layout.Impl.Services;
using Zaya.ScreenTranslator.Layout.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly IApplicationProfileService _profileService;

    public SettingsService(IApplicationProfileService profileService)
    {
        _profileService = profileService;
    }

    public IApplicationProfile BeginEdit()
    {
        var active = _profileService.ActiveProfile;
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
        _profileService.Save(edited);
        _profileService.SetActiveProfile(edited);
    }

    public void CommitEditAsNew(string name, IApplicationProfile edited)
    {
        edited.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.ProfileName] = name;
        _profileService.Save(edited);
        _profileService.SetActiveProfile(edited);
    }

    public IReadOnlyList<EngineInfo> GetAvailableOcrEngines()
    {
        return ScanForEngines(typeof(IOCRService));
    }

    public IReadOnlyList<EngineInfo> GetAvailableCaptureEngines()
    {
        return ScanForEngines(typeof(ICaptureService));
    }

    public IReadOnlyList<SettingDescriptor>? GetOcrDescriptors(string engineId)
    {
        using var engine = EngineFactory.CreateOcr(engineId);
        return engine?.Settings;
    }

    public IReadOnlyList<SettingDescriptor>? GetCaptureDescriptors(string engineId)
    {
        using var engine = EngineFactory.CreateCapture(engineId);
        return engine?.Settings;
    }

    public IReadOnlyList<EngineInfo> GetAvailableTextLayoutEngines()
    {
        return ScanForEngines(typeof(ITextLayoutService));
    }

    public IReadOnlyList<SettingDescriptor>? GetTextLayoutDescriptors(string engineId)
    {
        using var engine = EngineFactory.CreateTextLayout(engineId);
        return engine?.Settings;
    }

    public IReadOnlyList<EngineInfo> GetAvailableTranslatorEngines()
    {
        using var builtIn = new NoTranslationTranslatorService();
        var label = builtIn.DisplayName.GetValue(LocalizationService.Instance.CurrentCulture);
        var engines = new List<EngineInfo> { new(builtIn.EngineId, label) };
        engines.AddRange(ScanForEngines(typeof(ITranslatorService)));
        return engines;
    }

    public IReadOnlyList<SettingDescriptor>? GetTranslatorDescriptors(string engineId)
    {
        using var engine = EngineFactory.CreateTranslator(engineId);
        return engine?.Settings;
    }

    public IReadOnlyList<EngineInfo> GetAvailableOverlayLayoutEngines()
    {
        using var builtIn = new ScreenOverlayLayoutService();
        var label = builtIn.DisplayName.GetValue(LocalizationService.Instance.CurrentCulture);
        var engines = new List<EngineInfo> { new(builtIn.EngineId, label) };
        engines.AddRange(ScanForEngines(typeof(IOverlayLayoutService))
            .Where(e => !string.Equals(e.Id, builtIn.EngineId, StringComparison.OrdinalIgnoreCase)));
        return engines;
    }

    public IReadOnlyList<SettingDescriptor>? GetOverlayLayoutDescriptors(string engineId)
    {
        using var engine = EngineFactory.CreateOverlayLayout(engineId);
        return engine?.Settings;
    }

    private static IReadOnlyList<EngineInfo> ScanForEngines(Type serviceType)
    {
        var engines = new List<EngineInfo>();
        foreach (var asm in PluginLoader.LoadedAssemblies)
        {
            foreach (var type in SafeGetTypes(asm))
            {
                if (type is { IsClass: true, IsAbstract: false } &&
                    serviceType.IsAssignableFrom(type))
                {
                    try
                    {
                        var instance = Activator.CreateInstance(type);
                        if (instance is IOCRService ocr)
                            engines.Add(new EngineInfo(ocr.EngineId));
                        else if (instance is ICaptureService cap)
                            engines.Add(new EngineInfo(cap.EngineId));
                        else if (instance is ITextLayoutService tl)
                            engines.Add(new EngineInfo(tl.EngineId));
                        else if (instance is ITranslatorService tr)
                            engines.Add(new EngineInfo(tr.EngineId));
                        else if (instance is IOverlayLayoutService ol)
                            engines.Add(new EngineInfo(ol.EngineId, ol.DisplayName.GetValue(LocalizationService.Instance.CurrentCulture)));
                        (instance as IDisposable)?.Dispose();
                    }
                    catch { }
                }
            }
        }
        return engines;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
        catch { return []; }
    }
}
