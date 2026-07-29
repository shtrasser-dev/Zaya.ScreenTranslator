using System.Drawing;
using Zaya.ScreenTranslator.Impl.Layout.Models;

namespace Zaya.ScreenTranslator.Impl.Layout.Services;

/// <summary>
/// Keeps overlay boxes steady when OCR returns nearly-identical text with jittery bounds.
/// </summary>
internal sealed class OverlayBoundsStabilizer
{
    /// <summary>Reuse previous bounds if centers are within this distance (capture pixels).</summary>
    private const int SnapThresholdPx = 14;

    /// <summary>Blend toward new bounds when somewhat farther, still same text.</summary>
    private const int BlendThresholdPx = 40;

    private List<OverlayItem> _previous = [];

    public IReadOnlyList<OverlayItem> Stabilize(IReadOnlyList<OverlayItem> incoming)
    {
        if (incoming.Count == 0)
        {
            _previous = [];
            return incoming;
        }

        if (_previous.Count == 0)
        {
            _previous = CloneAll(incoming);
            return _previous;
        }

        var result = new List<OverlayItem>(incoming.Count);
        var used = new bool[_previous.Count];

        foreach (var item in incoming)
        {
            var bestIndex = -1;
            var bestDist = int.MaxValue;

            for (var i = 0; i < _previous.Count; i++)
            {
                if (used[i])
                    continue;
                if (!TextKeyEquals(item.Text, _previous[i].Text))
                    continue;

                var dist = CenterDistanceSq(item.Bounds, _previous[i].Bounds);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                result.Add(Clone(item));
                continue;
            }

            used[bestIndex] = true;
            var prev = _previous[bestIndex];
            var distPx = Math.Sqrt(bestDist);

            if (distPx <= SnapThresholdPx)
            {
                // Same text, tiny OCR jitter — keep previous geometry.
                result.Add(new OverlayItem { Text = item.Text, Bounds = prev.Bounds });
            }
            else if (distPx <= BlendThresholdPx)
            {
                result.Add(new OverlayItem
                {
                    Text = item.Text,
                    Bounds = Lerp(prev.Bounds, item.Bounds, 0.25),
                });
            }
            else
            {
                result.Add(Clone(item));
            }
        }

        _previous = CloneAll(result);
        return _previous;
    }

    public void Reset() => _previous = [];

    private static bool TextKeyEquals(string a, string b)
        => string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

    private static string Normalize(string s)
        => string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static int CenterDistanceSq(Rectangle a, Rectangle b)
    {
        var ax = a.X + a.Width / 2;
        var ay = a.Y + a.Height / 2;
        var bx = b.X + b.Width / 2;
        var by = b.Y + b.Height / 2;
        var dx = ax - bx;
        var dy = ay - by;
        return dx * dx + dy * dy;
    }

    private static Rectangle Lerp(Rectangle from, Rectangle to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        static int Mix(int a, int b, double tt) => (int)Math.Round(a + (b - a) * tt);
        return new Rectangle(
            Mix(from.X, to.X, t),
            Mix(from.Y, to.Y, t),
            Math.Max(1, Mix(from.Width, to.Width, t)),
            Math.Max(1, Mix(from.Height, to.Height, t)));
    }

    private static OverlayItem Clone(OverlayItem item)
        => new() { Text = item.Text, Bounds = item.Bounds };

    private static List<OverlayItem> CloneAll(IReadOnlyList<OverlayItem> items)
    {
        var list = new List<OverlayItem>(items.Count);
        foreach (var item in items)
            list.Add(Clone(item));
        return list;
    }
}
