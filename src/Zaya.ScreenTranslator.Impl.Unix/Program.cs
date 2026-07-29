using System.IO;
using System.Reflection;
using Avalonia;
using Zaya.ScreenTranslator.Impl.Shared;

var pluginsDir = Path.Combine(
    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
    "plugins");

App.PluginsDirectory = pluginsDir;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .LogToTrace()
    .StartWithClassicDesktopLifetime(args);
