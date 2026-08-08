using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Update;
using Zaya.ScreenTranslator.Impl.Shared.Views;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

internal sealed class ProfilePickerService
{
    private const int MaxNewProfileNumericSuffix = 100;

    private static readonly FilePickerFileType JsonFileType = new("JSON")
    {
        Patterns = ["*.json"],
    };

    private readonly IApplicationProfileService _profileService;
    private readonly ISettingsService _settingsService;
    private readonly IProfilePickerHost _host;

    public ProfilePickerService(
        IApplicationProfileService profileService,
        ISettingsService settingsService,
        IProfilePickerHost host)
    {
        _profileService = profileService;
        _settingsService = settingsService;
        _host = host;
    }

    public void RefreshProfilePicker()
    {
        var selected = _host.CommittedProfileName ?? _host.SelectedProfileName;
        _host.ProfileNames = _profileService.ListProfileNames()
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        SetSelectedProfileSilent(selected);
    }

    public bool IsCreateNewProfileItem(string name) =>
        string.Equals(name, _host.CreateNewProfileLabel, StringComparison.Ordinal);

    public bool IsCopyCurrentProfileItem(string name) =>
        string.Equals(name, _host.CopyCurrentProfileLabel, StringComparison.Ordinal);

    public bool IsImportProfileItem(string name) =>
        string.Equals(name, _host.ImportProfileLabel, StringComparison.Ordinal);

    public bool IsSpecialProfilePickerItem(string? name) =>
        !string.IsNullOrEmpty(name)
        && (IsCreateNewProfileItem(name) || IsCopyCurrentProfileItem(name) || IsImportProfileItem(name));

    public void ClearProfileError() => _host.ProfileErrorMessage = string.Empty;

    public void SetSelectedProfileSilent(string? name) =>
        _host.SelectedProfileName = name;

    public void PersistLastActiveProfile(string name)
    {
        var screen = _profileService.LoadScreenTranslatorProfile();
        screen.LastActiveProfileName = name;
        _profileService.SaveScreenTranslatorProfile(screen);
    }

    public Task SelectProfileAsync(string? name)
    {
        ClearProfileError();

        if (_host.SuppressProfileChange || string.IsNullOrEmpty(name))
            return Task.CompletedTask;

        if (string.Equals(name, _host.CommittedProfileName, StringComparison.Ordinal))
            return Task.CompletedTask;

        _profileService.SetActiveProfile(name);
        _host.CommittedProfileName = name;
        SetSelectedProfileSilent(name);
        PersistLastActiveProfile(name);
        _host.ReloadSettingsIfOpen();
        return Task.CompletedTask;
    }

