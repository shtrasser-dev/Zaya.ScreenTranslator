using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Update;
using Zaya.ScreenTranslator.Impl.Shared.ViewModels;
using Zaya.ScreenTranslator.Impl.Shared.Views;

namespace Zaya.ScreenTranslator.Impl.Shared;

public partial class App : Application
{
    public static string DataDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zaya", "ScreenTranslator");

    public static string PluginsDirectory { get; set; } =
        Path.Combine(DataDirectory, "plugins");

    private static readonly TimeSpan StartupUpdateCheckInterval = TimeSpan.FromHours(12);

    private ServiceProvider? _serviceProvider;
    private ScreenTranslatorProfile? _screenProfile;
    private MainWindow? _mainWindow;
    private GitHubReleasesClient? _releasesClient;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var channel = HostChannel.Current;
        var earlyProfileService = new ApplicationProfileService();
        var earlyProfile = earlyProfileService.LoadScreenTranslatorProfile();
        LocalizationService.Instance.SetCulture(
            string.IsNullOrWhiteSpace(earlyProfile.UiCulture)
                ? LocalizationService.ResolveSystemUiCulture()
                : earlyProfile.UiCulture);
        var loc = LocalizationService.Instance;
        var checkUpdatesOnStartup = earlyProfile.CheckUpdatesOnStartup
            && ShouldCheckUpdatesOnStartup(earlyProfile);

        _releasesClient = new GitHubReleasesClient();
        var pluginUpdater = new PluginUpdateService(_releasesClient);
        var hostChecker = new HostVersionChecker(_releasesClient);

        Window? progress = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            progress = UpdateDialogs.CreateProgressWindow(loc[LocalizationConstants.Main.WindowTitle]);
            desktopLifetime.MainWindow = progress;
            progress.Show();
            UpdateDialogs.SetProgressStatus(
                progress,
                checkUpdatesOnStartup ? loc[LocalizationConstants.Update.Checking] : loc[LocalizationConstants.Update.PreparingPlugins]);
        }

        try
        {
            if (checkUpdatesOnStartup)
            {
                // 1. Host update (open browser only — no self-replace)
                var hostUpdate = await hostChecker.CheckAsync(channel).ConfigureAwait(true);
                if (hostUpdate.UpdateAvailable && !string.IsNullOrEmpty(hostUpdate.ReleaseHtmlUrl))
                {
                    progress?.Hide();
                    var open = await UpdateDialogs.ShowHostUpdateAsync(
                        progress,
                        hostUpdate.RemoteVersion?.ToString() ?? "?",
                        hostUpdate.ReleaseName).ConfigureAwait(true);
                    if (open)
                        HostVersionChecker.OpenReleasePage(hostUpdate.ReleaseHtmlUrl);
                    progress?.Show();
                }
            }

            // 2. Plugins — before LoadPlugins
            UpdateDialogs.SetProgressStatus(
                progress,
                checkUpdatesOnStartup ? loc[LocalizationConstants.Update.UpdatingPlugins] : loc[LocalizationConstants.Update.PreparingPlugins]);
            var status = new Progress<string>(msg => UpdateDialogs.SetProgressStatus(progress, msg));

            var pluginResult = await pluginUpdater.EnsurePluginsAsync(
                PluginsDirectory,
                channel,
                updateOptional: checkUpdatesOnStartup,
                checkForUpdates: checkUpdatesOnStartup,
                status: status).ConfigureAwait(true);

            if (checkUpdatesOnStartup)
            {
                earlyProfile.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
                earlyProfileService.SaveScreenTranslatorProfile(earlyProfile);
            }

            if (!pluginResult.Success)
            {
                progress?.Close();
                await UpdateDialogs.ShowFatalAsync(
                    loc[LocalizationConstants.Update.PluginsRequiredTitle],
                    pluginResult.ErrorMessage ?? loc[LocalizationConstants.Update.PluginsRequiredBody]).ConfigureAwait(true);
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
                    d.Shutdown(1);
                return;
            }

            PluginLoader.LoadPlugins(PluginsDirectory);
        }
        catch (Exception ex)
        {
            progress?.Close();
            await UpdateDialogs.ShowFatalAsync(loc[LocalizationConstants.Update.StartupErrorTitle], ex.Message).ConfigureAwait(true);
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
                d.Shutdown(1);
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var profileService = _serviceProvider.GetRequiredService<IApplicationProfileService>();
        var localeService = LocalizationService.Instance;

        _screenProfile = profileService.LoadScreenTranslatorProfile();

        Current!.RequestedThemeVariant = _screenProfile.Theme switch
        {
            AppConstants.Theme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Light,
        };

        localeService.SetCulture(_screenProfile.UiCulture);

        profileService.SetActiveProfile(_screenProfile.LastActiveProfileName);
        if (profileService.ActiveProfile is not null)
        {
            var activeName = profileService.ActiveProfile.ScreenTranslatorSettings
                .GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName);
            if (!string.Equals(_screenProfile.LastActiveProfileName, activeName, StringComparison.Ordinal))
            {
                _screenProfile.LastActiveProfileName = activeName;
                profileService.SaveScreenTranslatorProfile(_screenProfile);
            }
        }

        var vm = _serviceProvider.GetRequiredService<MainViewModel>();
        _mainWindow = new MainWindow(vm);
        _mainWindow.ApplyStartupPosition(_screenProfile.MainWindow.X, _screenProfile.MainWindow.Y);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = _mainWindow;
            desktop.Exit += OnExit;
        }

        progress?.Close();
        _mainWindow.Show();

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(ServiceCollection services)
    {
        services.AddSingleton<IApplicationProfileService, ApplicationProfileService>();
        services.AddSingleton<IScreenTranslatorContext, ScreenTranslatorContext>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<TranslationLoopService>();
        services.AddSingleton<TranslationHistoryService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<TextWindowViewModel>();

        services.AddSingleton(LocalizationService.Instance);
        services.AddSingleton(_ => _releasesClient ?? new GitHubReleasesClient());
        services.AddSingleton(sp => new PluginUpdateService(sp.GetRequiredService<GitHubReleasesClient>()));
        services.AddSingleton(sp => new HostVersionChecker(sp.GetRequiredService<GitHubReleasesClient>()));
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _releasesClient?.Dispose();

        if (_mainWindow is null)
            return;

        var profileService = _serviceProvider!.GetRequiredService<IApplicationProfileService>();

        _screenProfile = profileService.LoadScreenTranslatorProfile();

        _screenProfile.MainWindow.X = _mainWindow.Position.X;
        _screenProfile.MainWindow.Y = _mainWindow.Position.Y;

        if (_mainWindow.DataContext is MainViewModel mainVm)
        {
            mainVm.CaptureTextWindowSettings(_screenProfile.TextWindow);
            mainVm.CloseAuxiliaryWindows();
            _screenProfile.DisplayMode = mainVm.SelectedDisplayMode;
        }

        if (profileService.ActiveProfile is not null)
            _screenProfile.LastActiveProfileName = profileService.ActiveProfile.ScreenTranslatorSettings
                .GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName);

        profileService.SaveScreenTranslatorProfile(_screenProfile);

        Environment.Exit(e.ApplicationExitCode);
    }

    private static bool ShouldCheckUpdatesOnStartup(ScreenTranslatorProfile profile)
    {
        if (profile.LastUpdateCheckUtc is not { } last)
            return true;

        return DateTimeOffset.UtcNow - last >= StartupUpdateCheckInterval;
    }
}
