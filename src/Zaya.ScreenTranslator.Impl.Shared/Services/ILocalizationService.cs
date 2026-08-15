using System.ComponentModel;
using System.Globalization;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// UI localization and culture switching for the host.
/// </summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    IReadOnlyList<string> SupportedUiCultures { get; }

    CultureInfo CurrentCulture { get; }

    string this[string key] { get; }

    string ResolveSystemUiCulture();

    string ResolveSystemTargetLanguage();

    bool IsSupportedUiCulture(string? code);

    string FormatExceptionMessage(Exception ex);
    string FormatStoppedWithError(Exception ex);
    string FormatStoppedWithError(string detail);
    void SetCulture(string cultureCode);
}
