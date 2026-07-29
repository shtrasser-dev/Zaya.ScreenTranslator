using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.ViewModels;
using Zaya.ScreenTranslator.Impl.Shared.Views;

namespace Zaya.ScreenTranslator.Impl.Shared;

public partial class App : Application
{
    public static string PluginsDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zaya", "ScreenTranslator", "plugins");

    private ServiceProvider? _serviceProvider;
    private ScreenTranslatorProfile? _screenProfile;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        PluginLoader.LoadPlugins(PluginsDirectory);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var profileService = _serviceProvider.GetRequiredService<IApplicationProfileService>();
        var localeService = LocalizationService.Instance;

        // 1. Load app-level settings
        _screenProfile = profileService.LoadScreenTranslatorProfile();

        // 2. Set theme
        Current!.RequestedThemeVariant = _screenProfile.Theme switch
        {
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Light,
        };

        // 3. Set culture
        localeService.SetCulture(_screenProfile.UiCulture);

        // 4. Load last active profile
        profileService.SetActiveProfile(_screenProfile.LastActiveProfileName);

        // 5. Create main window
        var vm = _serviceProvider.GetRequiredService<MainViewModel>();
        _mainWindow = new MainWindow(vm);
        ApplyWindowPosition(_mainWindow, _screenProfile.MainWindow);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = _mainWindow;
            desktop.Exit += OnExit;
        }

        _mainWindow.Show();

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        services.AddSingleton<IApplicationProfileService, ApplicationProfileService>();
        services.AddSingleton<IScreenTranslatorContext, ScreenTranslatorContext>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<TranslationLoopService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<TextWindowViewModel>();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_mainWindow is null)
            return;

        var profileService = _serviceProvider!.GetRequiredService<IApplicationProfileService>();

        // Re-read to avoid overwriting changes saved by Settings dialog
        _screenProfile = profileService.LoadScreenTranslatorProfile();

        // Update runtime-only fields
        _screenProfile.MainWindow.X = _mainWindow.Position.X;
        _screenProfile.MainWindow.Y = _mainWindow.Position.Y;
        _screenProfile.MainWindow.Width = (int)_mainWindow.Width;
        _screenProfile.MainWindow.Height = (int)_mainWindow.Height;

        if (_mainWindow.DataContext is MainViewModel mainVm)
        {
            mainVm.CaptureTextWindowSettings(_screenProfile.TextWindow);
            _screenProfile.DisplayMode = mainVm.SelectedDisplayMode;
        }

        if (profileService.ActiveProfile is not null)
            _screenProfile.LastActiveProfileName = profileService.ActiveProfile.ScreenTranslatorSettings
                .GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName);

        profileService.SaveScreenTranslatorProfile(_screenProfile);

        // OneOCR / onnxruntime / WinRT capture can leave native foreground threads that
        // prevent process exit after Avalonia shuts down.
        Environment.Exit(e.ApplicationExitCode);
    }

    private static void ApplyWindowPosition(Window window, WindowSettings settings)
    {
        if (settings.Width > 0) window.Width = settings.Width;
        if (settings.Height > 0) window.Height = settings.Height;
        if (settings.X != 0 || settings.Y != 0)
            window.Position = new PixelPoint(settings.X, settings.Y);
    }
}
