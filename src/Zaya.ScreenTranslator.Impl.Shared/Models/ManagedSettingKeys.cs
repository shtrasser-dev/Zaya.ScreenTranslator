using Zaya.Primitives;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>
/// Setting keys that ScreenTranslator owns and injects for plugins (not shown in UI).
/// </summary>
public static class ManagedSettingKeys
{
    public const string CacheDirectory = "cacheDirectory";

    /// <summary>
    /// Injected from app-level <see cref="ScreenTranslatorProfile.TargetLanguage"/>.
    /// Matches Google/Yandex plugin setting key.
    /// </summary>
    public const string TargetLanguage = "targetLanguage";

    /// <summary>
    /// Host-injected HWND for overlay sessions. Must never be persisted.
    /// </summary>
    public const string TargetWindowHandle = "targetWindowHandle";

    public static bool IsHostManaged(SettingDescriptor descriptor)
        => descriptor.Key is CacheDirectory or TargetLanguage;

    public static bool IsEphemeralHostKey(string key)
        => key is TargetWindowHandle;

    /// <summary>
    /// Copies stored plugin settings and overwrites host-managed keys when the plugin declares them.
    /// </summary>
    public static Dictionary<string, object> PrepareForEngine(
        string engineId,
        IReadOnlyList<SettingDescriptor> descriptors,
        IReadOnlyDictionary<string, object> stored,
        string? targetLanguage = null)
    {
        var result = new Dictionary<string, object>(stored);

        if (descriptors.Any(d => d.Key == CacheDirectory))
        {
            var path = AppPaths.GetPluginCacheDirectory(engineId);
            Directory.CreateDirectory(path);
            result[CacheDirectory] = path;
        }

        if (!string.IsNullOrWhiteSpace(targetLanguage) &&
            descriptors.Any(d => d.Key == TargetLanguage))
        {
            result[TargetLanguage] = targetLanguage;
        }

        return result;
    }
}
