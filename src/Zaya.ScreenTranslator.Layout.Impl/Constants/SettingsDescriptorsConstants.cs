using Zaya.Primitives;

namespace Zaya.ScreenTranslator.Layout.Impl.Constants
{
    internal class SettingsDescriptorsConstants
    {
        public static readonly IReadOnlyList<SettingDescriptor> Settings = [
            new EnumSettingDescriptor(OverlayLayoutSettingKeys.Placement, Loc(LocalizationConstants.Overlay.Placement))
            {
                Description = Loc(LocalizationConstants.Overlay.PlacementDesc),
                DefaultValue = OverlayLayoutSettingKeys.PlacementAbove,
                Options =
                [
                    new(OverlayLayoutSettingKeys.PlacementAbove, Loc(LocalizationConstants.Overlay.PlacementAbove)),
                    new(OverlayLayoutSettingKeys.PlacementOver, Loc(LocalizationConstants.Overlay.PlacementOver)),
                    new(OverlayLayoutSettingKeys.PlacementBelow, Loc(LocalizationConstants.Overlay.PlacementBelow)),
                ],
            },
            new EnumSettingDescriptor(OverlayLayoutSettingKeys.TranslateMode, Loc(LocalizationConstants.Overlay.TranslateMode))
            {
                Description = Loc(LocalizationConstants.Overlay.TranslateModeDesc),
                DefaultValue = OverlayLayoutSettingKeys.TranslateModeAlways,
                Options =
                [
                    new(OverlayLayoutSettingKeys.TranslateModeAlways, Loc(LocalizationConstants.Overlay.TranslateModeAlways)),
                    new(OverlayLayoutSettingKeys.TranslateModeOnDemand, Loc(LocalizationConstants.Overlay.TranslateModeOnDemand)),
                ],
            },
            new BooleanSettingDescriptor(OverlayLayoutSettingKeys.FixedFontSize, Loc(LocalizationConstants.Overlay.FixedFontSize))
            {
                Description = Loc(LocalizationConstants.Overlay.FixedFontSizeDesc),
                DefaultValue = false,
            },
            new IntegerSettingDescriptor(OverlayLayoutSettingKeys.FontScale, Loc(LocalizationConstants.Overlay.FontScale))
            {
                Description = Loc(LocalizationConstants.Overlay.FontScaleDesc),
                DefaultValue = 60,
                MinValue = 20,
                MaxValue = 150,
                IsVisible = s => !IsFixedFontSizeEnabled(s),
            },
            new IntegerSettingDescriptor(OverlayLayoutSettingKeys.FontSize, Loc(LocalizationConstants.Overlay.FontSize))
            {
                Description = Loc(LocalizationConstants.Overlay.FontSizeDesc),
                DefaultValue = 16,
                MinValue = 8,
                MaxValue = 200,
                IsVisible = IsFixedFontSizeEnabled,
            },
            new IntegerSettingDescriptor(OverlayLayoutSettingKeys.OffsetYPercent, Loc(LocalizationConstants.Overlay.OffsetYPercent))
            {
                Description = Loc(LocalizationConstants.Overlay.OffsetYPercentDesc),
                DefaultValue = 0,
                MinValue = -200,
                MaxValue = 200,
                IsVisible = s => !IsFixedFontSizeEnabled(s),
            },
            new IntegerSettingDescriptor(OverlayLayoutSettingKeys.OffsetY, Loc(LocalizationConstants.Overlay.OffsetY))
            {
                Description = Loc(LocalizationConstants.Overlay.OffsetYDesc),
                DefaultValue = 4,
                MinValue = -200,
                MaxValue = 200,
                IsVisible = IsFixedFontSizeEnabled,
            },
            new IntegerSettingDescriptor(OverlayLayoutSettingKeys.Padding, Loc(LocalizationConstants.Overlay.Padding))
            {
                Description = Loc(LocalizationConstants.Overlay.PaddingDesc),
                DefaultValue = 2,
                MinValue = 0,
                MaxValue = 40,
            },
            new IntegerSettingDescriptor(OverlayLayoutSettingKeys.HorizonSnapDegrees, Loc(LocalizationConstants.Overlay.HorizonSnapDegrees))
            {
                Description = Loc(LocalizationConstants.Overlay.HorizonSnapDegreesDesc),
                DefaultValue = 10,
                MinValue = 0,
                MaxValue = 45,
            },
            new EnumSettingDescriptor(OverlayLayoutSettingKeys.Background, Loc(LocalizationConstants.Overlay.Background))
            {
                Description = Loc(LocalizationConstants.Overlay.BackgroundDesc),
                DefaultValue = OverlayLayoutSettingKeys.BackgroundSoft,
                Options =
                [
                    new(OverlayLayoutSettingKeys.BackgroundNone, Loc(LocalizationConstants.Overlay.BackgroundNone)),
                    new(OverlayLayoutSettingKeys.BackgroundSoft, Loc(LocalizationConstants.Overlay.BackgroundSoft)),
                    new(OverlayLayoutSettingKeys.BackgroundOpaque, Loc(LocalizationConstants.Overlay.BackgroundOpaque)),
                ],
            },
            new IntegerSettingDescriptor(OverlayLayoutSettingKeys.BackgroundOpacity, Loc(LocalizationConstants.Overlay.BackgroundOpacity))
            {
                Description = Loc(LocalizationConstants.Overlay.BackgroundOpacityDesc),
                DefaultValue = 70,
                MinValue = 0,
                MaxValue = 100,
                IsVisible = IsBackgroundEnabled,
            },
            new EnumSettingDescriptor(OverlayLayoutSettingKeys.BackgroundColor, Loc(LocalizationConstants.Overlay.BackgroundColor))
            {
                DefaultValue = OverlayLayoutSettingKeys.BackgroundColorDark,
                Options =
                [
                    new(OverlayLayoutSettingKeys.BackgroundColorLight, Loc(LocalizationConstants.Overlay.BackgroundColorLight)),
                    new(OverlayLayoutSettingKeys.BackgroundColorDark, Loc(LocalizationConstants.Overlay.BackgroundColorDark)),
                ],
                IsVisible = IsBackgroundEnabled,
            },
            new EnumSettingDescriptor(OverlayLayoutSettingKeys.TextColor, Loc(LocalizationConstants.Overlay.TextColor))
            {
                Description = Loc(LocalizationConstants.Overlay.TextColorDesc),
                DefaultValue = OverlayLayoutSettingKeys.TextColorLight,
                Options =
                [
                    new(OverlayLayoutSettingKeys.TextColorLight, Loc(LocalizationConstants.Overlay.TextColorLight)),
                    new(OverlayLayoutSettingKeys.TextColorDark, Loc(LocalizationConstants.Overlay.TextColorDark)),
                    new(OverlayLayoutSettingKeys.TextColorCream, Loc(LocalizationConstants.Overlay.TextColorCream)),
                    new(OverlayLayoutSettingKeys.TextColorYellow, Loc(LocalizationConstants.Overlay.TextColorYellow)),
                    new(OverlayLayoutSettingKeys.TextColorCyan, Loc(LocalizationConstants.Overlay.TextColorCyan)),
                    new(OverlayLayoutSettingKeys.TextColorLime, Loc(LocalizationConstants.Overlay.TextColorLime)),
                    new(OverlayLayoutSettingKeys.TextColorOrange, Loc(LocalizationConstants.Overlay.TextColorOrange)),
                ],
            },
            new BooleanSettingDescriptor(OverlayLayoutSettingKeys.Outline, Loc(LocalizationConstants.Overlay.Outline))
            {
                Description = Loc(LocalizationConstants.Overlay.OutlineDesc),
                DefaultValue = true,
            },
            new BooleanSettingDescriptor(OverlayLayoutSettingKeys.DebugMode, Loc(LocalizationConstants.Overlay.DebugMode))
            {
                Description = Loc(LocalizationConstants.Overlay.DebugModeDesc),
                DefaultValue = false,
            }
        ];

        private static bool IsFixedFontSizeEnabled(IReadOnlyDictionary<string, object?> s)
            => s.GetValueOrDefault(OverlayLayoutSettingKeys.FixedFontSize) is true;

        private static bool IsBackgroundEnabled(IReadOnlyDictionary<string, object?> s)
            => (s.GetValueOrDefault(OverlayLayoutSettingKeys.Background) as string
                ?? OverlayLayoutSettingKeys.BackgroundSoft)
               != OverlayLayoutSettingKeys.BackgroundNone;

        private static LocalizedString Loc(string key) => new(key, culture => Properties.Resources.ResourceManager.GetString(key, culture)!);
    }
}
