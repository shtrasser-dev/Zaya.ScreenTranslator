using System.Globalization;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

/// <summary>Validates integer setting text against descriptor min/max.</summary>
internal static class IntegerSettingValidation
{
    public static bool TryParse(
        string? text,
        IntegerSettingDescriptor desc,
        out int value,
        out string? errorMessage,
        CultureInfo? culture = null)
    {
        culture ??= LocalizationService.Instance.CurrentCulture;
        value = 0;
        errorMessage = null;

        if (!int.TryParse(text, NumberStyles.Integer, culture, out value)
            && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            errorMessage = FormatRangeError(desc, culture);
            return false;
        }

        if (desc.MinValue is int min && value < min)
        {
            errorMessage = FormatRangeError(desc, culture);
            return false;
        }

        if (desc.MaxValue is int max && value > max)
        {
            errorMessage = FormatRangeError(desc, culture);
            return false;
        }

        return true;
    }

    public static string FormatRangeError(IntegerSettingDescriptor desc, CultureInfo culture)
    {
        var min = desc.MinValue ?? int.MinValue;
        var max = desc.MaxValue;
        var loc = LocalizationService.Instance;

        if (max is null || max == int.MaxValue)
        {
            return string.Format(
                culture,
                loc[LocalizationConstants.Validation.IntegerMinOnly],
                min);
        }

        return string.Format(
            culture,
            loc[LocalizationConstants.Validation.IntegerRange],
            min,
            max.Value);
    }
}
