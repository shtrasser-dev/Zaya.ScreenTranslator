using System.Text;
using Zaya.OCR.Models;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// Joins OCR paragraph lines for the translator. Consecutive lines are joined with
/// a space, except after a line that ends with sentence punctuation (<c>.</c>,
/// <c>!</c>, <c>?</c>, <c>...</c>, <c>…</c>) — then <c>\n</c> is kept so the translator
/// sees a sentence break.
/// </summary>
public sealed class TranslationBatchBuilder : ITranslationBatchBuilder
{
    public TranslationBatch Build(IReadOnlyList<ITextParagraph> paragraphs)
    {
        var paraList = new List<ITextParagraph>();
        var texts = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            var text = JoinParagraphForTranslation(paragraph);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            paraList.Add(paragraph);
            texts.Add(text);
        }

        return paraList.Count == 0
            ? TranslationBatch.Empty
            : new TranslationBatch(paraList, texts);
    }

    private static string JoinParagraphForTranslation(ITextParagraph paragraph)
    {
        var lines = paragraph.Lines;
        if (lines.Count == 0)
            return CollapseWhitespace(paragraph.Text.Replace('\r', ' ').Replace('\n', ' '));

        var sb = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
                sb.Append(EndsWithSentenceTerminator(lines[i - 1].Text) ? '\n' : ' ');
            sb.Append(lines[i].Text);
        }

        return CollapseWhitespacePreservingNewlines(sb.ToString());
    }

    /// <summary>
    /// True when the OCR line ends with sentence-ending punctuation
    /// (<c>.</c>, <c>!</c>, <c>?</c>, <c>...</c>, <c>…</c>), ignoring trailing quotes/brackets.
    /// </summary>
    internal static bool EndsWithSentenceTerminator(string? lineText)
    {
        if (string.IsNullOrWhiteSpace(lineText))
            return false;

        var t = lineText.TrimEnd();
        while (t.Length > 0 && IsTrailingCloser(t[^1]))
            t = t[..^1].TrimEnd();

        if (t.Length == 0)
            return false;

        if (t.EndsWith("...", StringComparison.Ordinal) || t.EndsWith('…'))
            return true;

        var last = t[^1];
        return last is '.' or '!' or '?';
    }

    private static bool IsTrailingCloser(char c) => c is '"' or '\'' or '\u2019' or '\u201D'
        or ')' or ']' or '}' or '\u00BB';

    private static string CollapseWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var chars = text.Trim().ToCharArray();
        var sb = new StringBuilder(chars.Length);
        var prevSpace = false;
        foreach (var c in chars)
        {
            if (char.IsWhiteSpace(c))
            {
                if (prevSpace)
                    continue;
                sb.Append(' ');
                prevSpace = true;
            }
            else
            {
                sb.Append(c);
                prevSpace = false;
            }
        }

        return sb.ToString();
    }

    private static string CollapseWhitespacePreservingNewlines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = text.Split('\n');
        var sb = new StringBuilder(text.Length);
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
