using Avalonia.Threading;
using System.Diagnostics;
using Zaya.OCR.Models;
using Zaya.OCR.Services;
using Zaya.Screenshot.Models;
using Zaya.Screenshot.Services;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Layout.Models;
using Zaya.ScreenTranslator.Layout.Services;
using Zaya.Translator.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class TranslationLoopService
{
    public async Task RunAsync(
        IOCRService ocr,
        ICaptureService capture,
        ITextLayoutService textLayout,
        ITranslatorService translator,
        ICaptureRegion region,
        IApplicationProfile profile,
        CancellationToken ct,
        Action<string> onTextUpdated,
        Action<(string Text, bool IsError)> onStatus,
        Action<double, double, double>? onTimings = null,
        string? targetLanguage = null,
        IOverlayLayoutSession? overlaySession = null,
        Action<IReadOnlyList<(string Source, string Translation)>>? onTranslatedPairs = null)
    {
        onStatus((LocalizationService.Instance[LocalizationConstants.Status.CreatingSessions], false));

        var ocrSettings = GetOrCreatePluginSettings(profile,
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
        var captureSettings = GetOrCreatePluginSettings(profile,
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));
        var tlSettings = GetOrCreatePluginSettings(profile,
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
        var trSettings = GetOrCreatePluginSettings(profile,
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));

        using var captureSession = await capture.CreateSessionAsync(region,
            ManagedSettingKeys.PrepareForEngine(capture.EngineId, capture.Settings, captureSettings), ct);
        using var ocrSession = await ocr.CreateSessionAsync(
            ManagedSettingKeys.PrepareForEngine(ocr.EngineId, ocr.Settings, ocrSettings), ct);
        using var layoutSession = await textLayout.CreateSessionAsync(
            ManagedSettingKeys.PrepareForEngine(textLayout.EngineId, textLayout.Settings, tlSettings), ct);
        using var translatorSession = await translator.CreateSessionAsync(
            ManagedSettingKeys.PrepareForEngine(
                translator.EngineId, translator.Settings, trSettings, targetLanguage), ct);

        var filter = TextFilterSession.Create(profile.ScreenTranslatorSettings);
        var passThrough = translator.EngineId == NoTranslationTranslatorService.EngineIdValue;

        var pauseMs = Math.Clamp(
            profile.ScreenTranslatorSettings.GetValueAsInt(ScreenTranslatorSettingDescriptors.FramePauseMs),
            0,
            10000);
        var frameDelay = TimeSpan.FromMilliseconds(pauseMs);

        var captureTimes = new Queue<double>();
        var ocrTimes = new Queue<double>();
        var translatorTimes = new Queue<double>();
        const int windowSize = 10;

        onStatus((AppConstants.LoopStatus.Running, false));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var capSw = Stopwatch.StartNew();
                var frame = await captureSession.CaptureAsync(ct);
                capSw.Stop();

                if (frame is null)
                {
                    await Task.Delay(frameDelay, ct);
                    continue;
                }

                using (frame)
                {
                    var ocrSw = Stopwatch.StartNew();
                    var result = await ocrSession.RecognizeAsync(frame, ct);
                    ocrSw.Stop();

                    captureTimes.Enqueue(capSw.Elapsed.TotalMilliseconds);
                    ocrTimes.Enqueue(ocrSw.Elapsed.TotalMilliseconds);
                    if (captureTimes.Count > windowSize) captureTimes.Dequeue();
                    if (ocrTimes.Count > windowSize) ocrTimes.Dequeue();

                    var avgCaptureMs = captureTimes.Average();
                    var avgOcrMs = ocrTimes.Average();

                    var layoutResult = await layoutSession.ProcessAsync(result, ct);

                    var batch = new List<(ITextParagraph Paragraph, string Text)>();
                    foreach (var paragraph in layoutResult.Paragraphs)
                    {
                        var text = JoinParagraphForTranslation(paragraph);
                        if (string.IsNullOrWhiteSpace(text))
                            continue;

                        if (!passThrough)
                        {
                            var filtered = filter.Apply([text]);
                            if (filtered.Count == 0)
                                continue;
                            text = filtered[0];
                        }

                        batch.Add((paragraph, text));
                    }

                    var sourceTexts = batch.Select(b => b.Text).ToList();

                    var trSw = Stopwatch.StartNew();
                    var translatedTexts = sourceTexts.Count == 0
                        ? Array.Empty<string>()
                        : await translatorSession.TranslateAsync(sourceTexts, ct);
                    trSw.Stop();

                    translatorTimes.Enqueue(trSw.Elapsed.TotalMilliseconds);
                    if (translatorTimes.Count > windowSize) translatorTimes.Dequeue();
                    var avgTranslateMs = translatorTimes.Average();

                    var overlayItems = BuildOverlayItems(batch, translatedTexts);
                    if (overlaySession is not null)
                        await overlaySession.PresentAsync(overlayItems, ct);

                    var pairs = new List<(string Source, string Translation)>(sourceTexts.Count);
                    for (var i = 0; i < sourceTexts.Count; i++)
                    {
                        var translated = i < translatedTexts.Count ? translatedTexts[i] : sourceTexts[i];
                        pairs.Add((sourceTexts[i], translated));
                    }

                    var confLine = string.Format(
                        LocalizationService.Instance.CurrentCulture,
                        LocalizationService.Instance[LocalizationConstants.Text.AvgConfidence],
                        result.Confidence);
                    var textOut = string.Join("\n\n", translatedTexts) + "\n" + confLine;

                    Dispatcher.UIThread.Post(() =>
                    {
                        onTranslatedPairs?.Invoke(pairs);
                        onTextUpdated(textOut);
                        onTimings?.Invoke(avgCaptureMs, avgOcrMs, avgTranslateMs);
                    });
                }

                await Task.Delay(frameDelay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                onStatus((string.Format(
                    LocalizationService.Instance.CurrentCulture,
                    LocalizationService.Instance[LocalizationConstants.Status.Error],
                    ex.Message), true));
                await Task.Delay(1000, ct);
            }
        }

        onStatus((LocalizationService.Instance[LocalizationConstants.Status.Stopped], false));
    }

    private static List<OverlayItem> BuildOverlayItems(
        IReadOnlyList<(ITextParagraph Paragraph, string Text)> batch,
        IReadOnlyList<string> translatedTexts)
    {
        var items = new List<OverlayItem>();
        for (var i = 0; i < batch.Count; i++)
        {
            var paragraph = batch[i].Paragraph;
            var translated = i < translatedTexts.Count ? translatedTexts[i] : batch[i].Text;
            var lines = paragraph.Lines;
            if (lines.Count == 0)
                continue;

            var parts = WrapTranslatedToLines(translated, lines);
            for (var li = 0; li < lines.Count; li++)
            {
                var lineText = parts[li];
                if (string.IsNullOrWhiteSpace(lineText))
                    continue;
                items.Add(new OverlayItem
                {
                    Text = lineText,
                    Bounds = lines[li].Bounds,
                });
            }
        }

        return items;
    }

    /// <summary>
    /// Joins OCR paragraph lines into one sentence for the translator
    /// (newlines would otherwise be treated as separate sentences).
    /// </summary>
    private static string JoinParagraphForTranslation(ITextParagraph paragraph)
    {
        if (paragraph.Lines.Count > 0)
            return CollapseWhitespace(string.Join(" ", paragraph.Lines.Select(l => l.Text)));

        return CollapseWhitespace(paragraph.Text.Replace('\r', ' ').Replace('\n', ' '));
    }

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
    /// Packs translated text into the original line boxes top-to-bottom.
    /// Character budgets follow line widths, but earlier lines get priority so a
    /// short final OCR line is not forced to absorb leftover from rigid cuts.
    /// </summary>
    private static string[] WrapTranslatedToLines(string text, IReadOnlyList<ITextLine> lines)
    {
        var lineCount = lines.Count;
        if (lineCount <= 0)
            return [];

        text = CollapseWhitespace(text.Replace('\r', ' ').Replace('\n', ' '));
        if (lineCount == 1)
            return [text];

        if (string.IsNullOrEmpty(text))
            return Enumerable.Repeat(string.Empty, lineCount).ToArray();

        var widths = new double[lineCount];
        var totalWidth = 0.0;
        for (var i = 0; i < lineCount; i++)
        {
            widths[i] = Math.Max(1.0, lines[i].Bounds.Width);
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

    private static Dictionary<string, object> GetOrCreatePluginSettings(
        IApplicationProfile profile, string pluginId)
    {
        if (!profile.Settings.TryGetValue(pluginId, out var settings))
            profile.Settings[pluginId] = settings = new();
        return settings;
    }
}
