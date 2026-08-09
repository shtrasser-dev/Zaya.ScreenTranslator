using Avalonia.Threading;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Layout.Impl.Constants;
using Zaya.ScreenTranslator.Layout.Impl.Models;
using Zaya.ScreenTranslator.Layout.Impl.Views;
using Zaya.ScreenTranslator.Layout.Models;
using Zaya.ScreenTranslator.Layout.Services;

namespace Zaya.ScreenTranslator.Layout.Impl.Services;

internal sealed class ScreenOverlayLayoutSession : IOverlayLayoutSession
{
    private readonly SettingDescriptorList _settings;
    private readonly IntPtr _targetHwnd;
    private readonly OverlayTranslateCallback? _translate;
    private readonly OverlayWindow _window;
    private readonly object _gate = new();
    /// <summary><see cref="OverlayItem.Id"/> → (source fingerprint, translation) for on-demand bubbles.</summary>
    private readonly Dictionary<Guid, (string Source, string Translation)> _expanded = new();
    private CancellationTokenSource? _hoverTranslateCts;
    private string? _hoverKey;
    private bool _disposed;
    private bool _visible;
    private int _presentGeneration;
    private IReadOnlyList<OverlayItem> _lastItems = Array.Empty<OverlayItem>();
    private IReadOnlyList<OverlayDebugWord>? _lastDebugWords;
    private IReadOnlyList<OverlayDebugLine>? _lastDebugMatchedLines;

    public ScreenOverlayLayoutSession(
        SettingDescriptorList settings,
        IntPtr targetHwnd,
        OverlayTranslateCallback? translate)
    {
        _settings = settings;
        _targetHwnd = targetHwnd;
        _translate = translate;
        _window = new OverlayWindow();
        _window.HoverTargetChanged += OnHoverTargetChanged;
        _window.SyncToTarget(_targetHwnd);
    }

    public Task PresentAsync(IReadOnlyList<OverlayItem> items, CancellationToken cancellationToken = default)
        => PresentAsync(items, debugWords: null, debugMatchedLines: null, cancellationToken);

    public Task PresentAsync(
        IReadOnlyList<OverlayItem> items,
        IReadOnlyList<OverlayDebugWord>? debugWords,
        CancellationToken cancellationToken = default)
        => PresentAsync(items, debugWords, debugMatchedLines: null, cancellationToken);

    public async Task PresentAsync(
        IReadOnlyList<OverlayItem> items,
        IReadOnlyList<OverlayDebugWord>? debugWords,
        IReadOnlyList<OverlayDebugLine>? debugMatchedLines,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var onDemand = IsOnDemand();
        var prunedHovered = PruneExpanded(items);

        // One OverlayItem per OCR line; same Id = same paragraph. Translate once per Id.
        Dictionary<Guid, string>? translationsById = null;
        if (!onDemand && _translate is not null && items.Count > 0)
        {
            var groups = GroupLinesByParagraph(items);
            if (groups.Count > 0)
            {
                var sources = groups.Select(g => OverlayParagraphWrap.JoinForTranslation(g.Lines)).ToList();
                try
                {
                    var translated = await _translate(sources, cancellationToken).ConfigureAwait(false);
                    translationsById = new Dictionary<Guid, string>(groups.Count);
                    for (var i = 0; i < groups.Count; i++)
                    {
                        var text = i < translated.Count && !string.IsNullOrWhiteSpace(translated[i])
                            ? translated[i]
                            : OverlayParagraphWrap.JoinForTranslation(groups[i].Lines);
                        translationsById[groups[i].Id] = text;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Host callback normally absorbs transient errors; keep presenting source text.
                    translationsById = null;
                }
            }
        }

        var specs = BuildSpecs(items, translationsById, onDemand);
        var debug = _settings.GetValueAsBool(OverlayLayoutSettingKeys.DebugMode) ? debugWords : null;
        var matchedLines = _settings.GetValueAsBool(OverlayLayoutSettingKeys.DebugMode) ? debugMatchedLines : null;

        lock (_gate)
        {
            _lastItems = items;
            _lastDebugWords = debugWords;
            _lastDebugMatchedLines = debugMatchedLines;
        }

        await RenderOnUiAsync(specs, debug, matchedLines, interactive: onDemand, cancellationToken).ConfigureAwait(false);

        if (onDemand && prunedHovered)
            RetranslateHoveredIfNeeded();
    }

    public void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _visible = visible;
        OverlayWindow.RunOnUi(() =>
        {
            if (visible)
            {
                _window.SyncToTarget(_targetHwnd);
                _window.Show();
            }
            else
            {
                _window.Hide();
            }
        });
    }

