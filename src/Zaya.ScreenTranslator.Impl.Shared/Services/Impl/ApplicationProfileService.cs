using CommunityToolkit.Mvvm.ComponentModel;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

public sealed class ApplicationProfileService : ObservableObject, IApplicationProfileService
{
    private readonly ILocalizationService _localizationService;
    private readonly IJsonConfigurationService _jsonConfigurationService;
    private readonly IEmbeddedResourceService _embeddedResourceService;
    private readonly IConfigurationPathService _configurationPathService;

    private IApplicationProfile? _activeProfile;

    public ApplicationProfileService(
        ILocalizationService localizationService,
        IJsonConfigurationService jsonConfigurationService,
        IEmbeddedResourceService embeddedResourceService,
        IConfigurationPathService configurationPathService)
    {
        _localizationService = localizationService;
        _jsonConfigurationService = jsonConfigurationService;
        _embeddedResourceService = embeddedResourceService;
        _configurationPathService = configurationPathService;

        Directory.CreateDirectory(_configurationPathService.GetProfilesDirectory());
        EnsureMangaProfileSeed();
    }

    /// <summary>
    /// First install only: when neither app settings nor Manga profile exist,
    /// extract the embedded Manga template into <c>profiles/Manga.json</c>.
    /// </summary>
    private void EnsureMangaProfileSeed()
    {
        var mangaPath = ProfilePath("Manga");
        if (File.Exists(_configurationPathService.GetSettingsFilePath()) || File.Exists(mangaPath))
            return;

        using var stream = _embeddedResourceService.GetStream(EmbeddedResourceConstants.MangaProfileJson);
        using var fs = File.Create(mangaPath);
        stream.CopyTo(fs);
    }

    public IApplicationProfile? ActiveProfile => _activeProfile;

    public ScreenTranslatorProfile LoadScreenTranslatorProfile()
    {
        if (_jsonConfigurationService.TryRead<ScreenTranslatorProfile>(_configurationPathService.GetSettingsFilePath(), out var result))
        {
            if (result.Theme is not AppConstants.Theme.Light and not AppConstants.Theme.Dark)
                result.Theme = AppConstants.Theme.Light;
            if (result.DisplayMode is not AppConstants.DisplayMode.TextWindow and not AppConstants.DisplayMode.Overlay)
                result.DisplayMode = AppConstants.DisplayMode.Overlay;
            if (!_localizationService.IsSupportedUiCulture(result.UiCulture))
                result.UiCulture = "en";
            return result;
        }

        return new()
        {
            UiCulture = _localizationService.ResolveSystemUiCulture(),
            TargetLanguage = _localizationService.ResolveSystemTargetLanguage(),
        };
    }

    public void SaveScreenTranslatorProfile(ScreenTranslatorProfile settings)
    {
        _jsonConfigurationService.Write(_configurationPathService.GetSettingsFilePath(), settings);
    }

    public void SetActiveProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            name = SettingsConstants.EngineDefaults.ProfileName;

        var path = ProfilePath(name);
        if (!File.Exists(path))
        {
            var names = ListProfileNames();
            var wantsDefault = string.Equals(
                name, SettingsConstants.EngineDefaults.ProfileName, StringComparison.OrdinalIgnoreCase);

            if (wantsDefault || names.Count == 0)
            {
                var defaultProfile = CreateDefaultApplicationProfile(
                    wantsDefault ? name : SettingsConstants.EngineDefaults.ProfileName);
                Save(defaultProfile);
                _activeProfile = defaultProfile;
                OnPropertyChanged(nameof(ActiveProfile));
                return;
            }

            ActivateFromFile(ProfilePath(names[0]!));
            return;
        }

