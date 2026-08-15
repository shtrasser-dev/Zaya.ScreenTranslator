using System.Globalization;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// Loads/saves capture and ignore regions from the active profile's screenTranslator bag.
/// </summary>
public sealed class CaptureRegionsStore : ICaptureRegionsStore
{
    public string CaptureKey => SettingsConstants.CaptureRegions;
    public string IgnoreKey => SettingsConstants.IgnoreRegions;
    public string ColX => "x";
    public string ColY => "y";
    public string ColWidth => "width";
    public string ColHeight => "height";

    public CaptureRegionsConfig Load(IApplicationProfile profile)
    {
        if (!profile.Settings.TryGetValue(ScreenTranslatorSettingDescriptors.StKey, out var st) || st is null)
            return new CaptureRegionsConfig();

        return new CaptureRegionsConfig
        {
            CaptureRegions = ReadRects(st, CaptureKey),
            IgnoreRegions = ReadRects(st, IgnoreKey),
        };
    }

    public void Save(IApplicationProfile profile, CaptureRegionsConfig config)
    {
        if (!profile.Settings.TryGetValue(ScreenTranslatorSettingDescriptors.StKey, out var st) || st is null)
        {
            st = new Dictionary<string, object>();
            profile.Settings[ScreenTranslatorSettingDescriptors.StKey] = st;
        }

        st[CaptureKey] = WriteRects(config.CaptureRegions);
        st[IgnoreKey] = WriteRects(config.IgnoreRegions);
    }

    private List<object> WriteRects(IReadOnlyList<PercentRect> rects)
    {
        var list = new List<object>(rects.Count);
        foreach (var r in rects)
        {
            var c = r.Clamp();
            if (c.IsEmpty)
                continue;
            list.Add(new Dictionary<string, object>
            {
                [ColX] = Round(c.X),
                [ColY] = Round(c.Y),
                [ColWidth] = Round(c.Width),
                [ColHeight] = Round(c.Height),
            });
        }

        return list;
    }

    private IReadOnlyList<PercentRect> ReadRects(Dictionary<string, object> st, string key)
    {
        if (!st.TryGetValue(key, out var raw) || raw is null)
            return [];

        if (raw is string || raw is not System.Collections.IEnumerable enumerable)
            return [];

        var result = new List<PercentRect>();
        foreach (var item in enumerable)
        {
            if (item is not Dictionary<string, object> row)
                continue;

            var rect = new PercentRect(
                ReadDouble(row, ColX),
                ReadDouble(row, ColY),
                ReadDouble(row, ColWidth),
                ReadDouble(row, ColHeight)).Clamp();
            if (!rect.IsEmpty)
                result.Add(rect);
        }

        return result;
    }

    private static double ReadDouble(Dictionary<string, object> row, string key)
    {
        if (!row.TryGetValue(key, out var raw) || raw is null)
            return 0;

        return raw switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal m => (double)m,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) => p,
            _ => 0,
        };
    }

    private static double Round(double v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);
}
