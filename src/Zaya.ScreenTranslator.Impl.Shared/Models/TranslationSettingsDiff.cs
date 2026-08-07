using System.Collections;
using Zaya.ScreenTranslator.Impl.Shared.Constants;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>Deep-clones profile settings trees for change detection.</summary>
internal static class SettingsSnapshot
{
    public static Dictionary<string, Dictionary<string, object>> Clone(
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
                copy[key] = CloneValue(value)!;
            }
            result[pluginId] = copy;
        }
        return result;
    }

    private static object? CloneValue(object? value)
    {
        if (value is null)
            return null;

        if (value is string or bool or int or long or double or float or decimal)
            return value;

        if (value is Dictionary<string, object> dict)
        {
            var copy = new Dictionary<string, object>(dict.Count);
            foreach (var (k, v) in dict)
                copy[k] = CloneValue(v)!;
            return copy;
        }

        if (value is IList list)
        {
            var copy = new List<object?>(list.Count);
            foreach (var item in list)
                copy.Add(CloneValue(item));
            return copy;
        }

        return value;
    }
}

/// <summary>Maps settings diffs to pipeline modules that need session recreation.</summary>
internal static class TranslationSettingsDiff
{
    public static TranslationModuleKind Detect(
        Dictionary<string, Dictionary<string, object>>? previous,
        Dictionary<string, Dictionary<string, object>> current,
        string? previousTargetLanguage,
        string? currentTargetLanguage)
    {
        previous ??= new Dictionary<string, Dictionary<string, object>>();
        var modules = TranslationModuleKind.None;

        var prevSt = GetPlugin(previous, ScreenTranslatorSettingDescriptors.StKey);
        var currSt = GetPlugin(current, ScreenTranslatorSettingDescriptors.StKey);

        if (!ScalarEqual(Get(prevSt, ScreenTranslatorSettingDescriptors.TargetProcess),
                Get(currSt, ScreenTranslatorSettingDescriptors.TargetProcess)))
            return TranslationModuleKind.FullRestart;

        var prevOcr = ResolveEngineId(prevSt, ScreenTranslatorSettingDescriptors.Ocr, SettingsConstants.EngineDefaults.Ocr);
        var currOcr = ResolveEngineId(currSt, ScreenTranslatorSettingDescriptors.Ocr, SettingsConstants.EngineDefaults.Ocr);
        if (!string.Equals(prevOcr, currOcr, StringComparison.Ordinal)
            || !PluginEqual(previous, current, prevOcr)
            || !PluginEqual(previous, current, currOcr))
            modules |= TranslationModuleKind.Ocr;

        var prevCapture = ResolveEngineId(prevSt, ScreenTranslatorSettingDescriptors.Capture, SettingsConstants.EngineDefaults.Capture);
        var currCapture = ResolveEngineId(currSt, ScreenTranslatorSettingDescriptors.Capture, SettingsConstants.EngineDefaults.Capture);
        if (!string.Equals(prevCapture, currCapture, StringComparison.Ordinal)
            || !PluginEqual(previous, current, prevCapture)
            || !PluginEqual(previous, current, currCapture))
            modules |= TranslationModuleKind.Capture;

        var prevLayout = ResolveEngineId(prevSt, ScreenTranslatorSettingDescriptors.TextLayout, SettingsConstants.EngineDefaults.TextLayout);
        var currLayout = ResolveEngineId(currSt, ScreenTranslatorSettingDescriptors.TextLayout, SettingsConstants.EngineDefaults.TextLayout);
        if (!string.Equals(prevLayout, currLayout, StringComparison.Ordinal)
            || !PluginEqual(previous, current, prevLayout)
            || !PluginEqual(previous, current, currLayout))
            modules |= TranslationModuleKind.TextLayout;

        var prevTranslator = ResolveEngineId(prevSt, ScreenTranslatorSettingDescriptors.Translator, SettingsConstants.EngineDefaults.Translator);
        var currTranslator = ResolveEngineId(currSt, ScreenTranslatorSettingDescriptors.Translator, SettingsConstants.EngineDefaults.Translator);
        var prevCache = ResolveEngineId(prevSt, ScreenTranslatorSettingDescriptors.TranslatorCache, SettingsConstants.EngineDefaults.TranslatorCache);
        var currCache = ResolveEngineId(currSt, ScreenTranslatorSettingDescriptors.TranslatorCache, SettingsConstants.EngineDefaults.TranslatorCache);
        var translatorChanged =
            !string.Equals(prevTranslator, currTranslator, StringComparison.Ordinal)
            || !PluginEqual(previous, current, prevTranslator)
            || !PluginEqual(previous, current, currTranslator)
            || !string.Equals(prevCache, currCache, StringComparison.Ordinal)
            || !PluginEqual(previous, current, prevCache)
            || !PluginEqual(previous, current, currCache)
            || !string.Equals(
                previousTargetLanguage ?? string.Empty,
                currentTargetLanguage ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);

        if (translatorChanged)
            modules |= TranslationModuleKind.Translator;

        var prevOverlay = ResolveEngineId(prevSt, ScreenTranslatorSettingDescriptors.OverlayLayout, SettingsConstants.EngineDefaults.OverlayLayout);
        var currOverlay = ResolveEngineId(currSt, ScreenTranslatorSettingDescriptors.OverlayLayout, SettingsConstants.EngineDefaults.OverlayLayout);
        if (!string.Equals(prevOverlay, currOverlay, StringComparison.Ordinal)
            || !PluginEqual(previous, current, prevOverlay)
            || !PluginEqual(previous, current, currOverlay))
            modules |= TranslationModuleKind.Overlay;

        return modules;
    }

