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
