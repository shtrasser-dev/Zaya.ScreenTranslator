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
    private readonly OverlayWindow _window;
    private bool _disposed;
    private bool _visible;

    public ScreenOverlayLayoutSession(SettingDescriptorList settings, IntPtr targetHwnd)
    {
        _settings = settings;
        _targetHwnd = targetHwnd;
        _window = new OverlayWindow();
        _window.SyncToTarget(_targetHwnd);
    }

    public Task PresentAsync(IReadOnlyList<OverlayItem> items, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var placement = _settings.GetValueAsString(OverlayLayoutSettingKeys.Placement);
        var fontScale = _settings.GetValueAsInt(OverlayLayoutSettingKeys.FontScale);
        var offsetY = _settings.GetValueAsInt(OverlayLayoutSettingKeys.OffsetY);
        var padding = _settings.GetValueAsInt(OverlayLayoutSettingKeys.Padding);
        var background = _settings.GetValueAsString(OverlayLayoutSettingKeys.Background);
        var bgOpacity = _settings.GetValueAsInt(OverlayLayoutSettingKeys.BackgroundOpacity);
        var textColor = _settings.GetValueAsString(OverlayLayoutSettingKeys.TextColor);
        var outline = _settings.GetValueAsBool(OverlayLayoutSettingKeys.Outline);
        var fitMode = _settings.GetValueAsString(OverlayLayoutSettingKeys.FitMode);

        var specs = new List<OverlayDrawSpec>(items.Count);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Text))
                continue;
            specs.Add(OverlayLayoutMath.Compute(
                item, placement, fontScale, offsetY, padding,
                background, bgOpacity, textColor, outline, fitMode));
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OverlayWindow.RunOnUi(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _window.SyncToTarget(_targetHwnd);
                _window.RenderItems(specs);
                if (_visible && !_window.IsVisible)
                    _window.Show();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
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
        OverlayWindow.RunOnUi(() => _window.ClearItems());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        OverlayWindow.RunOnUi(() =>
        {
            _window.ClearItems();
            _window.Close();
        });
    }
}
