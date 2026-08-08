using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

/// <summary>
/// Defers change-event subscription until after the control has loaded and settled,
/// so initial SelectedItem/Text/IsChecked assignment does not notify as a user edit.
/// </summary>
internal static class SettingEditorChangeWiring
{
    public static void AfterInit(Control control, Action subscribe)
    {
        void Schedule() =>
            Dispatcher.UIThread.Post(subscribe, DispatcherPriority.Input);

        if (control.IsLoaded)
        {
            Schedule();
            return;
        }

        void OnLoaded(object? sender, RoutedEventArgs e)
        {
            control.Loaded -= OnLoaded;
            Schedule();
        }

        control.Loaded += OnLoaded;
    }
}
