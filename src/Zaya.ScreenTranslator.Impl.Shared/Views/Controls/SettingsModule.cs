using Avalonia;
using Avalonia.Controls;
using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

public static class SettingsModule
{
    public static readonly AttachedProperty<TranslationModuleKind> KindProperty =
        AvaloniaProperty.RegisterAttached<Control, TranslationModuleKind>(
            "Kind",
            typeof(SettingsModule),
            defaultValue: TranslationModuleKind.None);

    public static TranslationModuleKind GetKind(Control control) => control.GetValue(KindProperty);

    public static void SetKind(Control control, TranslationModuleKind value) => control.SetValue(KindProperty, value);
}
