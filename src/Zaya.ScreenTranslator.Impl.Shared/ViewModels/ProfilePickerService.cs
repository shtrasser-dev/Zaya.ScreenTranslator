using Avalonia;
using Avalonia.Controls;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Update;
using Zaya.ScreenTranslator.Impl.Shared.Views;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

internal sealed class ProfilePickerService
{
    private const int MaxNewProfileNumericSuffix = 100;

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

    public IReadOnlyList<string> BuildProfilePickerItems(IReadOnlyList<string> names) =>
        names.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .Append(_host.CreateNewProfileLabel)
            .ToList();

    public void RefreshProfilePicker()
    {
        _host.ProfileNames = _profileService.ListProfileNames();
        _host.ProfilePickerItems = BuildProfilePickerItems(_host.ProfileNames);
    }

    public bool IsCreateNewProfileItem(string name) =>
        string.Equals(name, _host.CreateNewProfileLabel, StringComparison.Ordinal);

    public void ClearProfileError() => _host.ProfileErrorMessage = string.Empty;

    public void SetSelectedProfileSilent(string? name) => _host.SelectedProfileName = name;

    public void PersistLastActiveProfile(string name)
    {
        var screen = _profileService.LoadScreenTranslatorProfile();
        screen.LastActiveProfileName = name;
        _profileService.SaveScreenTranslatorProfile(screen);
    }

    public Task OnProfilePickedFromListAsync(string? name)
    {
        ClearProfileError();

        if (_host.SuppressProfileChange || string.IsNullOrEmpty(name))
            return Task.CompletedTask;

        if (IsCreateNewProfileItem(name))
            return CreateNewProfileAsync();

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
        if (string.IsNullOrEmpty(text) || IsCreateNewProfileItem(text))
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
            SetSelectedProfileSilent(text);
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

        var owner = Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

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
            RefreshProfilePicker();

            var next = _host.ProfileNames.FirstOrDefault();
            if (string.IsNullOrEmpty(next))
            {
                _host.ProfileErrorMessage = _host.Loc[LocalizationConstants.Profile.DeleteLast];
                return;
            }

            _profileService.SetActiveProfile(next);
            _host.CommittedProfileName = next;
            SetSelectedProfileSilent(next);
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

            var copy = _settingsService.BeginEdit();
            _settingsService.CommitEditAsNew(name, copy);

            _host.CommittedProfileName = name;
            SetSelectedProfileSilent(name);
            RefreshProfilePicker();
            PersistLastActiveProfile(name);
            _host.ReloadSettingsIfOpen();
        }
        finally
        {
            _host.SuppressProfileChange = false;
        }

        return Task.CompletedTask;
    }

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
