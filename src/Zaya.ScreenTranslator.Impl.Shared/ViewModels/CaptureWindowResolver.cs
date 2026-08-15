using System.Diagnostics;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Native;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

internal sealed class CaptureWindowResolver
{
    public sealed record CaptureTarget(nint Handle, string Title, string ProcessName);

    private readonly ICaptureHostState _captureHostState;
    private readonly IProcessIconLoader _processIconLoader;
    private int _windowsLoadVersion;

    public CaptureWindowResolver(ICaptureHostState captureHostState, IProcessIconLoader processIconLoader)
    {
        _captureHostState = captureHostState;
        _processIconLoader = processIconLoader;
    }

    public async Task<CaptureTarget?> ResolveCaptureTargetAsync(IApplicationProfile profile)
    {
        if (_captureHostState.SelectedWindow is { IsLoadingPlaceholder: false, Handle: not 0 } window)
        {
            _captureHostState.SetWindowError(null);
            _captureHostState.Context.ActiveWindowHandle = window.Handle;
            _captureHostState.Context.ActiveWindowTitle = window.Title;
            return new CaptureTarget(window.Handle, window.Title, window.ProcessName);
        }

        var targetProcess = CaptureProcessWindowHelpers.NormalizeProcessName(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TargetProcess));

        if (string.IsNullOrEmpty(targetProcess))
        {
            ReportSelectTargetWindowError();
            return null;
        }

        _captureHostState.SetWindowError(null);
        _captureHostState.LoopCts = new CancellationTokenSource();
        var ct = _captureHostState.LoopCts.Token;
        _captureHostState.IsRunning = true;
        _captureHostState.SetStatus(string.Format(_captureHostState.Loc[LocalizationConstants.Status.WaitingForProcess], targetProcess));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (CaptureProcessWindowHelpers.TryFindProcessMainWindow(targetProcess, out var handle, out var title))
                {
                    _captureHostState.SetWindowError(null);
                    _captureHostState.Context.ActiveWindowHandle = handle;
                    _captureHostState.Context.ActiveWindowTitle = title;
                    return new CaptureTarget(handle, title, targetProcess);
                }

                try
                {
                    await Task.Delay(1000, ct).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            if (ct.IsCancellationRequested)
            {
                _captureHostState.IsRunning = false;
                _captureHostState.LoopCts?.Dispose();
                _captureHostState.LoopCts = null;
            }
        }

        if (ct.IsCancellationRequested)
            return null;

        _captureHostState.IsRunning = false;
        _captureHostState.LoopCts?.Dispose();
        _captureHostState.LoopCts = null;
        ReportSelectTargetWindowError();
        return null;
    }

    private void ReportSelectTargetWindowError()
    {
        _captureHostState.SetWindowError(_captureHostState.Loc[LocalizationConstants.Status.SelectTargetWindow]);
    }

    public async Task SyncWindowPickerAsync(CaptureTarget target)
    {
        IReadOnlyList<WindowInfo> list;
        try
        {
            list = await Task.Run(() => BuildWindowList()).ConfigureAwait(true);
        }
        catch
        {
            list = [];
        }

        var match = list.FirstOrDefault(w => w.Handle == target.Handle);
        if (match is null)
        {
            match = new WindowInfo
            {
                Handle = target.Handle,
                Title = target.Title,
                ProcessName = target.ProcessName,
                Icon = CaptureProcessWindowHelpers.TryLoadIconForProcessName(target.ProcessName, _processIconLoader),
            };
            list = list
                .Append(match)
                .OrderBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        _captureHostState.Windows = list;
        _captureHostState.SetSelectedWindow(match);
        _captureHostState.LastSelectedWindow = match;
        _captureHostState.NotifySetCurrentProcessCanExecuteChanged();
    }

    public async Task LoadWindowsAsync()
    {
        var version = Interlocked.Increment(ref _windowsLoadVersion);
        var previous = _captureHostState.LastSelectedWindow;

        _captureHostState.Windows = [WindowInfo.Loading];

        IReadOnlyList<WindowInfo> list;
        try
        {
            list = await Task.Run(() => BuildWindowList()).ConfigureAwait(true);
        }
        catch
        {
            list = [];
        }

        if (version != _windowsLoadVersion)
            return;

        _captureHostState.Windows = list;
        _captureHostState.SetSelectedWindow(previous is null
            ? null
            : list.FirstOrDefault(w => w.Handle == previous.Handle));
    }

    /// <summary>
    /// Clears the Window picker when the selected process is gone or its window HWND is no longer valid.
    /// </summary>
    public Task ClearSelectedWindowIfProcessGoneAsync()
    {
        var selected = _captureHostState.SelectedWindow;
        if (selected is null || selected.IsLoadingPlaceholder)
            return Task.CompletedTask;

        var processAlive = CaptureProcessWindowHelpers.IsProcessRunning(selected.ProcessName);
        var windowAlive = Win32WindowBounds.IsValidWindow(selected.Handle);
        if (processAlive && windowAlive)
            return Task.CompletedTask;

        _captureHostState.LastSelectedWindow = null;
        _captureHostState.SetSelectedWindow(null);
        _captureHostState.Context.ActiveWindowHandle = 0;
        _captureHostState.Context.ActiveWindowTitle = null;
        _captureHostState.NotifySetCurrentProcessCanExecuteChanged();
        return LoadWindowsAsync();
    }

    private List<WindowInfo> BuildWindowList()
    {
        var result = new List<WindowInfo>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.MainWindowHandle == IntPtr.Zero)
                    continue;

                var title = process.MainWindowTitle;
                if (string.IsNullOrEmpty(title))
                    continue;

                result.Add(new WindowInfo
                {
                    Handle = process.MainWindowHandle,
                    Title = title,
                    ProcessName = process.ProcessName,
                    Icon = _processIconLoader.GetIcon(process),
                });
            }
            catch
            {
                // Some processes deny query access — skip them.
            }
            finally
            {
                process.Dispose();
            }
        }

        result.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));
        return result;
    }
}
