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

    private readonly IApplicationProfileService _applicationProfileService;
    private readonly ISettingsService _settingsService;
    private readonly IProfilePickerHost _profilePickerHost;

    public ProfilePickerService(
        IApplicationProfileService applicationProfileService,
        ISettingsService settingsService,
        IProfilePickerHost profilePickerHost)
    {
        _applicationProfileService = applicationProfileService;
        _settingsService = settingsService;
        _profilePickerHost = profilePickerHost;
    }

    public void RefreshProfilePicker()
    {
        var selected = _profilePickerHost.CommittedProfileName ?? _profilePickerHost.SelectedProfileName;
        _profilePickerHost.ProfileNames = _applicationProfileService.ListProfileNames()
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        SetSelectedProfileSilent(selected);
    }

    public bool IsCreateNewProfileItem(string name) =>
        string.Equals(name, _profilePickerHost.CreateNewProfileLabel, StringComparison.Ordinal);

    public bool IsCopyCurrentProfileItem(string name) =>
        string.Equals(name, _profilePickerHost.CopyCurrentProfileLabel, StringComparison.Ordinal);

    public bool IsImportProfileItem(string name) =>
        string.Equals(name, _profilePickerHost.ImportProfileLabel, StringComparison.Ordinal);

    public bool IsSpecialProfilePickerItem(string? name) =>
        !string.IsNullOrEmpty(name)
        && (IsCreateNewProfileItem(name) || IsCopyCurrentProfileItem(name) || IsImportProfileItem(name));

    public void ClearProfileError() => _profilePickerHost.ProfileErrorMessage = string.Empty;

    public void SetSelectedProfileSilent(string? name) =>
        _profilePickerHost.SelectedProfileName = name;

    public void PersistLastActiveProfile(string name)
    {
        var screen = _applicationProfileService.LoadScreenTranslatorProfile();
        screen.LastActiveProfileName = name;
        _applicationProfileService.SaveScreenTranslatorProfile(screen);
    }

    public Task SelectProfileAsync(string? name)
    {
        ClearProfileError();

        if (_profilePickerHost.SuppressProfileChange || string.IsNullOrEmpty(name))
            return Task.CompletedTask;

        if (string.Equals(name, _profilePickerHost.CommittedProfileName, StringComparison.Ordinal))
            return Task.CompletedTask;

        _applicationProfileService.SetActiveProfile(name);
        _profilePickerHost.CommittedProfileName = name;
        SetSelectedProfileSilent(name);
        PersistLastActiveProfile(name);
        _profilePickerHost.ReloadSettingsIfOpen();
        return Task.CompletedTask;
    }

    public bool CommitProfileRename(string? editedText)
    {
        if (_profilePickerHost.SuppressProfileChange)
            return true;

        ClearProfileError();

        var text = editedText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text) || IsSpecialProfilePickerItem(text))
        {
            _profilePickerHost.ProfileErrorMessage = _profilePickerHost.Loc[LocalizationConstants.SaveAsNew.ErrorEmpty];
            return false;
        }

        var current = _profilePickerHost.CommittedProfileName;
        if (string.IsNullOrEmpty(current))
            return false;

        if (string.Equals(current, text, StringComparison.Ordinal))
            return true;

        if (!_applicationProfileService.TryRename(current, text, out var errorCode))
        {
            _profilePickerHost.ProfileErrorMessage = errorCode switch
            {
                ProfileConstants.ErrorEmpty => _profilePickerHost.Loc[LocalizationConstants.SaveAsNew.ErrorEmpty],
                ProfileConstants.ErrorExists => _profilePickerHost.Loc[LocalizationConstants.SaveAsNew.ErrorExists],
                _ => _profilePickerHost.Loc[LocalizationConstants.SaveAsNew.ErrorExists],
            };
            return false;
        }

        _profilePickerHost.SuppressProfileChange = true;
        try
        {
            _profilePickerHost.CommittedProfileName = text;
            RefreshProfilePicker();
            PersistLastActiveProfile(text);
            _profilePickerHost.ReloadSettingsIfOpen();
        }
        finally
        {
            _profilePickerHost.SuppressProfileChange = false;
        }

        return true;
    }

    public async Task DeleteProfileAsync(Func<bool> canDelete, Action notifyCanExecuteChanged)
    {
        ClearProfileError();

        var name = _profilePickerHost.CommittedProfileName;
        if (string.IsNullOrEmpty(name) || !canDelete())
        {
            _profilePickerHost.ProfileErrorMessage = _profilePickerHost.Loc[LocalizationConstants.Profile.DeleteLast];
            return;
        }

        var owner = GetOwnerWindow();

        var confirmed = await UpdateDialogs.ShowConfirmAsync(
            owner,
            _profilePickerHost.Loc[LocalizationConstants.Profile.DeleteTitle],
            string.Format(_profilePickerHost.Loc[LocalizationConstants.Profile.DeleteConfirm], name),
            _profilePickerHost.Loc[LocalizationConstants.Profile.Delete],
            _profilePickerHost.Loc[LocalizationConstants.SaveAsNew.Cancel]);
        if (!confirmed)
            return;

        _profilePickerHost.SuppressProfileChange = true;
        try
        {
            _applicationProfileService.Delete(name);
            var next = _applicationProfileService.ListProfileNames().FirstOrDefault();
            if (string.IsNullOrEmpty(next))
            {
                RefreshProfilePicker();
                _profilePickerHost.ProfileErrorMessage = _profilePickerHost.Loc[LocalizationConstants.Profile.DeleteLast];
                return;
            }

            _applicationProfileService.SetActiveProfile(next);
            _profilePickerHost.CommittedProfileName = next;
            RefreshProfilePicker();
            PersistLastActiveProfile(next);
            _profilePickerHost.ReloadSettingsIfOpen();
        }
        finally
        {
            _profilePickerHost.SuppressProfileChange = false;
            notifyCanExecuteChanged();
        }
    }

    public Task CreateNewProfileAsync()
    {
        if (_profilePickerHost.SuppressProfileChange)
            return Task.CompletedTask;

        ClearProfileError();
        _profilePickerHost.SuppressProfileChange = true;
        try
        {
            var name = AllocateNewProfileName();
            if (name is null)
            {
                _profilePickerHost.ProfileErrorMessage = string.Format(
                    _profilePickerHost.Loc[LocalizationConstants.Profile.CreateLimit],
                    _profilePickerHost.Loc[LocalizationConstants.Profile.NewName],
                    MaxNewProfileNumericSuffix);
                SetSelectedProfileSilent(_profilePickerHost.CommittedProfileName);
                return Task.CompletedTask;
            }

            var profile = _applicationProfileService.CreateFromDefaultTemplate(name);
            _applicationProfileService.Save(profile);
            _applicationProfileService.SetActiveProfile(profile);
            ActivateCreatedProfile(name);
        }
        finally
        {
            _profilePickerHost.SuppressProfileChange = false;
        }

        return Task.CompletedTask;
    }

    public Task CopyCurrentProfileAsync()
    {
        if (_profilePickerHost.SuppressProfileChange)
            return Task.CompletedTask;

        ClearProfileError();
        var current = _profilePickerHost.CommittedProfileName;
        if (string.IsNullOrEmpty(current))
        {
            SetSelectedProfileSilent(_profilePickerHost.CommittedProfileName);
            return Task.CompletedTask;
        }

        _profilePickerHost.SuppressProfileChange = true;
        try
        {
            var name = _applicationProfileService.AllocateUniqueProfileName(current);
            var copy = _settingsService.BeginEdit();
            _settingsService.CommitEditAsNew(name, copy);
            ActivateCreatedProfile(name);
        }
        finally
        {
            _profilePickerHost.SuppressProfileChange = false;
        }

        return Task.CompletedTask;
    }

    private void ActivateCreatedProfile(string name)
    {
        _profilePickerHost.CommittedProfileName = name;
        RefreshProfilePicker();
        PersistLastActiveProfile(name);
        _profilePickerHost.ReloadSettingsIfOpen();
    }

    public async Task ImportProfileAsync()
    {
        if (_profilePickerHost.SuppressProfileChange)
            return;

        var owner = GetOwnerWindow();
        var topLevel = owner is null ? null : TopLevel.GetTopLevel(owner);
        if (topLevel is null)
        {
            SetSelectedProfileSilent(_profilePickerHost.CommittedProfileName);
            return;
        }

        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = _profilePickerHost.Loc[LocalizationConstants.Profile.Import],
                AllowMultiple = false,
                FileTypeFilter = [JsonFileType],
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _profilePickerHost.ProfileErrorMessage = string.Format(
                _profilePickerHost.Loc[LocalizationConstants.Profile.ImportFailed], ex.Message);
            SetSelectedProfileSilent(_profilePickerHost.CommittedProfileName);
            return;
        }

        if (files.Count == 0)
        {
            SetSelectedProfileSilent(_profilePickerHost.CommittedProfileName);
            return;
        }

        var path = files[0].TryGetLocalPath();
        string? loadError = null;
        if (string.IsNullOrWhiteSpace(path)
            || !_applicationProfileService.TryLoadProfileFile(path, out var loaded, out loadError)
            || loaded is null)
        {
            _profilePickerHost.ProfileErrorMessage = string.Format(
                _profilePickerHost.Loc[LocalizationConstants.Profile.ImportFailed],
                loadError ?? "Invalid path.");
            SetSelectedProfileSilent(_profilePickerHost.CommittedProfileName);
            return;
        }

        var preferred = Path.GetFileNameWithoutExtension(path);
        var name = _applicationProfileService.AllocateUniqueProfileName(preferred);

        _profilePickerHost.SuppressProfileChange = true;
        try
        {
            loaded.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.ProfileName] = name;
            _applicationProfileService.Save(loaded);
            _applicationProfileService.SetActiveProfile(name);
            _profilePickerHost.CommittedProfileName = name;
            RefreshProfilePicker();
            PersistLastActiveProfile(name);
            _profilePickerHost.ReloadSettingsIfOpen();
            ClearProfileError();
        }
        catch (Exception ex)
        {
            _profilePickerHost.ProfileErrorMessage = string.Format(
                _profilePickerHost.Loc[LocalizationConstants.Profile.ImportFailed], ex.Message);
            SetSelectedProfileSilent(_profilePickerHost.CommittedProfileName);
        }
        finally
        {
            _profilePickerHost.SuppressProfileChange = false;
        }
    }

    public async Task ExportProfileAsync(Window? ownerWindow = null)
    {
        ClearProfileError();

        var profile = _applicationProfileService.ActiveProfile;
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
                Title = _profilePickerHost.Loc[LocalizationConstants.Profile.Export],
                SuggestedFileName = $"{profileName}.json",
                DefaultExtension = "json",
                FileTypeChoices = [JsonFileType],
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _profilePickerHost.ProfileErrorMessage = string.Format(
                _profilePickerHost.Loc[LocalizationConstants.Profile.ExportFailed], ex.Message);
            return;
        }

        if (file is null)
            return;

        try
        {
            var path = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                _profilePickerHost.ProfileErrorMessage = string.Format(
                    _profilePickerHost.Loc[LocalizationConstants.Profile.ExportFailed],
                    "Invalid path.");
                return;
            }

            _applicationProfileService.ExportTo(profileName, path);
        }
        catch (Exception ex)
        {
            _profilePickerHost.ProfileErrorMessage = string.Format(
                _profilePickerHost.Loc[LocalizationConstants.Profile.ExportFailed], ex.Message);
        }
    }

    private static Window? GetOwnerWindow() =>
        Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

    private string? AllocateNewProfileName()
    {
        var baseName = _profilePickerHost.Loc[LocalizationConstants.Profile.NewName];
        var existing = new HashSet<string>(
            _applicationProfileService.ListProfileNames(),
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