    public void Clear()
    {
        if (_disposed) return;
        CancelHoverTranslate();
        lock (_gate)
        {
            _expanded.Clear();
            _hoverKey = null;
        }
        OverlayWindow.RunOnUi(() => _window.ClearItems());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelHoverTranslate();
        _window.HoverTargetChanged -= OnHoverTargetChanged;
        OverlayWindow.RunOnUi(() =>
        {
            _window.ClearItems();
            _window.Close();
        });
    }

    private bool IsOnDemand()
        => string.Equals(
            _settings.GetValueAsString(OverlayLayoutSettingKeys.TranslateMode),
            OverlayLayoutSettingKeys.TranslateModeOnDemand,
            StringComparison.Ordinal);

    /// <returns>True when the currently hovered paragraph lost its cached translation.</returns>
    private bool PruneExpanded(IReadOnlyList<OverlayItem> items)
    {
        var sourcesById = new Dictionary<Guid, string>();
        foreach (var (id, lines) in GroupLinesByParagraph(items))
            sourcesById[id] = OverlayParagraphWrap.JoinForTranslation(lines);

        lock (_gate)
        {
            var stale = _expanded
                .Where(kv =>
                    !sourcesById.TryGetValue(kv.Key, out var src)
                    || !string.Equals(kv.Value.Source, src, StringComparison.Ordinal))
                .Select(kv => kv.Key)
                .ToList();

            var prunedHovered = false;
            foreach (var id in stale)
            {
                if (_hoverKey is not null && string.Equals(_hoverKey, IdKey(id), StringComparison.Ordinal))
                    prunedHovered = true;
                _expanded.Remove(id);
            }

            return prunedHovered;
        }
    }

    private static string IdKey(Guid id) => id.ToString("N");

