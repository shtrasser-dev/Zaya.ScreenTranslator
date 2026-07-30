using Avalonia.Threading;
using System.Diagnostics;
using Zaya.OCR.Models;
using Zaya.OCR.Services;
using Zaya.Screenshot.Models;
using Zaya.Screenshot.Services;
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
        Action<string> onStatus,
        Action<double, double, double>? onTimings = null,
        string? targetLanguage = null,
        IOverlayLayoutSession? overlaySession = null)
    {
        onStatus("Creating sessions...");

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

        var fps = profile.ScreenTranslatorSettings.GetValueAsInt(ScreenTranslatorSettingDescriptors.TargetFps);
        var frameDelay = fps > 0
            ? TimeSpan.FromMilliseconds(1000.0 / fps)
            : TimeSpan.FromMilliseconds(66);

        var captureTimes = new Queue<double>();
        var ocrTimes = new Queue<double>();
        var translatorTimes = new Queue<double>();
        const int windowSize = 10;

        onStatus("Running");

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
                        if (string.IsNullOrWhiteSpace(paragraph.Text))
                            continue;

                        var text = paragraph.Text;
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

                    var textOut = string.Join("\n\n", translatedTexts)
                        + $"\n--- avg conf: {result.Confidence:F2} ---";

                    Dispatcher.UIThread.Post(() =>
                    {
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
                onStatus($"Error: {ex.Message}");
                await Task.Delay(1000, ct);
            }
        }

        onStatus("Stopped");
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

            var parts = SplitToLineCount(translated, lines.Count);
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

    private static string[] SplitToLineCount(string text, int lineCount)
    {
        if (lineCount <= 0)
            return [];

        var byNewline = text.Replace("\r\n", "\n").Split('\n');
        if (byNewline.Length == lineCount)
            return byNewline;

        var result = new string[lineCount];
        if (byNewline.Length > lineCount)
        {
            for (var i = 0; i < lineCount - 1; i++)
                result[i] = byNewline[i];
            result[lineCount - 1] = string.Join(" ", byNewline.Skip(lineCount - 1));
            return result;
        }

        // Fewer newline parts than lines: put all on first line, rest empty — or distribute words.
        var words = text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            result[0] = text;
            for (var i = 1; i < lineCount; i++)
                result[i] = string.Empty;
            return result;
        }

        var perLine = Math.Max(1, (int)Math.Ceiling(words.Length / (double)lineCount));
        var wi = 0;
        for (var i = 0; i < lineCount; i++)
        {
            var take = Math.Min(perLine, words.Length - wi);
            if (i == lineCount - 1)
                take = words.Length - wi;
            result[i] = take > 0 ? string.Join(" ", words.Skip(wi).Take(take)) : string.Empty;
            wi += take;
        }

        return result;
    }

    private static Dictionary<string, object> GetOrCreatePluginSettings(
        IApplicationProfile profile, string pluginId)
    {
        if (!profile.Settings.TryGetValue(pluginId, out var settings))
            profile.Settings[pluginId] = settings = new();
        return settings;
    }
}
