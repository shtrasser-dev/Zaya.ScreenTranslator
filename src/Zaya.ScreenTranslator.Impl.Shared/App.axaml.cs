using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.DependencyInjection;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Update;
using Zaya.ScreenTranslator.Impl.Shared.ViewModels;
using Zaya.ScreenTranslator.Impl.Shared.Views;

namespace Zaya.ScreenTranslator.Impl.Shared;

public partial class App : Application
{
    private static readonly TimeSpan StartupUpdateCheckInterval = TimeSpan.FromHours(12);

    /// <summary>
    /// Kept alive for the app lifetime so transferred singletons and the shared HttpClient are not disposed.
    /// </summary>
    private ServiceProvider? _bootstrapServiceProvider;
    private ServiceProvider? _serviceProvider;
    private ScreenTranslatorProfile? _screenProfile;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var channel = HostChannel.Current;

        var bootstrapServices = new ServiceCollection();
        BootstrapServiceRegistrar.Register(bootstrapServices);
        _bootstrapServiceProvider = bootstrapServices.BuildServiceProvider();

        var profileService = _bootstrapServiceProvider.GetRequiredService<IApplicationProfileService>();
        var loc = _bootstrapServiceProvider.GetRequiredService<ILocalizationService>();
        var earlyProfile = profileService.LoadScreenTranslatorProfile();
        loc.SetCulture(
            string.IsNullOrWhiteSpace(earlyProfile.UiCulture)
                ? loc.ResolveSystemUiCulture()
                : earlyProfile.UiCulture);

        var checkUpdatesOnStartup = earlyProfile.CheckUpdatesOnStartup
            && ShouldCheckUpdatesOnStartup(earlyProfile);

        Window? progress = null;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            progress = UpdateDialogs.CreateProgressWindow(loc, loc[LocalizationConstants.Main.WindowTitle]);
            desktopLifetime.MainWindow = progress;
            progress.Show();
            UpdateDialogs.SetProgressStatus(
                progress,
                checkUpdatesOnStartup ? loc[LocalizationConstants.Update.Checking] : loc[LocalizationConstants.Update.PreparingPlugins]);
        }

        BootstrapResult bootstrapResult;
        try
        {
            if (checkUpdatesOnStartup)
            {
                var hostChecker = _bootstrapServiceProvider.GetRequiredService<IHostVersionChecker>();
                var hostUpdate = await hostChecker.CheckAsync().ConfigureAwait(true);
                if (hostUpdate.UpdateAvailable && !string.IsNullOrEmpty(hostUpdate.ReleaseHtmlUrl))
                {
                    progress?.Hide();
                    var open = await UpdateDialogs.ShowHostUpdateAsync(
                        progress,
                        loc,
                        hostUpdate.RemoteVersion?.ToString() ?? "?",
                        hostUpdate.ReleaseName).ConfigureAwait(true);
                    if (open)
                        hostChecker.OpenReleasePage(hostUpdate.ReleaseHtmlUrl);
                    progress?.Show();
                }
            }

            var bootstrap = _bootstrapServiceProvider.GetRequiredService<IApplicationBootstrap>();
            var status = new Progress<string>(msg => UpdateDialogs.SetProgressStatus(progress, msg));
            bootstrapResult = await bootstrap.RunAsync(
                channel,
                checkUpdatesOnStartup,
                status).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            progress?.Close();
            await UpdateDialogs.ShowFatalAsync(
                loc,
                loc[LocalizationConstants.Update.StartupErrorTitle],
                ex.Message).ConfigureAwait(true);
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
                d.Shutdown(1);
            return;
        }

        if (!bootstrapResult.Success)
        {
            progress?.Close();
            await UpdateDialogs.ShowFatalAsync(
                loc,
                bootstrapResult.ErrorTitle ?? loc[LocalizationConstants.Update.PluginsRequiredTitle],
                bootstrapResult.ErrorMessage ?? loc[LocalizationConstants.Update.PluginsRequiredBody]).ConfigureAwait(true);
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
                d.Shutdown(1);
            return;
        }

        var transferred = BootstrapServiceRegistrar.ResolveTransferred(_bootstrapServiceProvider);
        var appServices = new ServiceCollection();
        AppServiceRegistrar.Register(appServices, transferred);
        _serviceProvider = appServices.BuildServiceProvider();

        profileService = _serviceProvider.GetRequiredService<IApplicationProfileService>();
        var localeService = _serviceProvider.GetRequiredService<ILocalizationService>();

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

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_mainWindow is null)
            return;

        var profileService = _serviceProvider!.GetRequiredService<IApplicationProfileService>();

        _screenProfile = profileService.LoadScreenTranslatorProfile();

        var pos = _mainWindow.LastKnownPosition;
        if (pos.X == 0 && pos.Y == 0)
            pos = _mainWindow.Position;

        _screenProfile.MainWindow.X = pos.X;
        _screenProfile.MainWindow.Y = pos.Y;

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
