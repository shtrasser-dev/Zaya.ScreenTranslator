using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Views;
using Zaya.ScreenTranslator.Layout.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

internal interface ITextOutputHost : IStatusHost
{
    bool IsTextWindowVisible { get; set; }
    bool IsOverlayMode { get; }
}

internal sealed class TextOutputPresenter
{
    private readonly IApplicationProfileService _applicationProfileService;
    private readonly ITranslationHistoryService _translationHistoryService;
    private readonly ILocalizationService _localizationService;
    private readonly ITextOutputHost _textOutputHost;
    private readonly Func<IOverlayLayoutSession?> _getOverlaySession;

    private Window? _textWindow;
    private HistoryWindow? _historyWindow;
    private HistoryViewModel? _historyViewModel;

    public TextOutputPresenter(
        IApplicationProfileService applicationProfileService,
        ITranslationHistoryService translationHistoryService,
        ILocalizationService localizationService,
        ITextOutputHost textOutputHost,
        Func<IOverlayLayoutSession?> getOverlaySession)
    {
        _applicationProfileService = applicationProfileService;
        _translationHistoryService = translationHistoryService;
        _localizationService = localizationService;
        _textOutputHost = textOutputHost;
        _getOverlaySession = getOverlaySession;
    }

    public void ToggleTextOutput() => SetTextOutputVisible(!_textOutputHost.IsTextWindowVisible);

    public void SetTextOutputVisible(bool visible)
    {
        _textOutputHost.IsTextWindowVisible = visible;

        if (_textOutputHost.IsOverlayMode)
        {
            var overlaySession = _getOverlaySession();
            if (overlaySession is not null)
                overlaySession.SetVisible(_textOutputHost.IsTextWindowVisible);
            else if (_textOutputHost.IsTextWindowVisible)
                _textOutputHost.SetStatus(_textOutputHost.Loc[LocalizationConstants.Overlay.NeedStart], isError: true);
            return;
        }

        EnsureTextWindow();

        if (_textOutputHost.IsTextWindowVisible)
            _textWindow!.Show();
        else
            _textWindow!.Hide();
    }

    public void OnDisplayModeChanged(string value)
    {
        if (value.Equals(ScreenTranslatorSettingDescriptors.DisplayModeOverlay, StringComparison.OrdinalIgnoreCase))
        {
            if (_textWindow is not null)
            {
                _textWindow.Hide();
                _textOutputHost.IsTextWindowVisible = false;
            }

            var overlaySession = _getOverlaySession();
            if (overlaySession is not null)
            {
                _textOutputHost.IsTextWindowVisible = true;
                overlaySession.SetVisible(true);
            }
        }
        else
        {
            _getOverlaySession()?.SetVisible(false);
        }
    }

    public void OpenHistory()
    {
        if (_historyWindow is null)
        {
            _historyViewModel = new HistoryViewModel(_translationHistoryService, _localizationService);
            _historyWindow = new HistoryWindow { DataContext = _historyViewModel };
        }
        else
        {
            _historyViewModel?.Refresh();
        }

        var owner = Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (!_historyWindow.IsVisible)
        {
            if (owner is not null)
                _historyWindow.Show(owner);
            else
                _historyWindow.Show();
        }

        _historyWindow.Activate();
    }

    public void CloseAuxiliaryWindows()
    {
        if (_historyWindow is not null)
        {
            _historyViewModel?.Detach();
            _historyWindow.ForceClose();
            _historyWindow = null;
            _historyViewModel = null;
        }
    }

    public void CloseTextWindow()
    {
        if (_textWindow is TextWindow tw)
        {
            tw.ForceClose();
            _textWindow = null;
            _textOutputHost.IsTextWindowVisible = false;
        }
        else if (_textWindow is not null)
        {
            _textWindow.Close();
            _textWindow = null;
            _textOutputHost.IsTextWindowVisible = false;
        }
    }

    public void CaptureTextWindowSettings(WindowSettings settings)
    {
        if (_textWindow is null)
            return;

        settings.X = _textWindow.Position.X;
        settings.Y = _textWindow.Position.Y;
        settings.Width = (int)_textWindow.Width;
        settings.Height = (int)_textWindow.Height;
        if (_textWindow.DataContext is TextWindowViewModel vm)
            settings.Topmost = vm.IsTopmost;
    }

    public void UpdateText(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_textWindow?.DataContext is TextWindowViewModel vm)
                vm.RecognizedText = text;
        });
    }

    public void RefreshLocalization()
    {
        if (_textWindow?.DataContext is TextWindowViewModel textVm)
            textVm.RefreshLocalization();
        _historyViewModel?.RefreshLocalization();
    }

    public void ForceWindowRebind()
    {
        if (_textWindow is not null)
        {
            var dc = _textWindow.DataContext;
            _textWindow.DataContext = null;
            _textWindow.DataContext = dc;
        }
    }

    private void EnsureTextWindow()
    {
        if (_textWindow is not null)
            return;

        var screenProfile = _applicationProfileService.LoadScreenTranslatorProfile();
        var vm = new TextWindowViewModel(_localizationService)
        {
            IsTopmost = screenProfile.TextWindow.Topmost
        };
        _textWindow = new TextWindow
        {
            DataContext = vm,
            Width = screenProfile.TextWindow.Width > 0 ? screenProfile.TextWindow.Width : 480,
            Height = screenProfile.TextWindow.Height > 0 ? screenProfile.TextWindow.Height : 320,
        };
        if (screenProfile.TextWindow.X != 0 || screenProfile.TextWindow.Y != 0)
            _textWindow.Position = new PixelPoint(screenProfile.TextWindow.X, screenProfile.TextWindow.Y);
    }
}
