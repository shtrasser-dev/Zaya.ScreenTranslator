using Avalonia;
using Zaya.ScreenTranslator.Impl.Shared;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .With(new SkiaOptions { UseOpacitySaveLayer = true })
    .LogToTrace()
    .StartWithClassicDesktopLifetime(args);
