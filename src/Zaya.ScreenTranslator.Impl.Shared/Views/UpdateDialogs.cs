using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Views;

internal static class UpdateDialogs
{
    private const double ProgressWindowSize = 360;

    private static LocalizationService Loc => LocalizationService.Instance;

    public static async Task ShowMessageAsync(Window? owner, string title, string message)
    {
        var window = CreateDialog(title, message, (Loc[LocalizationConstants.Dialog.Ok], true));
        _ = await ShowAsync(owner, window);
    }

    /// <returns>true = open release page; false = later</returns>
    public static Task<bool> ShowHostUpdateAsync(Window? owner, string remoteVersion, string? releaseName)
    {
        var text = string.IsNullOrWhiteSpace(releaseName)
            ? string.Format(Loc.CurrentCulture, Loc[LocalizationConstants.Update.AvailableBody], remoteVersion)
            : string.Format(Loc.CurrentCulture, Loc[LocalizationConstants.Update.AvailableBodyNamed], releaseName);

        var window = CreateDialog(
            Loc[LocalizationConstants.Update.AvailableTitle],
            text,
            (Loc[LocalizationConstants.Update.OpenPage], true),
            (Loc[LocalizationConstants.Update.Later], false));
        return ShowAsync(owner, window);
    }

    public static Task<bool> ShowConfirmAsync(
        Window? owner,
        string title,
        string message,
        string confirmLabel,
        string cancelLabel)
    {
        var window = CreateDialog(title, message, (confirmLabel, true), (cancelLabel, false));
        return ShowAsync(owner, window);
    }

    public static async Task ShowFatalAsync(string title, string message)
    {
        var window = CreateDialog(title, message, (Loc[LocalizationConstants.Dialog.Exit], true));
        await ShowAsync(null, window);
    }

    private static async Task<bool> ShowAsync(Window? owner, Window window)
    {
        if (owner is { IsVisible: true })
            return await window.ShowDialog<bool>(owner);

        var tcs = new TaskCompletionSource<bool>();
        window.Closed += (_, _) => tcs.TrySetResult(window.Tag is true);

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var previous = desktop.MainWindow;
            desktop.MainWindow = window;
            window.Show();
            var result = await tcs.Task;
            if (ReferenceEquals(desktop.MainWindow, window))
                desktop.MainWindow = previous;
            return result;
        }

        window.Show();
        return await tcs.Task;
    }

    private static Window CreateDialog(string title, string message, params (string Label, bool Result)[] buttons)
    {
        var window = new Window
        {
            Title = title,
            Width = 420,
            MinHeight = 160,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 16,
        };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        });

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        foreach (var (label, buttonResult) in buttons)
        {
            var btn = new Button
            {
                Content = label,
                MinWidth = 88,
                IsDefault = buttonResult,
            };
            btn.Click += (_, _) =>
            {
                window.Tag = buttonResult;
                window.Close(buttonResult);
            };
            buttonRow.Children.Add(btn);
        }

        panel.Children.Add(buttonRow);
        window.Content = panel;
        return window;
    }

    public static Window CreateProgressWindow(string title)
    {
        var status = new TextBlock
        {
            Text = Loc[LocalizationConstants.Status.PleaseWait],
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(16, 8, 16, 12),
            Foreground = Brushes.White,
            FontSize = 13,
            Effect = new DropShadowEffect
            {
                BlurRadius = 6,
                Color = Colors.Black,
                Opacity = 0.85,
                OffsetX = 0,
                OffsetY = 1,
            },
        };

        var content = new DockPanel();
        DockPanel.SetDock(status, Dock.Bottom);
        content.Children.Add(status);

        if (TryLoadLogoBitmap() is { } logo)
        {
            content.Children.Add(new Image
            {
                Source = logo,
                Opacity = 1,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = false,
            });
        }

        return new Window
        {
            Title = title,
            Width = ProgressWindowSize,
            Height = ProgressWindowSize,
            MinWidth = ProgressWindowSize,
            MinHeight = ProgressWindowSize,
            MaxWidth = ProgressWindowSize,
            MaxHeight = ProgressWindowSize,
            CanResize = false,
            ShowInTaskbar = false,
            WindowDecorations = WindowDecorations.None,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Background = Brushes.Transparent,
            TransparencyBackgroundFallback = Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = content,
            Tag = status,
        };
    }

    private static Bitmap? TryLoadLogoBitmap()
    {
        try
        {
            var uri = new Uri("avares://Zaya.ScreenTranslator.Impl.Shared/Assets/logo.png");
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    public static void SetProgressStatus(Window? window, string message)
    {
        if (window?.Tag is TextBlock text)
            Dispatcher.UIThread.Post(() => text.Text = message);
    }
}
