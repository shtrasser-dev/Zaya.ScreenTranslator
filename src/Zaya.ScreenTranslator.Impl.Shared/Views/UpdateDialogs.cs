using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Zaya.ScreenTranslator.Impl.Shared.Views;

internal static class UpdateDialogs
{
    public static async Task ShowMessageAsync(Window? owner, string title, string message)
    {
        var window = CreateDialog(title, message, ("OK", true));
        _ = await ShowAsync(owner, window);
    }

    /// <returns>true = open release page; false = later</returns>
    public static Task<bool> ShowHostUpdateAsync(Window? owner, string remoteVersion, string? releaseName)
    {
        var text = string.IsNullOrWhiteSpace(releaseName)
            ? $"A newer ScreenTranslator version is available ({remoteVersion}).\n\nOpen the release page in your browser?"
            : $"{releaseName} is available.\n\nOpen the release page in your browser?";

        var window = CreateDialog("Update available", text, ("Open page", true), ("Later", false));
        return ShowAsync(owner, window);
    }

    public static async Task ShowFatalAsync(string title, string message)
    {
        var window = CreateDialog(title, message, ("Exit", true));
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
            Text = "Please wait…",
            TextWrapping = TextWrapping.Wrap,
        };

        return new Window
        {
            Title = title,
            Width = 420,
            Height = 140,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new Border
            {
                Padding = new Avalonia.Thickness(16),
                Child = status,
            },
            Tag = status,
        };
    }

    public static void SetProgressStatus(Window? window, string message)
    {
        if (window?.Tag is TextBlock text)
            Dispatcher.UIThread.Post(() => text.Text = message);
    }
}
