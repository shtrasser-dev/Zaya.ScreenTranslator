using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

public sealed class LocalizationService : ObservableObject, ILocalizationService
{
    private static readonly IReadOnlyList<string> DefaultSupportedUiCultures =
        ["en", "ru", "de", "fr", "ja", "ko", "pl", "pt", "tr", "uk", "zh-Hans"];

    private readonly Properties.Resources _resources = Properties.Resources.Instance;

    public IReadOnlyList<string> SupportedUiCultures { get; } = DefaultSupportedUiCultures;

    public CultureInfo CurrentCulture { get; private set; } = new("en");

    public string this[string key]
    {
        get
        {
            return _resources.GetString(key, CurrentCulture);
        }
    }

    public string FormatExceptionMessage(Exception ex)
        => ex is LocalizedException lex
            ? lex.GetLocalizedMessage(CurrentCulture)
            : ex.Message;

    public string FormatStoppedWithError(Exception ex)
        => FormatStoppedWithError(FormatExceptionMessage(ex));

    public string FormatStoppedWithError(string detail)
    {
        var error = string.Format(CurrentCulture, this[LocalizationConstants.Status.Error], detail);
        return $"{this[LocalizationConstants.Status.Stopped]} {error}";
    }

    public string ResolveSystemUiCulture()
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

    public string ResolveSystemTargetLanguage()
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

    public bool IsSupportedUiCulture(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        return SupportedUiCultures.Any(s =>
            string.Equals(s, code, StringComparison.OrdinalIgnoreCase)
            || code.StartsWith(s + "-", StringComparison.OrdinalIgnoreCase));
    }

    private bool TryMatchSupported(CultureInfo culture, out string code)
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

        if (culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            var name = culture.Name;
            var preferred = name.Contains("Hant", StringComparison.OrdinalIgnoreCase)
                            || name.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
                            || name.Equals("zh-HK", StringComparison.OrdinalIgnoreCase)
                            || name.Equals("zh-MO", StringComparison.OrdinalIgnoreCase)
                ? "zh-Hant"
                : "zh-Hans";

            if (SupportedUiCultures.Any(s => string.Equals(s, preferred, StringComparison.OrdinalIgnoreCase)))
            {
                code = preferred;
                return true;
            }

            if (SupportedUiCultures.Any(s => string.Equals(s, "zh-Hans", StringComparison.OrdinalIgnoreCase)))
            {
                code = "zh-Hans";
                return true;
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
        CurrentCulture = new CultureInfo(cultureCode);
        CultureInfo.CurrentUICulture = CurrentCulture;

        OnPropertyChanged(nameof(CurrentCulture));
        OnPropertyChanged("Item");
        OnPropertyChanged("Item[]");
        OnPropertyChanged(string.Empty);
    }
}
