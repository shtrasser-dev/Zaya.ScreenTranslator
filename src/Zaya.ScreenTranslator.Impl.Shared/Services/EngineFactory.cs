using System.Reflection;
using Zaya.OCR.Services;
using Zaya.Screenshot.Services;
using Zaya.ScreenTranslator.Layout.Impl.Services;
using Zaya.ScreenTranslator.Layout.Models;
using Zaya.ScreenTranslator.Layout.Services;
using Zaya.Translator.Services;
using Zaya.TranslatorCache.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public static class EngineFactory
{
    public static IOCRService? CreateOcr(string engineId)
    {
        var engineType = typeof(IOCRService);
        foreach (var asm in PluginLoader.LoadedAssemblies)
        {
            foreach (var t in SafeGetTypes(asm))
            {
                if (t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
                    engineType.IsAssignableFrom(t))
                {
                    try
                    {
                        var instance = Activator.CreateInstance(t) as IOCRService;
                        if (instance?.EngineId == engineId)
                            return instance;
                        (instance as IDisposable)?.Dispose();
                    }
                    catch { }
                }
            }
        }
        return null;
    }

    public static ITextLayoutService? CreateTextLayout(string engineId)
    {
        var engineType = typeof(ITextLayoutService);
        foreach (var asm in PluginLoader.LoadedAssemblies)
        {
            foreach (var t in SafeGetTypes(asm))
            {
                if (t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
                    engineType.IsAssignableFrom(t))
                {
                    try
                    {
                        var instance = Activator.CreateInstance(t) as ITextLayoutService;
                        if (instance?.EngineId == engineId)
                            return instance;
                        (instance as IDisposable)?.Dispose();
                    }
                    catch { }
                }
            }
        }
        return null;
    }

    public static ICaptureService? CreateCapture(string engineId)
    {
        var captureType = typeof(ICaptureService);
        foreach (var asm in PluginLoader.LoadedAssemblies)
        {
            foreach (var t in SafeGetTypes(asm))
            {
                if (t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
                    captureType.IsAssignableFrom(t))
                {
                    try
                    {
                        var instance = Activator.CreateInstance(t) as ICaptureService;
                        if (instance?.EngineId == engineId)
                            return instance;
                        (instance as IDisposable)?.Dispose();
                    }
                    catch { }
                }
            }
        }
        return null;
    }

    public static ITranslatorService? CreateTranslator(string engineId)
    {
        if (string.Equals(engineId, NoTranslationTranslatorService.EngineIdValue, StringComparison.OrdinalIgnoreCase))
            return new NoTranslationTranslatorService();

        var engineType = typeof(ITranslatorService);
        foreach (var asm in PluginLoader.LoadedAssemblies)
        {
            foreach (var t in SafeGetTypes(asm))
            {
                if (t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
                    engineType.IsAssignableFrom(t))
                {
                    try
                    {
                        var instance = Activator.CreateInstance(t) as ITranslatorService;
                        if (instance?.EngineId == engineId)
                            return instance;
                        (instance as IDisposable)?.Dispose();
                    }
                    catch { }
                }
            }
        }
        return null;
    }

    public static ITranslatorCacheService? CreateTranslatorCache(string engineId)
    {
        if (string.Equals(engineId, NoTranslatorCacheService.EngineIdValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(engineId, "none", StringComparison.OrdinalIgnoreCase))
            return new NoTranslatorCacheService();

        var engineType = typeof(ITranslatorCacheService);
        foreach (var asm in PluginLoader.LoadedAssemblies)
        {
            foreach (var t in SafeGetTypes(asm))
            {
                if (t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
                    engineType.IsAssignableFrom(t))
                {
                    try
                    {
                        var instance = Activator.CreateInstance(t) as ITranslatorCacheService;
                        if (instance?.EngineId == engineId)
                            return instance;
                        (instance as IDisposable)?.Dispose();
                    }
                    catch { }
                }
            }
        }
        return null;
    }

    public static IOverlayLayoutService? CreateOverlayLayout(string engineId)
    {
        if (string.Equals(engineId, ScreenOverlayLayoutService.EngineIdValue, StringComparison.OrdinalIgnoreCase))
            return new ScreenOverlayLayoutService();

        var engineType = typeof(IOverlayLayoutService);
        foreach (var asm in PluginLoader.LoadedAssemblies)
        {
            foreach (var t in SafeGetTypes(asm))
            {
                if (t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
                    engineType.IsAssignableFrom(t))
                {
                    try
                    {
                        var instance = Activator.CreateInstance(t) as IOverlayLayoutService;
                        if (instance?.EngineId == engineId)
                            return instance;
                        instance?.Dispose();
                    }
                    catch { }
                }
            }
        }

        return null;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
        catch { return []; }
    }
}
