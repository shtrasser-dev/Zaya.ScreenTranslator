using System.Numerics;
using Zaya.ScreenTranslator.Layout.Models;

namespace Zaya.ScreenTranslator.Layout.Impl.Services;

/// <summary>
/// Joins OCR lines for translation and wraps a translation back into per-line boxes
/// (same rules as the host used before overlay-owned translate).
/// </summary>
internal static class OverlayParagraphWrap
{
    public static string JoinForTranslation(IReadOnlyList<OverlayItem> lines)
    {
        if (lines.Count == 0)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
                sb.Append(EndsWithSentenceTerminator(lines[i - 1].Text) ? '\n' : ' ');
            sb.Append(lines[i].Text);
        }

        return CollapseWhitespacePreservingNewlines(sb.ToString());
    }

    public static string[] WrapTranslatedToLines(string text, IReadOnlyList<OverlayItem> lines)
    {
        var lineCount = lines.Count;
        if (lineCount <= 0)
            return [];

        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (lineCount == 1)
            return [CollapseWhitespace(text.Replace('\n', ' '))];

        if (string.IsNullOrWhiteSpace(text))
            return Enumerable.Repeat(string.Empty, lineCount).ToArray();

        var groups = BuildJoinLineGroups(lines);
        var segments = text.Split('\n');
        if (segments.Length == groups.Count)
        {
            var result = new string[lineCount];
            var offset = 0;
            for (var g = 0; g < groups.Count; g++)
            {
                var groupSize = groups[g];
                var groupLines = new OverlayItem[groupSize];
                for (var i = 0; i < groupSize; i++)
                    groupLines[i] = lines[offset + i];

                var wrapped = WrapFlatTextToLines(CollapseWhitespace(segments[g]), groupLines);
                for (var i = 0; i < wrapped.Length; i++)
                    result[offset + i] = wrapped[i];
                offset += groupSize;
            }

            return result;
        }

        return WrapFlatTextToLines(CollapseWhitespace(text.Replace('\n', ' ')), lines);
    }

    private static List<int> BuildJoinLineGroups(IReadOnlyList<OverlayItem> lines)
    {
        var groups = new List<int>();
        var count = 0;
        foreach (var line in lines)
        {
            count++;
            if (!EndsWithSentenceTerminator(line.Text))
                continue;
            groups.Add(count);
            count = 0;
        }

        if (count > 0)
            groups.Add(count);

        return groups;
    }

    private static string[] WrapFlatTextToLines(string text, IReadOnlyList<OverlayItem> lines)
    {
        var lineCount = lines.Count;
        if (lineCount <= 0)
            return [];

        if (lineCount == 1)
            return [text];

        if (string.IsNullOrEmpty(text))
            return Enumerable.Repeat(string.Empty, lineCount).ToArray();

        var widths = new double[lineCount];
        var totalWidth = 0.0;
        for (var i = 0; i < lineCount; i++)
        {
            widths[i] = Math.Max(1.0, Vector2.Distance(lines[i].Bounds.P5, lines[i].Bounds.P6));
            totalWidth += widths[i];
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 1 && text.Length > 1 && !text.Contains(' '))
            return WrapByCharacters(text, widths, totalWidth);

        var totalChars = words.Sum(w => w.Length) + Math.Max(0, words.Length - 1);
        var budgets = AllocateCharBudgets(widths, totalWidth, totalChars);

        var result = new string[lineCount];
        var wi = 0;
        for (var li = 0; li < lineCount; li++)
        {
            if (wi >= words.Length)
            {
                result[li] = string.Empty;
                continue;
            }

            if (li == lineCount - 1)
            {
                result[li] = string.Join(" ", words.Skip(wi));
                break;
            }

            var budget = SoftEarlierBudget(budgets[li]);
            if (li == lineCount - 2)
                budget = Math.Max(budget, RemainingChars(words, wi) - budgets[^1]);

            var start = wi;
            var used = 0;
            while (wi < words.Length)
            {
                var w = words[wi];
                var need = w.Length + (wi > start ? 1 : 0);
                if (wi > start && used + need > budget)
                    break;
                used += need;
                wi++;
                if (used >= budget)
                    break;
            }

            if (wi == start && wi < words.Length)
                wi++;

            result[li] = string.Join(" ", words.Skip(start).Take(wi - start));
        }

        return result;
    }

    private static string[] WrapByCharacters(string text, double[] widths, double totalWidth)
    {
        var lineCount = widths.Length;
        var budgets = AllocateCharBudgets(widths, totalWidth, text.Length);
        var result = new string[lineCount];
        var offset = 0;
        for (var li = 0; li < lineCount; li++)
        {
            if (offset >= text.Length)
            {
                result[li] = string.Empty;
                continue;
            }

            if (li == lineCount - 1)
            {
                result[li] = text[offset..];
                break;
            }

            var takeBudget = SoftEarlierBudget(budgets[li]);
            if (li == lineCount - 2)
                takeBudget = Math.Max(takeBudget, text.Length - offset - budgets[^1]);

            var take = Math.Clamp(takeBudget, 1, text.Length - offset);
            result[li] = text.Substring(offset, take);
            offset += take;
        }

        return result;
    }

    private static int RemainingChars(string[] words, int startIndex)
    {
        if (startIndex >= words.Length)
            return 0;

        var chars = 0;
        for (var i = startIndex; i < words.Length; i++)
        {
            if (i > startIndex)
                chars++;
            chars += words[i].Length;
        }

        return chars;
    }

    private static int[] AllocateCharBudgets(double[] widths, double totalWidth, int totalChars)
    {
        var lineCount = widths.Length;
        var budgets = new int[lineCount];
        if (totalChars <= 0 || lineCount == 0)
            return budgets;

        if (lineCount == 1)
        {
            budgets[0] = totalChars;
            return budgets;
        }

        var last = lineCount - 1;
        var lastMax = Math.Max(1, (int)Math.Floor(totalChars * (widths[last] / totalWidth) * 1.10));
        lastMax = Math.Min(lastMax, Math.Max(1, totalChars - (lineCount - 1)));

        var earlierChars = totalChars - lastMax;
        var earlierWidth = 0.0;
        for (var i = 0; i < last; i++)
            earlierWidth += widths[i];
        if (earlierWidth < 1)
            earlierWidth = 1;

        var assigned = 0;
        for (var i = 0; i < last; i++)
        {
            if (i == last - 1)
            {
                budgets[i] = Math.Max(0, earlierChars - assigned);
                break;
            }

            budgets[i] = Math.Max(1, (int)Math.Round(earlierChars * (widths[i] / earlierWidth)));
            assigned += budgets[i];
        }

        budgets[last] = lastMax;
        return budgets;
    }

    private static int SoftEarlierBudget(int budget) =>
        Math.Max(budget, (int)Math.Ceiling(budget * 1.25));

    private static bool EndsWithSentenceTerminator(string? lineText)
    {
        if (string.IsNullOrWhiteSpace(lineText))
            return false;

        var t = lineText.TrimEnd();
        while (t.Length > 0 && IsTrailingCloser(t[^1]))
            t = t[..^1].TrimEnd();
        if (t.Length == 0)
            return false;

        if (t.EndsWith("...", StringComparison.Ordinal) || t.EndsWith("…", StringComparison.Ordinal))
            return true;

        var c = t[^1];
        return c is '.' or '!' or '?';
    }

    private static bool IsTrailingCloser(char c)
        => c is '"' or '\'' or '”' or '’' or ')' or ']' or '}' or '»' or '〉' or '》';

    private static string CollapseWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        var sb = new System.Text.StringBuilder(text.Length);
        var prevSpace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace)
                    sb.Append(' ');
                prevSpace = true;
            }
            else
            {
                sb.Append(ch);
                prevSpace = false;
            }
        }

        return sb.ToString().Trim();
    }

    private static string CollapseWhitespacePreservingNewlines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new System.Text.StringBuilder(text.Length);
        var wrote = false;
        foreach (var line in lines)
        {
            var collapsed = CollapseWhitespace(line);
            if (!wrote)
            {
                sb.Append(collapsed);
                wrote = true;
            }
            else
            {
                sb.Append('\n');
                sb.Append(collapsed);
            }
        }

        return sb.ToString().Trim('\n');
    }
}
