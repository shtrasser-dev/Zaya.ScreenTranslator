using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.ViewModels;

namespace Zaya.ScreenTranslator.Impl.Shared.Views;

internal static class CaptureRegionsDialog
{
    private const double ChromePadding = 16;
    private const double ToolbarEstimate = 48;
    private const double VerticalChrome = ChromePadding * 2 + ToolbarEstimate + 8;
    private const double MinWindowWidth = 920;
    private const double MinWindowHeight = 280;

    public static async Task<CaptureRegionsConfig?> ShowAsync(
        Window owner,
        IApplicationProfile profile,
        nint? targetHwnd,
        CaptureRegionsConfig initial,
        ICaptureRegionsSnapshotService snapshotService,
        ILocalizationService localizationService)
    {
        var vm = new CaptureRegionsViewModel(initial, localizationService);
        var editor = new CaptureRegionsEditorCanvas(vm.Regions);
        vm.AttachEditor(editor);

        var cts = new CancellationTokenSource();
        CaptureRegionsSnapshotService.Snapshot? snapshot = null;

        var dialog = new Window
        {
            Title = localizationService[LocalizationConstants.CaptureRegions.Title],
            Topmost = false,
            CanResize = true,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            MinWidth = MinWindowWidth,
            MinHeight = MinWindowHeight,
            Width = MinWindowWidth,
            Height = Math.Max(MinWindowHeight, 420),
            DataContext = vm,
        };

        dialog.Styles.Add(new Style(x => x.OfType<Button>().Class("tool-active"))
        {
            Setters =
            {
                new Setter(Button.FontWeightProperty, FontWeight.SemiBold),
                new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromArgb(255, 55, 120, 200))),
                new Setter(Button.ForegroundProperty, Brushes.White),
            },
        });

        var clearBtn = MakeButton(localizationService[LocalizationConstants.CaptureRegions.ClearAll], vm.ClearAllCommand);
        var addCaptureBtn = MakeButton(localizationService[LocalizationConstants.CaptureRegions.AddCapture], vm.AddCaptureCommand);
        var addIgnoreBtn = MakeButton(localizationService[LocalizationConstants.CaptureRegions.AddIgnore], vm.AddIgnoreCommand);

        var leftBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        leftBar.Children.Add(addCaptureBtn);
        leftBar.Children.Add(addIgnoreBtn);
        leftBar.Children.Add(clearBtn);

        var saveBtn = MakeButton(localizationService["Settings_Save"], null);
        saveBtn.Click += (_, _) => dialog.Close(true);
        var cancelBtn = MakeButton(localizationService["Settings_Cancel"], null);
        cancelBtn.Click += (_, _) => dialog.Close(false);

        var rightBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        rightBar.Children.Add(saveBtn);
        rightBar.Children.Add(cancelBtn);

        var topBar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(leftBar, 0);
        Grid.SetColumn(rightBar, 1);
        topBar.Children.Add(leftBar);
        topBar.Children.Add(rightBar);

        void SyncToolHighlight()
        {
            SetActiveClass(addCaptureBtn, vm.ActiveDrawKind == CaptureRegionKind.Capture);
            SetActiveClass(addIgnoreBtn, vm.ActiveDrawKind == CaptureRegionKind.Ignore);
        }

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CaptureRegionsViewModel.ActiveDrawKind) or null)
                SyncToolHighlight();
        };
        SyncToolHighlight();

        var waitingText = new TextBlock
        {
            Text = localizationService[LocalizationConstants.CaptureRegions.WaitingCapture],
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24),
            FontSize = 16,
            Opacity = 0.85,
            IsVisible = true,
        };

        var imageHost = new Panel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = true,
        };
        editor.HorizontalAlignment = HorizontalAlignment.Center;
        editor.VerticalAlignment = VerticalAlignment.Center;
        editor.IsVisible = false;
        imageHost.Children.Add(waitingText);
        imageHost.Children.Add(editor);

        var root = new Grid
        {
            Margin = new Thickness(ChromePadding),
            RowDefinitions = new RowDefinitions("Auto,*"),
        };
        Grid.SetRow(topBar, 0);
        Grid.SetRow(imageHost, 1);
        root.Children.Add(topBar);
        root.Children.Add(imageHost);
        dialog.Content = root;

        void FitEditorToHost()
        {
            if (snapshot is null)
                return;

            var scaling = Math.Max(0.1, dialog.RenderScaling);
            var imgW = snapshot.PixelWidth / scaling;
            var imgH = snapshot.PixelHeight / scaling;
            var availW = imageHost.Bounds.Width;
            var availH = imageHost.Bounds.Height;
            if (availW <= 1 || availH <= 1)
            {
                editor.SetBitmap(snapshot.Bitmap, imgW, imgH);
                return;
            }

            // 1:1 when the frame fully fits; otherwise scale down to the host.
            var scale = (imgW <= availW && imgH <= availH)
                ? 1.0
                : Math.Min(availW / imgW, availH / imgH);
            editor.SetBitmap(snapshot.Bitmap, imgW * scale, imgH * scale);
        }

        void ApplySnapshot(CaptureRegionsSnapshotService.Snapshot snap)
        {
            snapshot?.Bitmap.Dispose();
            snapshot = snap;

            waitingText.IsVisible = false;
            editor.IsVisible = true;

            var scaling = Math.Max(0.1, dialog.RenderScaling);
            var imgW = snap.PixelWidth / scaling;
            var imgH = snap.PixelHeight / scaling;
            var naturalW = Math.Max(MinWindowWidth, imgW + ChromePadding * 2);
            var naturalH = Math.Max(MinWindowHeight, imgH + VerticalChrome);

            var screen = dialog.Screens.ScreenFromWindow(dialog) ?? dialog.Screens.Primary;
            if (screen is not null)
            {
                var wa = screen.WorkingArea;
                var waW = wa.Width / scaling;
                var waH = wa.Height / scaling;
                if (naturalW <= waW && naturalH <= waH)
                {
                    dialog.WindowState = WindowState.Normal;
                    dialog.Width = naturalW;
                    dialog.Height = naturalH;
                }
                else
                {
                    dialog.WindowState = WindowState.Maximized;
                }
            }
            else
            {
                dialog.Width = naturalW;
                dialog.Height = naturalH;
            }

            editor.SetBitmap(snap.Bitmap, imgW, imgH);
            FitEditorToHost();
        }

        imageHost.SizeChanged += (_, _) =>
        {
            if (snapshot is not null)
                FitEditorToHost();
        };

        dialog.Opened += (_, _) =>
        {
            if (targetHwnd is null or 0)
            {
                ApplySnapshot(snapshotService.CreatePlaceholderSnapshot());
                return;
            }

            var hwnd = targetHwnd.Value;
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await snapshotService.CaptureUntilStableAsync(
                        profile, hwnd, cts.Token).ConfigureAwait(false);
                    if (result is null || cts.IsCancellationRequested)
                        return;

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (cts.IsCancellationRequested)
                        {
                            result.Bitmap.Dispose();
                            return;
                        }

                        ApplySnapshot(result);
                    });
                }
                catch (OperationCanceledException)
                {
                    // Dialog closed while waiting for a stable frame.
                }
                catch (Exception ex)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        waitingText.Text = string.Format(
                            localizationService.CurrentCulture,
                            localizationService[LocalizationConstants.Status.Error],
                            localizationService.FormatExceptionMessage(ex));
                    });
                }
            });
        };

        dialog.Closed += (_, _) =>
        {
            cts.Cancel();
            snapshot?.Bitmap.Dispose();
            snapshot = null;
        };

        var saved = await dialog.ShowDialog<bool>(owner);
        cts.Cancel();
        return saved ? vm.ResultConfig : null;
    }

    private static void SetActiveClass(Button button, bool active)
    {
        if (active)
        {
            if (!button.Classes.Contains("tool-active"))
                button.Classes.Add("tool-active");
        }
        else
        {
            button.Classes.Remove("tool-active");
        }
    }

    private static Button MakeButton(string text, System.Windows.Input.ICommand? command)
        => new()
        {
            Content = text,
            Command = command,
            MinHeight = 32,
            MinWidth = 120,
            Padding = new Thickness(12, 6),
        };
}
