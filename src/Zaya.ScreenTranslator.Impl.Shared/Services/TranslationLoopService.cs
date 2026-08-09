using System.Diagnostics;
using Avalonia.Threading;
using Zaya.OCR.Models;
using Zaya.OCR.Services;
using Zaya.Primitives;
using Zaya.Screenshot.Models;
using Zaya.Screenshot.Services;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Layout.Models;
using Zaya.ScreenTranslator.Layout.Services;
using Zaya.Translator.Services;
using Zaya.TranslatorCache.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class TranslationLoopService
{
    internal async Task RunAsync(
        TranslationLoopRuntime runtime,
        Func<IApplicationProfile?> getProfile,
        ITranslationModuleRefresh? moduleRefresh,
        CancellationToken ct,
        Action<string> onTextUpdated,
        Action<(string Text, bool IsError)> onStatus,
        Action<double, double, double>? onTimings = null,
        Action<IReadOnlyList<(string Source, string Translation)>>? onTranslatedPairs = null)
    {
        try
        {
            await RunLoopAsync(
                runtime,
                getProfile,
                moduleRefresh,
                ct,
                onTextUpdated,
                onStatus,
                onTimings,
                onTranslatedPairs);
        }
        finally
        {
            DisposeQuietly(runtime.CaptureSession);
            DisposeQuietly(runtime.OcrSession);
            DisposeQuietly(runtime.LayoutSession);
            DisposeQuietly(runtime.TranslatorSession);
            // Overlay session is owned by the coordinator (may be swapped mid-run).
        }
    }

    private static void DisposeQuietly(IDisposable? disposable)
    {
        try { disposable?.Dispose(); } catch { /* ignore */ }
    }

    private static async Task RunLoopAsync(
        TranslationLoopRuntime runtime,
        Func<IApplicationProfile?> getProfile,
        ITranslationModuleRefresh? moduleRefresh,
        CancellationToken ct,
        Action<string> onTextUpdated,
        Action<(string Text, bool IsError)> onStatus,
        Action<double, double, double>? onTimings,
        Action<IReadOnlyList<(string Source, string Translation)>>? onTranslatedPairs)
    {
        var captureTimes = new Queue<double>();
        var ocrTimes = new Queue<double>();
        var translatorTimes = new Queue<double>();
        const int windowSize = 10;

        onStatus((AppConstants.LoopStatus.Running, false));

        string? fatalError = null;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (moduleRefresh is not null)
                    await moduleRefresh.ApplyPendingAsync(runtime, ct).ConfigureAwait(false);

                var profile = getProfile();
                if (profile is null)
                {
                    await Task.Delay(200, ct).ConfigureAwait(false);
                    continue;
                }

                var captureRegions = CaptureRegionsStore.Load(profile);
                var pauseMs = Math.Clamp(
                    profile.ScreenTranslatorSettings.GetValueAsInt(ScreenTranslatorSettingDescriptors.FramePauseMs),
                    0,
                    10000);
                var frameDelay = TimeSpan.FromMilliseconds(pauseMs);

                var capSw = Stopwatch.StartNew();
                var frame = await runtime.CaptureSession.CaptureAsync(ct);
                capSw.Stop();

                if (frame is null)
                {
                    await Task.Delay(frameDelay, ct);
                    continue;
                }

                using (frame)
                {
                    CaptureFrameProcessor.ProcessedFrame? clientAligned = null;
                    CaptureFrameProcessor.ProcessedFrame? processed = null;
                    try
                    {
                        var windowHandleForAlign = runtime.WindowHandle;
                        clientAligned = windowHandleForAlign != 0
                            ? CaptureFrameProcessor.TryAlignToClientArea(frame, windowHandleForAlign)
                            : null;
                        var clientFrame = (IRawImage)(clientAligned ?? frame);

                        processed = CaptureFrameProcessor.TryProcess(clientFrame, captureRegions);
                        var imageForOcr = (IRawImage)(processed ?? clientFrame);
                        var originX = processed?.OriginX ?? 0;
                        var originY = processed?.OriginY ?? 0;

                        var ocrSw = Stopwatch.StartNew();
                        var result = await runtime.OcrSession.RecognizeAsync(imageForOcr, ct);
                        ocrSw.Stop();

                        captureTimes.Enqueue(capSw.Elapsed.TotalMilliseconds);
                        ocrTimes.Enqueue(ocrSw.Elapsed.TotalMilliseconds);
                        if (captureTimes.Count > windowSize) captureTimes.Dequeue();
                        if (ocrTimes.Count > windowSize) ocrTimes.Dequeue();

                        var avgCaptureMs = captureTimes.Average();
                        var avgOcrMs = ocrTimes.Average();

                        var layoutResult = await runtime.LayoutSession.ProcessAsync(result, ct);

                        var batch = new List<(ITextParagraph Paragraph, string Text)>();
                        foreach (var paragraph in layoutResult.Paragraphs)
                        {
                            var text = JoinParagraphForTranslation(paragraph);
                            if (string.IsNullOrWhiteSpace(text))
                                continue;

                            batch.Add((paragraph, text));
                        }

                        var sourceTexts = batch.Select(b => b.Text).ToList();
                        var overlaySession = runtime.OverlaySession;
                        var useOverlayTranslate = overlaySession is not null;

                        IReadOnlyList<string> translatedTexts = Array.Empty<string>();
                        double avgTranslateMs = translatorTimes.Count > 0 ? translatorTimes.Average() : 0;

                        if (!useOverlayTranslate)
                        {
                            try
                            {
                                var trSw = Stopwatch.StartNew();
                                translatedTexts = sourceTexts.Count == 0
                                    ? Array.Empty<string>()
                                    : await runtime.TranslatorSession.TranslateAsync(sourceTexts, ct);
                                trSw.Stop();

                                translatorTimes.Enqueue(trSw.Elapsed.TotalMilliseconds);
                                if (translatorTimes.Count > windowSize) translatorTimes.Dequeue();
                                avgTranslateMs = translatorTimes.Average();
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                // Transient translator failures: keep capturing/OCR and retry.
                                onStatus((FormatStatusError(ex), true));
                                await Task.Delay(1000, ct);
                                continue;
                            }
                        }

                        if (overlaySession is not null)
                        {
                            // Source paragraphs; layout engine translates via host callback (always / on-demand).
                            var overlayItems = BuildOverlaySourceParagraphItems(batch, originX, originY);
                            var debugWords = BuildOverlayDebugWords(result.Words, originX, originY);
                            var debugMatchedLines = BuildOverlayDebugMatchedLines(layoutResult.Lines, originX, originY);
                            await overlaySession.PresentAsync(overlayItems, debugWords, debugMatchedLines, ct);
                        }

                        var pairs = new List<(string Source, string Translation)>(sourceTexts.Count);
                        if (!useOverlayTranslate)
                        {
                            for (var i = 0; i < sourceTexts.Count; i++)
                            {
                                var translated = i < translatedTexts.Count ? translatedTexts[i] : sourceTexts[i];
                                pairs.Add((sourceTexts[i], translated));
                            }
                        }

                        var confLine = string.Format(
                            LocalizationService.Instance.CurrentCulture,
                            LocalizationService.Instance[LocalizationConstants.Text.AvgConfidence],
                            result.Confidence);
                        var textOut = useOverlayTranslate
                            ? confLine
                            : string.Join("\n\n", translatedTexts) + "\n" + confLine;

                        Dispatcher.UIThread.Post(() =>
                        {
                            if (pairs.Count > 0)
                                onTranslatedPairs?.Invoke(pairs);
                            onTextUpdated(textOut);
                            onTimings?.Invoke(avgCaptureMs, avgOcrMs, avgTranslateMs);
                        });
                    }
                    finally
                    {
                        processed?.Dispose();
                        clientAligned?.Dispose();
                    }
                }

                await Task.Delay(frameDelay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Capture / OCR / layout / overlay: stop the session.
                fatalError = LocalizationService.Instance.FormatExceptionMessage(ex);
                break;
            }
        }

        if (fatalError is not null)
        {
            onStatus((LocalizationService.Instance.FormatStoppedWithError(fatalError), true));
        }
        else
        {
            onStatus((LocalizationService.Instance[LocalizationConstants.Status.Stopped], false));
        }
    }

    private static string FormatStatusError(Exception ex)
        => string.Format(
            LocalizationService.Instance.CurrentCulture,
            LocalizationService.Instance[LocalizationConstants.Status.Error],
            LocalizationService.Instance.FormatExceptionMessage(ex));

    /// <summary>
    /// One <see cref="OverlayItem"/> per OCR line; shared <see cref="OverlayItem.Id"/> =
    /// paragraph id so Layout can translate once and wrap across line boxes.
    /// </summary>
    private static List<OverlayItem> BuildOverlaySourceParagraphItems(
        IReadOnlyList<(ITextParagraph Paragraph, string Text)> batch,
        int originX = 0,
        int originY = 0)
    {
        var items = new List<OverlayItem>();
        foreach (var (paragraph, _) in batch)
        {
            var lines = paragraph.Lines;
            if (lines.Count == 0)
                continue;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line.Text))
                    continue;

                items.Add(new OverlayItem
                {
                    Id = paragraph.Id,
                    Text = line.Text,
                    Bounds = OffsetBounds(line.Bounds, originX, originY),
                });
            }
        }

        return items;
    }

    private static BoundingBox OffsetBounds(BoundingBox bounds, int originX, int originY)
    {
        if (originX == 0 && originY == 0)
            return bounds;

        var delta = new System.Numerics.Vector2(originX, originY);
        return new BoundingBox(
            bounds.P1 + delta,
            bounds.P2 + delta,
            bounds.P3 + delta,
            bounds.P4 + delta);
    }

    private static IReadOnlyList<OverlayDebugWord> BuildOverlayDebugWords(
        IReadOnlyList<IOCRWord> words,
        int originX,
        int originY)
    {
        if (words.Count == 0)
            return [];

        var list = new List<OverlayDebugWord>(words.Count);
        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word.Text) && word.Bounds.IsEmpty)
                continue;
            list.Add(new OverlayDebugWord
            {
                Text = word.Text,
                Bounds = OffsetBounds(word.Bounds, originX, originY),
            });
        }

        return list;
    }

    private static IReadOnlyList<OverlayDebugLine> BuildOverlayDebugMatchedLines(
        IReadOnlyList<ITextLine> lines,
        int originX,
        int originY)
    {
        if (lines.Count == 0)
            return [];

        var list = new List<OverlayDebugLine>();
        foreach (var line in lines)
        {
            if (!line.HasPreviousFrameMatch || line.Bounds.IsEmpty)
                continue;
            list.Add(new OverlayDebugLine
            {
                Text = line.Text,
                Bounds = OffsetBounds(line.Bounds, originX, originY),
            });
        }

        return list;
    }

    /// <summary>
    /// Joins OCR paragraph lines for the translator. Consecutive lines are joined with
    /// a space, except after a line that ends with sentence punctuation (<c>.</c>,
    /// <c>!</c>, <c>?</c>, <c>...</c>, <c>…</c>) — then <c>\n</c> is kept so the translator
    /// sees a sentence break. Paragraph/line layout is unchanged.
    /// </summary>
    private static string JoinParagraphForTranslation(ITextParagraph paragraph)
    {
        var lines = paragraph.Lines;
        if (lines.Count == 0)
            return CollapseWhitespace(paragraph.Text.Replace('\r', ' ').Replace('\n', ' '));

        var sb = new System.Text.StringBuilder();
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
        or ')' or ']' or '}' or '\u00BB'; // »

    private static string CollapseWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var chars = text.Trim().ToCharArray();
        var sb = new System.Text.StringBuilder(chars.Length);
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

    /// <summary>
    /// Like <see cref="CollapseWhitespace"/>, but keeps <c>\n</c> as hard breaks
    /// (only collapses spaces/tabs within each line).
    /// </summary>
    private static string CollapseWhitespacePreservingNewlines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = text.Split('\n');
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

    /// <summary>
    /// Packs translated text into the original line boxes top-to-bottom.
    /// When the translation keeps the same number of <c>\n</c> breaks as the join
    /// rule (period-ending OCR lines), each segment is wrapped only into that
    /// line group; otherwise falls back to flat wrap across all lines.
    /// </summary>
    private static string[] WrapTranslatedToLines(string text, IReadOnlyList<ITextLine> lines)
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
                var groupLines = new ITextLine[groupSize];
                for (var i = 0; i < groupSize; i++)
                    groupLines[i] = lines[offset + i];

                var wrapped = WrapFlatTextToLines(
                    CollapseWhitespace(segments[g]),
                    groupLines);
                for (var i = 0; i < wrapped.Length; i++)
                    result[offset + i] = wrapped[i];
                offset += groupSize;
            }

            return result;
        }

        return WrapFlatTextToLines(CollapseWhitespace(text.Replace('\n', ' ')), lines);
    }

    /// <summary>
    /// Line-count groups matching <see cref="JoinParagraphForTranslation"/>:
    /// consecutive lines until one ends with sentence punctuation.
    /// </summary>
    private static List<int> BuildJoinLineGroups(IReadOnlyList<ITextLine> lines)
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

    private static string[] WrapFlatTextToLines(string text, IReadOnlyList<ITextLine> lines)
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
            widths[i] = Math.Max(1.0, System.Numerics.Vector2.Distance(lines[i].Bounds.P5, lines[i].Bounds.P6));
            totalWidth += widths[i];
        }

        // Prefer word wrap; fall back to character wrap for scripts without spaces.
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

            // Soft cut: allow earlier lines a bit past their share so leftovers
            // do not pile onto a short final line.
            var budget = SoftEarlierBudget(budgets[li]);
            // On the line before last, keep taking words until the remainder
            // fits the last line's width share.
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

            // Always consume at least one word so we make progress.
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

    /// <summary>
    /// Caps the last line by its width share; distributes the rest across earlier
    /// lines (they absorb more so a short final box does not overflow).
    /// </summary>
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
        // Strict-ish cap for the last line (only +10% slack). Remainder goes earlier.
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
}