        ActivateFromFile(path);
    }

    private void ActivateFromFile(string path)
    {
        var dict = _jsonConfigurationService.Read<Dictionary<string, Dictionary<string, object>>>(path);
        if (dict is null)
            return;

        _activeProfile = new ApplicationProfile { Settings = dict };
        OnPropertyChanged(nameof(ActiveProfile));
    }

    private ApplicationProfile CreateDefaultApplicationProfile(string name)
    {
        var settings = LoadDefaultProfileTemplate();
        if (!settings.TryGetValue(ScreenTranslatorSettingDescriptors.StKey, out var st))
            settings[ScreenTranslatorSettingDescriptors.StKey] = st = new();

        st[ScreenTranslatorSettingDescriptors.ProfileName] = name;
        return new ApplicationProfile { Settings = settings };
    }

    public IApplicationProfile CreateFromDefaultTemplate(string name) =>
        CreateDefaultApplicationProfile(name);

    private Dictionary<string, Dictionary<string, object>> LoadDefaultProfileTemplate()
    {
        using var stream = _embeddedResourceService.GetStream(EmbeddedResourceConstants.DefaultProfileJson);
        return _jsonConfigurationService.Read<Dictionary<string, Dictionary<string, object>>>(stream);
    }

    public void SetActiveProfile(IApplicationProfile profile)
    {
        _activeProfile = profile;
        OnPropertyChanged(nameof(ActiveProfile));
    }

    public List<string> ListProfileNames()
    {
        if (!Directory.Exists(_configurationPathService.GetProfilesDirectory()))
            return [];

        return Directory.EnumerateFiles(_configurationPathService.GetProfilesDirectory(), "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .ToList()!;
    }

    public void Save(IApplicationProfile profile)
    {
        var name = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName);
        _jsonConfigurationService.Write(ProfilePath(name), profile.Settings);
    }

    public void Delete(string name)
    {
        var names = ListProfileNames();
        if (names.Count <= 1)
            return;

        var path = ProfilePath(name);
        if (File.Exists(path))
            File.Delete(path);
    }

    public void ExportTo(string name, string destinationPath)
    {
        var source = ProfilePath(name);
        if (!File.Exists(source))
            throw new FileNotFoundException($"Profile '{name}' not found.", source);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.Copy(source, destinationPath, overwrite: true);
    }

    public string AllocateUniqueProfileName(string preferredName)
    {
        preferredName = preferredName.Trim();
        if (string.IsNullOrWhiteSpace(preferredName))
            preferredName = SettingsConstants.EngineDefaults.ProfileName;

        var existing = new HashSet<string>(ListProfileNames(), StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(preferredName))
            return preferredName;

        for (var i = 1; i <= 1000; i++)
        {
            var candidate = $"{preferredName} {i}";
            if (!existing.Contains(candidate))
                return candidate;
        }

        return $"{preferredName} {Guid.NewGuid():N}"[..Math.Min(40, preferredName.Length + 33)];
    }

    public bool TryLoadProfileFile(string path, out IApplicationProfile? profile, out string? errorMessage)
    {
        profile = null;
        errorMessage = null;
        try
        {
            var dict = _jsonConfigurationService.Read<Dictionary<string, Dictionary<string, object>>>(path);
            if (dict is null)
            {
                errorMessage = "Invalid profile file.";
                return false;
            }

            profile = new ApplicationProfile { Settings = dict };
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool TryRename(string oldName, string newName, out string? errorCode)
    {
        errorCode = null;
        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            errorCode = ProfileConstants.ErrorEmpty;
            return false;
        }

        if (string.Equals(oldName, newName, StringComparison.Ordinal))
            return true;

        var names = ListProfileNames();
        if (names.Any(n =>
                !string.Equals(n, oldName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(n, newName, StringComparison.OrdinalIgnoreCase)))
        {
            errorCode = ProfileConstants.ErrorExists;
            return false;
        }

        var oldPath = ProfilePath(oldName);
        if (!File.Exists(oldPath))
        {
            errorCode = ProfileConstants.ErrorMissing;
            return false;
        }

        var dict = _jsonConfigurationService.Read<Dictionary<string, Dictionary<string, object>>>(oldPath);
        if (dict is null)
        {
            errorCode = ProfileConstants.ErrorMissing;
            return false;
        }

        if (!dict.TryGetValue(ScreenTranslatorSettingDescriptors.StKey, out var st))
        {
            st = new Dictionary<string, object>();
            dict[ScreenTranslatorSettingDescriptors.StKey] = st;
        }

        st[ScreenTranslatorSettingDescriptors.ProfileName] = newName;

        var newPath = ProfilePath(newName);
        var samePath = string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase);

        _jsonConfigurationService.Write(newPath, dict);

        if (!samePath && File.Exists(oldPath))
            File.Delete(oldPath);

        var activeName = _activeProfile?.ScreenTranslatorSettings
            .GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName);
        if (string.Equals(activeName, oldName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(oldName, activeName, StringComparison.OrdinalIgnoreCase))
        {
            _activeProfile = new ApplicationProfile { Settings = dict };
            OnPropertyChanged(nameof(ActiveProfile));
        }

        return true;
    }

    private string ProfilePath(string name) =>
        Path.Combine(_configurationPathService.GetProfilesDirectory(), SanitizeFileName(name) + ".json");

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        return string.IsNullOrWhiteSpace(sanitized) ? "Default" : sanitized;
    }
}
