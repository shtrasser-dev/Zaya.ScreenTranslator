using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class LocalizationService : ObservableObject
{
    public static LocalizationService Instance { get; } = new();

    /// <summary>UI cultures that have resource satellites in this app.</summary>
    public static IReadOnlyList<string> SupportedUiCultures { get; } = ["en", "ru"];

    private readonly Properties.Resources _resources = Properties.Resources.Instance;

    public CultureInfo CurrentCulture { get; private set; } = new("en");

    public string this[string key]
    {
        get
        {
            var result = _resources.GetString(key, CurrentCulture);
            if (key == "Btn_Start")
                Debug.WriteLine($"[Loc] key={key}, culture={CurrentCulture.Name}, result='{result}'");
            return result;
        }
    }

    /// <summary>
    /// Picks the Windows UI language when a translation exists; otherwise English.
    /// </summary>
    public static string ResolveSystemUiCulture()
    {
        foreach (var culture in new[]
                 {
                     CultureInfo.CurrentUICulture,
                     CultureInfo.InstalledUICulture,
                     CultureInfo.CurrentCulture,
                 })
        {
            if (TryMatchSupported(culture, out var code))
                return code;
        }

        return "en";
    }

    /// <summary>
    /// Picks a translation target from <see cref="Zaya.Primitives.Languages.All"/>
    /// based on the Windows UI / system culture; otherwise English.
    /// </summary>
    public static string ResolveSystemTargetLanguage()
    {
        foreach (var culture in new[]
                 {
                     CultureInfo.CurrentUICulture,
                     CultureInfo.InstalledUICulture,
                     CultureInfo.CurrentCulture,
                 })
        {
            if (TryMatchTargetLanguage(culture, out var code))
                return code;
        }

        return "en";
    }

    public static bool IsSupportedUiCulture(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        return SupportedUiCultures.Any(s =>
            string.Equals(s, code, StringComparison.OrdinalIgnoreCase)
            || code.StartsWith(s + "-", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryMatchSupported(CultureInfo culture, out string code)
    {
        foreach (var candidate in new[] { culture.Name, culture.TwoLetterISOLanguageName })
        {
            foreach (var supported in SupportedUiCultures)
            {
                if (string.Equals(supported, candidate, StringComparison.OrdinalIgnoreCase)
                    || candidate.StartsWith(supported + "-", StringComparison.OrdinalIgnoreCase))
                {
                    code = supported;
                    return true;
                }
            }
        }

        code = "en";
        return false;
    }

    private static bool TryMatchTargetLanguage(CultureInfo culture, out string code)
    {
        for (var c = culture; !Equals(c, CultureInfo.InvariantCulture); c = c.Parent)
        {
            if (Zaya.Primitives.Languages.Find(c.Name) is { } byName)
            {
                code = byName.Value;
                return true;
            }

            if (c.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
            {
                var name = c.Name;
                code = name.Contains("Hant", StringComparison.OrdinalIgnoreCase)
                       || name.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
                       || name.Equals("zh-HK", StringComparison.OrdinalIgnoreCase)
                       || name.Equals("zh-MO", StringComparison.OrdinalIgnoreCase)
                    ? "zh-Hant"
                    : "zh-Hans";
                return true;
            }

            if (Zaya.Primitives.Languages.Find(c.TwoLetterISOLanguageName) is { } byIso)
            {
                code = byIso.Value;
                return true;
            }

            if (string.IsNullOrEmpty(c.Parent.Name) || ReferenceEquals(c.Parent, c))
                break;
        }

        code = "en";
        return false;
    }

    public void SetCulture(string cultureCode)
    {
        Debug.WriteLine($"[SetCulture] Switching from {CurrentCulture.Name} to {cultureCode}");
        CurrentCulture = new CultureInfo(cultureCode);
        CultureInfo.CurrentUICulture = CurrentCulture;

        // Avalonia / WPF indexer bindings listen for different names.
        OnPropertyChanged(nameof(CurrentCulture));
        OnPropertyChanged("Item");
        OnPropertyChanged("Item[]");
        OnPropertyChanged(string.Empty);
    }
}
