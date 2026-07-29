using System.Diagnostics;
using System.Text.RegularExpressions;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Host-side text filter applied after layout and before translation.
/// </summary>
public sealed class TextFilterSession
{
    private readonly int _minLength;
    private readonly List<CompiledRule> _stripRules;
    private readonly List<CompiledRule> _skipRules;

    private TextFilterSession(int minLength, List<CompiledRule> stripRules, List<CompiledRule> skipRules)
    {
        _minLength = minLength;
        _stripRules = stripRules;
        _skipRules = skipRules;
    }

    public static TextFilterSession Create(SettingDescriptorList settings)
    {
        var minLength = settings.GetValueAsInt(ScreenTranslatorSettingDescriptors.FilterMinLength);
        var strip = new List<CompiledRule>();
        var skip = new List<CompiledRule>();

        foreach (var row in settings.GetValueAsTable(ScreenTranslatorSettingDescriptors.FilterRules))
        {
            if (!row.GetValueAsBool(ScreenTranslatorSettingDescriptors.RuleEnabled))
                continue;

            var pattern = row.GetValueAsString(ScreenTranslatorSettingDescriptors.RulePattern);
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            var isRegex = row.GetValueAsBool(ScreenTranslatorSettingDescriptors.RuleIsRegex);
            var ignoreCase = row.GetValueAsBool(ScreenTranslatorSettingDescriptors.RuleIgnoreCase);
            var action = row.GetValueAsString(ScreenTranslatorSettingDescriptors.RuleAction);

            Regex? regex = null;
            if (isRegex)
            {
                try
                {
                    var options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
                    if (ignoreCase)
                        options |= RegexOptions.IgnoreCase;
                    regex = new Regex(pattern, options);
                }
                catch (ArgumentException ex)
                {
                    Debug.WriteLine($"[TextFilter] Invalid regex '{pattern}': {ex.Message}");
                    continue;
                }
            }

            var rule = new CompiledRule(pattern, regex, ignoreCase);
            if (string.Equals(action, ScreenTranslatorSettingDescriptors.ActionStrip, StringComparison.OrdinalIgnoreCase))
                strip.Add(rule);
            else
                skip.Add(rule);
        }

        return new TextFilterSession(minLength, strip, skip);
    }

    public IReadOnlyList<string> Apply(IReadOnlyList<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var result = new List<string>(texts.Count);
        foreach (var original in texts)
        {
            if (string.IsNullOrWhiteSpace(original))
                continue;

            var text = original;
            foreach (var rule in _stripRules)
                text = Strip(text, rule);

            text = text.Trim();
            if (text.Length == 0)
                continue;

            if (text.Length < _minLength)
                continue;

            if (_skipRules.Any(r => Matches(text, r)))
                continue;

            result.Add(text);
        }

        return result;
    }

    private static string Strip(string text, CompiledRule rule)
    {
        if (rule.Regex is not null)
            return rule.Regex.Replace(text, string.Empty);

        var comparison = rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return text.Replace(rule.Pattern, string.Empty, comparison);
    }

    private static bool Matches(string text, CompiledRule rule)
    {
        if (rule.Regex is not null)
            return rule.Regex.IsMatch(text);

        var comparison = rule.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return text.Contains(rule.Pattern, comparison);
    }

    private sealed record CompiledRule(string Pattern, Regex? Regex, bool IgnoreCase);
}
