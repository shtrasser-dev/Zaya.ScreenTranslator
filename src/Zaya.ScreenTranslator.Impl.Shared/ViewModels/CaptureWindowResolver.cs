using System.Diagnostics;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

internal sealed class CaptureWindowResolver
{
    public sealed record CaptureTarget(nint Handle, string Title, string ProcessName);

    private readonly ICaptureHostState _host;
    private int _windowsLoadVersion;

    public CaptureWindowResolver(ICaptureHostState host) => _host = host;

    public async Task<CaptureTarget?> ResolveCaptureTargetAsync(IApplicationProfile profile)
    {
        if (_host.SelectedWindow is { IsLoadingPlaceholder: false, Handle: not 0 } window)
        {
            _host.SetWindowError(null);
            _host.Context.ActiveWindowHandle = window.Handle;
            _host.Context.ActiveWindowTitle = window.Title;
            return new CaptureTarget(window.Handle, window.Title, window.ProcessName);
        }

        var targetProcess = CaptureProcessWindowHelpers.NormalizeProcessName(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TargetProcess));

        if (string.IsNullOrEmpty(targetProcess))
        {
            ReportSelectTargetWindowError();
            return null;
        }

        _host.SetWindowError(null);
        _host.LoopCts = new CancellationTokenSource();
        var ct = _host.LoopCts.Token;
        _host.IsRunning = true;
        _host.SetStatus(string.Format(_host.Loc[LocalizationConstants.Status.WaitingForProcess], targetProcess));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (CaptureProcessWindowHelpers.TryFindProcessMainWindow(targetProcess, out var handle, out var title))
                {
                    _host.SetWindowError(null);
                    _host.Context.ActiveWindowHandle = handle;
                    _host.Context.ActiveWindowTitle = title;
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
                _host.IsRunning = false;
                _host.LoopCts?.Dispose();
                _host.LoopCts = null;
            }
        }

        if (ct.IsCancellationRequested)
            return null;

        _host.IsRunning = false;
        _host.LoopCts?.Dispose();
        _host.LoopCts = null;
        ReportSelectTargetWindowError();
        return null;
    }

    private void ReportSelectTargetWindowError()
    {
        _host.SetWindowError(_host.Loc[LocalizationConstants.Status.SelectTargetWindow]);
    }

    public async Task SyncWindowPickerAsync(CaptureTarget target)
    {
        IReadOnlyList<WindowInfo> list;
        try
        {
            list = await Task.Run(BuildWindowList).ConfigureAwait(true);
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
                Icon = CaptureProcessWindowHelpers.TryLoadIconForProcessName(target.ProcessName),
            };
            list = list
                .Append(match)
                .OrderBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        _host.Windows = list;
        _host.SetSelectedWindow(match);
        _host.LastSelectedWindow = match;
        _host.NotifySetCurrentProcessCanExecuteChanged();
    }

    public async Task LoadWindowsAsync()
    {
        var version = Interlocked.Increment(ref _windowsLoadVersion);
        var previous = _host.LastSelectedWindow;

        _host.Windows = [WindowInfo.Loading];

        IReadOnlyList<WindowInfo> list;
        try
        {
            list = await Task.Run(BuildWindowList).ConfigureAwait(true);
        }
        catch
        {
            list = [];
        }

        if (version != _windowsLoadVersion)
            return;

        _host.Windows = list;
        _host.SetSelectedWindow(previous is null
            ? null
            : list.FirstOrDefault(w => w.Handle == previous.Handle));
    }

    public static List<WindowInfo> BuildWindowList()
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
                    Icon = ProcessIconLoader.GetIcon(process),
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