    public bool CommitProfileRename(string? editedText)
    {
        if (_host.SuppressProfileChange)
            return true;

        ClearProfileError();

        var text = editedText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text) || IsSpecialProfilePickerItem(text))
        {
            _host.ProfileErrorMessage = _host.Loc[LocalizationConstants.SaveAsNew.ErrorEmpty];
            return false;
        }

        var current = _host.CommittedProfileName;
        if (string.IsNullOrEmpty(current))
            return false;

        if (string.Equals(current, text, StringComparison.Ordinal))
            return true;

        if (!_profileService.TryRename(current, text, out var errorCode))
        {
            _host.ProfileErrorMessage = errorCode switch
            {
                ProfileConstants.ErrorEmpty => _host.Loc[LocalizationConstants.SaveAsNew.ErrorEmpty],
                ProfileConstants.ErrorExists => _host.Loc[LocalizationConstants.SaveAsNew.ErrorExists],
                _ => _host.Loc[LocalizationConstants.SaveAsNew.ErrorExists],
            };
            return false;
        }

        _host.SuppressProfileChange = true;
        try
        {
            _host.CommittedProfileName = text;
            RefreshProfilePicker();
            PersistLastActiveProfile(text);
            _host.ReloadSettingsIfOpen();
        }
        finally
        {
            _host.SuppressProfileChange = false;
        }

        return true;
    }

    public async Task DeleteProfileAsync(Func<bool> canDelete, Action notifyCanExecuteChanged)
    {
        ClearProfileError();

        var name = _host.CommittedProfileName;
        if (string.IsNullOrEmpty(name) || !canDelete())
        {
            _host.ProfileErrorMessage = _host.Loc[LocalizationConstants.Profile.DeleteLast];
            return;
        }

        var owner = GetOwnerWindow();

        var confirmed = await UpdateDialogs.ShowConfirmAsync(
            owner,
            _host.Loc[LocalizationConstants.Profile.DeleteTitle],
            string.Format(_host.Loc[LocalizationConstants.Profile.DeleteConfirm], name),
            _host.Loc[LocalizationConstants.Profile.Delete],
            _host.Loc[LocalizationConstants.SaveAsNew.Cancel]);
        if (!confirmed)
            return;

        _host.SuppressProfileChange = true;
        try
        {
            _profileService.Delete(name);
            var next = _profileService.ListProfileNames().FirstOrDefault();
            if (string.IsNullOrEmpty(next))
            {
                RefreshProfilePicker();
                _host.ProfileErrorMessage = _host.Loc[LocalizationConstants.Profile.DeleteLast];
                return;
            }

            _profileService.SetActiveProfile(next);
            _host.CommittedProfileName = next;
            RefreshProfilePicker();
            PersistLastActiveProfile(next);
            _host.ReloadSettingsIfOpen();
        }
        finally
        {
            _host.SuppressProfileChange = false;
            notifyCanExecuteChanged();
        }
    }

    public Task CreateNewProfileAsync()
    {
        if (_host.SuppressProfileChange)
            return Task.CompletedTask;

        ClearProfileError();
        _host.SuppressProfileChange = true;
        try
        {
            var name = AllocateNewProfileName();
            if (name is null)
            {
                _host.ProfileErrorMessage = string.Format(
                    _host.Loc[LocalizationConstants.Profile.CreateLimit],
                    _host.Loc[LocalizationConstants.Profile.NewName],
                    MaxNewProfileNumericSuffix);
                SetSelectedProfileSilent(_host.CommittedProfileName);
                return Task.CompletedTask;
            }

            var profile = _profileService.CreateFromDefaultTemplate(name);
            _profileService.Save(profile);
            _profileService.SetActiveProfile(profile);
            ActivateCreatedProfile(name);
        }
        finally
        {
            _host.SuppressProfileChange = false;
        }

        return Task.CompletedTask;
    }

    public Task CopyCurrentProfileAsync()
    {
        if (_host.SuppressProfileChange)
            return Task.CompletedTask;

        ClearProfileError();
        var current = _host.CommittedProfileName;
        if (string.IsNullOrEmpty(current))
        {
            SetSelectedProfileSilent(_host.CommittedProfileName);
            return Task.CompletedTask;
        }

        _host.SuppressProfileChange = true;
        try
        {
            var name = _profileService.AllocateUniqueProfileName(current);
            var copy = _settingsService.BeginEdit();
            _settingsService.CommitEditAsNew(name, copy);
            ActivateCreatedProfile(name);
        }
        finally
        {
            _host.SuppressProfileChange = false;
        }

        return Task.CompletedTask;
    }

    private void ActivateCreatedProfile(string name)
    {
        _host.CommittedProfileName = name;
        RefreshProfilePicker();
        PersistLastActiveProfile(name);
        _host.ReloadSettingsIfOpen();
    }

    public async Task ImportProfileAsync()
    {
        if (_host.SuppressProfileChange)
            return;

        var owner = GetOwnerWindow();
        var topLevel = owner is null ? null : TopLevel.GetTopLevel(owner);
        if (topLevel is null)
        {
            SetSelectedProfileSilent(_host.CommittedProfileName);
            return;
        }

        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = _host.Loc[LocalizationConstants.Profile.Import],
                AllowMultiple = false,
                FileTypeFilter = [JsonFileType],
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _host.ProfileErrorMessage = string.Format(
                _host.Loc[LocalizationConstants.Profile.ImportFailed], ex.Message);
            SetSelectedProfileSilent(_host.CommittedProfileName);
            return;
        }

        if (files.Count == 0)
        {
            SetSelectedProfileSilent(_host.CommittedProfileName);
            return;
        }

        var path = files[0].TryGetLocalPath();
        string? loadError = null;
        if (string.IsNullOrWhiteSpace(path)
            || !_profileService.TryLoadProfileFile(path, out var loaded, out loadError)
            || loaded is null)
        {
            _host.ProfileErrorMessage = string.Format(
                _host.Loc[LocalizationConstants.Profile.ImportFailed],
                loadError ?? "Invalid path.");
            SetSelectedProfileSilent(_host.CommittedProfileName);
            return;
        }

        var preferred = Path.GetFileNameWithoutExtension(path);
        var name = _profileService.AllocateUniqueProfileName(preferred);

        _host.SuppressProfileChange = true;
        try
        {
            loaded.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.ProfileName] = name;
            _profileService.Save(loaded);
            _profileService.SetActiveProfile(name);
            _host.CommittedProfileName = name;
            RefreshProfilePicker();
            PersistLastActiveProfile(name);
            _host.ReloadSettingsIfOpen();
            ClearProfileError();
        }
        catch (Exception ex)
        {
            _host.ProfileErrorMessage = string.Format(
                _host.Loc[LocalizationConstants.Profile.ImportFailed], ex.Message);
            SetSelectedProfileSilent(_host.CommittedProfileName);
        }
        finally
        {
            _host.SuppressProfileChange = false;
        }
    }

    public async Task ExportProfileAsync(Window? ownerWindow = null)
    {
        ClearProfileError();

        var profile = _profileService.ActiveProfile;
        if (profile is null)
            return;

        var profileName = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName);
        if (string.IsNullOrWhiteSpace(profileName))
            profileName = SettingsConstants.EngineDefaults.ProfileName;

        var owner = ownerWindow ?? GetOwnerWindow();
        var topLevel = owner is null ? null : TopLevel.GetTopLevel(owner);
        if (topLevel is null)
            return;

        IStorageFile? file;
        try
        {
            file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = _host.Loc[LocalizationConstants.Profile.Export],
                SuggestedFileName = $"{profileName}.json",
                DefaultExtension = "json",
                FileTypeChoices = [JsonFileType],
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _host.ProfileErrorMessage = string.Format(
                _host.Loc[LocalizationConstants.Profile.ExportFailed], ex.Message);
            return;
        }

        if (file is null)
            return;

        try
        {
            profile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.ProfileName]
                = profileName;

            var json = System.Text.Json.JsonSerializer.Serialize(
                profile.Settings,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new Converters.SettingsJsonConverter() },
                });

            var path = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                await File.WriteAllTextAsync(path, json).ConfigureAwait(true);
            }
            else
            {
                await using var stream = await file.OpenWriteAsync().ConfigureAwait(true);
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(json).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _host.ProfileErrorMessage = string.Format(
                _host.Loc[LocalizationConstants.Profile.ExportFailed], ex.Message);
        }
    }

    private static Window? GetOwnerWindow() =>
        Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

    private string? AllocateNewProfileName()
    {
        var baseName = _host.Loc[LocalizationConstants.Profile.NewName];
        var existing = new HashSet<string>(
            _profileService.ListProfileNames(),
            StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(baseName))
            return baseName;

        for (var i = 1; i <= MaxNewProfileNumericSuffix; i++)
        {
            var candidate = $"{baseName} {i}";
            if (!existing.Contains(candidate))
                return candidate;
        }

        return null;
    }
}