    /// <summary>
    /// Engine ids may be absent from the ST dict while UI still shows descriptor defaults —
    /// without this fallback plugin bags are never compared and module refresh is skipped.
    /// </summary>
    private static string ResolveEngineId(
        Dictionary<string, object> stSettings,
        string key,
        string defaultId)
    {
        var raw = Get(stSettings, key);
        if (raw is string s && !string.IsNullOrWhiteSpace(s))
            return s;
        if (raw is not null)
        {
            var text = Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return defaultId;
    }

    private static Dictionary<string, object> GetPlugin(
        Dictionary<string, Dictionary<string, object>> settings,
        string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            return new Dictionary<string, object>();
        return settings.TryGetValue(pluginId, out var dict) ? dict : new Dictionary<string, object>();
    }

    private static object? Get(Dictionary<string, object> settings, string key)
        => settings.TryGetValue(key, out var value) ? value : null;

    private static bool PluginEqual(
        Dictionary<string, Dictionary<string, object>> left,
        Dictionary<string, Dictionary<string, object>> right,
        string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId)
            || string.Equals(pluginId, ScreenTranslatorSettingDescriptors.StKey, StringComparison.Ordinal))
            return true;

        return ValuesEqual(GetPlugin(left, pluginId), GetPlugin(right, pluginId));
    }

    private static bool ScalarEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null)
            return false;
        return string.Equals(
            Convert.ToString(left, System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToString(right, System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null)
            return false;

        if (left is IDictionary leftMap && right is IDictionary rightMap)
        {
            if (leftMap.Count != rightMap.Count)
                return false;
            foreach (DictionaryEntry entry in leftMap)
            {
                if (entry.Key is not string key)
                    return false;
                if (!rightMap.Contains(key) || !ValuesEqual(entry.Value, rightMap[key]))
                    return false;
            }
            return true;
        }

        if (left is IEnumerable leftSeq and not string
            && right is IEnumerable rightSeq and not string
            && left is not IDictionary
            && right is not IDictionary)
        {
            using var le = leftSeq.Cast<object?>().GetEnumerator();
            using var re = rightSeq.Cast<object?>().GetEnumerator();
            while (true)
            {
                var lm = le.MoveNext();
                var rm = re.MoveNext();
                if (lm != rm)
                    return false;
                if (!lm)
                    return true;
                if (!ValuesEqual(le.Current, re.Current))
                    return false;
            }
        }

        if (IsNumeric(left) && IsNumeric(right))
            return Convert.ToDecimal(left) == Convert.ToDecimal(right);

        return string.Equals(
            Convert.ToString(left, System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToString(right, System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static bool IsNumeric(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
