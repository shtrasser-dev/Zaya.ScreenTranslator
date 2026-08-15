using System.Reflection;
using Zaya.Logging.Services;
using Zaya.OCR.Services;
using Zaya.Screenshot.Services;
using Zaya.ScreenTranslator.Impl.Shared.Exceptions;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Layout.Services;
using Zaya.Translator.Services;
using Zaya.TranslatorCache.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// Catalog of plugin engines discovered from <c>plugin.json</c> <c>entryPoint</c> values.
/// </summary>
public sealed class PluginCatalog : IPluginCatalog
{
    private readonly ILocalizationService _localizationService;
    private readonly List<PluginEngineRegistration> _entries = [];

    public PluginCatalog(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public IReadOnlyList<PluginEngineRegistration> Entries => _entries;

    public void Clear() => _entries.Clear();

    public void Register(PluginManifest manifest, IReadOnlyList<Assembly> assemblies)
    {
        if (string.IsNullOrWhiteSpace(manifest.EntryPoint))
            throw new InvalidPluginManifestException(InvalidPluginManifestReason.MissingEntryPoint);

        var entryType = ResolveType(manifest.EntryPoint.Trim(), assemblies);
        if (entryType is null)
            throw new InvalidPluginManifestException(InvalidPluginManifestReason.EntryPointNotFound);

        if (!TryProbe(entryType, out var engineId, out var displayName, out var serviceKind))
            throw new InvalidPluginManifestException(InvalidPluginManifestReason.ProbeFailed);

        _entries.RemoveAll(e =>
            string.Equals(e.EngineId, engineId, StringComparison.OrdinalIgnoreCase)
            && e.ServiceKind == serviceKind);

        _entries.Add(new PluginEngineRegistration
        {
            PluginId = manifest.Id,
            PluginType = manifest.Type,
            EngineId = engineId,
            DisplayName = displayName,
            EntryType = entryType,
            ServiceKind = serviceKind,
        });
    }

    public PluginEngineRegistration? Find(PluginServiceKind kind, string engineId)
        => _entries.FirstOrDefault(e =>
            e.ServiceKind == kind
            && string.Equals(e.EngineId, engineId, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<PluginEngineRegistration> List(PluginServiceKind kind)
        => _entries.Where(e => e.ServiceKind == kind).ToList();

    public object Create(Type entryType, ILoggingWrapper loggingWrapper)
    {
        ArgumentNullException.ThrowIfNull(entryType);
        ArgumentNullException.ThrowIfNull(loggingWrapper);

        var ctor = entryType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: [typeof(ILoggingWrapper)],
            modifiers: null);

        if (ctor is null)
            throw new MissingMethodException(entryType.FullName, ".ctor(ILoggingWrapper)");

        return ctor.Invoke([loggingWrapper]);
    }

    private static Type? ResolveType(string entryPoint, IReadOnlyList<Assembly> assemblies)
    {
        foreach (var asm in assemblies)
        {
            var type = asm.GetType(entryPoint, throwOnError: false, ignoreCase: false);
            if (type is not null)
                return type;
        }

        var comma = entryPoint.IndexOf(',');
        if (comma > 0)
        {
            var typeName = entryPoint[..comma].Trim();
            foreach (var asm in assemblies)
            {
                var type = asm.GetType(typeName, throwOnError: false, ignoreCase: false);
                if (type is not null)
                    return type;
            }
        }

        return null;
    }

    private bool TryProbe(
        Type entryType,
        out string engineId,
        out string? displayName,
        out PluginServiceKind serviceKind)
    {
        engineId = "";
        displayName = null;
        serviceKind = default;

        object? instance = null;
        try
        {
            instance = Create(entryType, EmptyLoggingWrapper.Instance);
            if (instance is null)
                return false;

            var culture = _localizationService.CurrentCulture;
            switch (instance)
            {
                case IOCRService ocr:
                    engineId = ocr.EngineId;
                    displayName = ocr.DisplayName.GetValue(culture);
                    serviceKind = PluginServiceKind.Ocr;
                    return !string.IsNullOrWhiteSpace(engineId);
                case ITextLayoutService layout:
                    engineId = layout.EngineId;
                    displayName = layout.DisplayName.GetValue(culture);
                    serviceKind = PluginServiceKind.TextLayout;
                    return !string.IsNullOrWhiteSpace(engineId);
                case ICaptureService capture:
                    engineId = capture.EngineId;
                    displayName = capture.DisplayName.GetValue(culture);
                    serviceKind = PluginServiceKind.Capture;
                    return !string.IsNullOrWhiteSpace(engineId);
                case ITranslatorService translator:
                    engineId = translator.EngineId;
                    displayName = translator.DisplayName.GetValue(culture);
                    serviceKind = PluginServiceKind.Translator;
                    return !string.IsNullOrWhiteSpace(engineId);
                case ITranslatorCacheService cache:
                    engineId = cache.EngineId;
                    displayName = cache.DisplayName.GetValue(culture);
                    serviceKind = PluginServiceKind.TranslatorCache;
                    return !string.IsNullOrWhiteSpace(engineId);
                case IOverlayLayoutService overlay:
                    engineId = overlay.EngineId;
                    displayName = overlay.DisplayName.GetValue(culture);
                    serviceKind = PluginServiceKind.OverlayLayout;
                    return !string.IsNullOrWhiteSpace(engineId);
                default:
                    return false;
            }
        }
        catch (Exception ex) when (ex is not InvalidPluginManifestException)
        {
            throw new InvalidPluginManifestException(InvalidPluginManifestReason.ProbeFailed, ex);
        }
        finally
        {
            (instance as IDisposable)?.Dispose();
        }
    }
}