    private static List<(Guid Id, List<OverlayItem> Lines)> GroupLinesByParagraph(IReadOnlyList<OverlayItem> items)
    {
        var order = new List<Guid>();
        var map = new Dictionary<Guid, List<OverlayItem>>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Text))
                continue;
            if (!map.TryGetValue(item.Id, out var list))
            {
                list = [];
                map[item.Id] = list;
                order.Add(item.Id);
            }

            list.Add(item);
        }

        return order.Select(id => (id, map[id])).ToList();
    }

    private List<OverlayDrawSpec> BuildSpecs(
        IReadOnlyList<OverlayItem> items,
        IReadOnlyDictionary<Guid, string>? translationsById,
        bool onDemand)
    {
        var placement = _settings.GetValueAsString(OverlayLayoutSettingKeys.Placement);
        var fixedFontSize = _settings.GetValueAsBool(OverlayLayoutSettingKeys.FixedFontSize);
        var fontScale = _settings.GetValueAsInt(OverlayLayoutSettingKeys.FontScale);
        var fontSize = _settings.GetValueAsInt(OverlayLayoutSettingKeys.FontSize);
        var offsetY = _settings.GetValueAsInt(OverlayLayoutSettingKeys.OffsetY);
        var offsetYPercent = _settings.GetValueAsInt(OverlayLayoutSettingKeys.OffsetYPercent);
        var padding = _settings.GetValueAsInt(OverlayLayoutSettingKeys.Padding);
        var background = _settings.GetValueAsString(OverlayLayoutSettingKeys.Background);
        var bgOpacity = _settings.GetValueAsInt(OverlayLayoutSettingKeys.BackgroundOpacity);
        var backgroundColor = _settings.GetValueAsString(OverlayLayoutSettingKeys.BackgroundColor);
        var textColor = _settings.GetValueAsString(OverlayLayoutSettingKeys.TextColor);
        var outline = _settings.GetValueAsBool(OverlayLayoutSettingKeys.Outline);

        Dictionary<Guid, string>? expanded;
        lock (_gate)
        {
            expanded = onDemand
                ? _expanded.ToDictionary(kv => kv.Key, kv => kv.Value.Translation)
                : null;
        }

        var specs = new List<OverlayDrawSpec>(items.Count);
        foreach (var (id, lines) in GroupLinesByParagraph(items))
        {
            var idKey = IdKey(id);
            if (onDemand)
            {
                // Fill first, outline on top so the 1px rect stays visible after expand.
                if (expanded is not null && expanded.TryGetValue(id, out var translated)
                    && !string.IsNullOrWhiteSpace(translated))
                {
                    specs.Add(OverlayLayoutMath.ComputeParagraphFill(
                        lines, translated, fixedFontSize, fontScale, fontSize,
                        background, bgOpacity, backgroundColor, textColor, outline, idKey));
                }

                specs.Add(OverlayLayoutMath.ComputeParagraphHitArea(lines, textColor, idKey));
                continue;
            }

            string displayParagraph;
            if (translationsById is not null && translationsById.TryGetValue(id, out var t) && !string.IsNullOrWhiteSpace(t))
                displayParagraph = t;
            else
                displayParagraph = OverlayParagraphWrap.JoinForTranslation(lines);

            var lineParts = OverlayParagraphWrap.WrapTranslatedToLines(displayParagraph, lines);
            for (var i = 0; i < lines.Count; i++)
            {
                var part = i < lineParts.Length ? lineParts[i] : string.Empty;
                if (string.IsNullOrWhiteSpace(part))
                    continue;
                specs.Add(OverlayLayoutMath.Compute(
                    lines[i], placement, fixedFontSize, fontScale, fontSize, offsetY, offsetYPercent, padding,
                    background, bgOpacity, backgroundColor, textColor, outline,
                    displayText: part, sourceKey: idKey));
            }
        }

        return specs;
    }

    private Task RenderOnUiAsync(
        IReadOnlyList<OverlayDrawSpec> specs,
        IReadOnlyList<OverlayDebugWord>? debug,
        IReadOnlyList<OverlayDebugLine>? matchedLines,
        bool interactive,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OverlayWindow.RunOnUi(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _window.SyncToTarget(_targetHwnd);
                _window.SetInteractive(interactive);
                _window.RenderItems(specs, debug, matchedLines);
                if (_visible)
                {
                    if (!_window.IsVisible)
                        _window.Show();
                }
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private void CancelHoverTranslate()
    {
        try { _hoverTranslateCts?.Cancel(); } catch { /* ignore */ }
        _hoverTranslateCts?.Dispose();
        _hoverTranslateCts = null;
    }

    private async void OnHoverTargetChanged(string? idKey)
    {
        if (_disposed)
            return;

        if (string.Equals(_hoverKey, idKey, StringComparison.Ordinal))
            return;

        CancelHoverTranslate();
        _hoverKey = idKey;

        lock (_gate)
            _expanded.Clear();

        var generation = Interlocked.Increment(ref _presentGeneration);
        // Drop any visible translation immediately (leave or switch target).
        await RerenderOnDemandAsync(generation).ConfigureAwait(false);

        if (string.IsNullOrEmpty(idKey) || !Guid.TryParseExact(idKey, "N", out var id))
            return;

        await TranslateHoveredAsync(idKey, id).ConfigureAwait(false);
    }

    /// <summary>
    /// After prune removed the hovered bubble (same Id, new source text), request a fresh translation.
    /// </summary>
    private void RetranslateHoveredIfNeeded()
    {
        string? hoverKey;
        lock (_gate)
            hoverKey = _hoverKey;

        if (string.IsNullOrEmpty(hoverKey) || !Guid.TryParseExact(hoverKey, "N", out var id))
            return;

        lock (_gate)
        {
            if (_expanded.ContainsKey(id))
                return;
        }

        CancelHoverTranslate();
        _ = TranslateHoveredAsync(hoverKey, id);
    }

    private async Task TranslateHoveredAsync(string idKey, Guid id)
    {
        if (_translate is null || _disposed)
            return;

        List<OverlayItem> lines;
        lock (_gate)
            lines = _lastItems.Where(i => i.Id == id && !string.IsNullOrWhiteSpace(i.Text)).ToList();

        if (lines.Count == 0)
            return;

        var cts = new CancellationTokenSource();
        _hoverTranslateCts = cts;
        var source = OverlayParagraphWrap.JoinForTranslation(lines);
        try
        {
            var translated = await _translate([source], cts.Token).ConfigureAwait(false);
            if (cts.IsCancellationRequested || _disposed)
                return;
            if (!string.Equals(_hoverKey, idKey, StringComparison.Ordinal))
                return;

            var text = translated.Count > 0 && !string.IsNullOrWhiteSpace(translated[0])
                ? translated[0]
                : source;
            lock (_gate)
                _expanded[id] = (source, text);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            return;
        }

        var generation = Interlocked.Increment(ref _presentGeneration);
        await RerenderOnDemandAsync(generation).ConfigureAwait(false);
    }

    private async Task RerenderOnDemandAsync(int generation)
    {
        if (_disposed || Volatile.Read(ref _presentGeneration) != generation)
            return;

        IReadOnlyList<OverlayItem> items;
        IReadOnlyList<OverlayDebugWord>? debugWords;
        IReadOnlyList<OverlayDebugLine>? debugLines;
        lock (_gate)
        {
            items = _lastItems;
            debugWords = _lastDebugWords;
            debugLines = _lastDebugMatchedLines;
        }

        var specs = BuildSpecs(items, translationsById: null, onDemand: true);
        var debug = _settings.GetValueAsBool(OverlayLayoutSettingKeys.DebugMode) ? debugWords : null;
        var matched = _settings.GetValueAsBool(OverlayLayoutSettingKeys.DebugMode) ? debugLines : null;
        await RenderOnUiAsync(specs, debug, matched, interactive: true, CancellationToken.None).ConfigureAwait(false);
    }
}
