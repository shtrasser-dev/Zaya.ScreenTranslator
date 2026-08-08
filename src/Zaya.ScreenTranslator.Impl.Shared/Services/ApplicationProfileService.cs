using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Converters;
using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class ApplicationProfileService : ObservableObject, IApplicationProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new SettingsJsonConverter() }
    };

    private readonly string _baseDir;
    private readonly string _profilesDir;
    private readonly string _settingsPath;

    private IApplicationProfile? _activeProfile;

    public ApplicationProfileService()
    {
        _baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zaya", "ScreenTranslator");
        _profilesDir = Path.Combine(_baseDir, "profiles");
        _settingsPath = Path.Combine(_baseDir, "settings.json");

        Directory.CreateDirectory(_profilesDir);
    }

    public IApplicationProfile? ActiveProfile => _activeProfile;

    // ── App-level settings ──

    public ScreenTranslatorProfile LoadScreenTranslatorProfile()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                Debug.WriteLine($"[LoadScreen] File not found: {_settingsPath}");
                return CreateDefaultScreenProfile();
            }

            var json = File.ReadAllText(_settingsPath);
            var result = JsonSerializer.Deserialize<ScreenTranslatorProfile>(json, JsonOptions)
                         ?? CreateDefaultScreenProfile();
            if (result.Theme is not AppConstants.Theme.Light and not AppConstants.Theme.Dark)
                result.Theme = AppConstants.Theme.Light;
            if (result.DisplayMode is not AppConstants.DisplayMode.TextWindow and not AppConstants.DisplayMode.Overlay)
                result.DisplayMode = AppConstants.DisplayMode.Overlay;
            if (!LocalizationService.IsSupportedUiCulture(result.UiCulture))
                result.UiCulture = "en";
            Debug.WriteLine($"[LoadScreen] UiCulture={result.UiCulture}, Theme={result.Theme}, DisplayMode={result.DisplayMode}, LastProfile={result.LastActiveProfileName}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LoadScreen] Error: {ex.Message}");
            return CreateDefaultScreenProfile();
        }
    }

    private static ScreenTranslatorProfile CreateDefaultScreenProfile() =>
        new()
        {
            UiCulture = LocalizationService.ResolveSystemUiCulture(),
            TargetLanguage = LocalizationService.ResolveSystemTargetLanguage(),
        };

    public void SaveScreenTranslatorProfile(ScreenTranslatorProfile settings)
    {
        Debug.WriteLine($"[SaveScreen] UiCulture={settings.UiCulture}, Theme={settings.Theme}, LastProfile={settings.LastActiveProfileName}");
        var tmp = _settingsPath + ".tmp";
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(tmp, json);
        File.Move(tmp, _settingsPath, overwrite: true);
        Debug.WriteLine($"[SaveScreen] Written to {_settingsPath}");
    }

    // ── Translation profiles ──

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

            // Missing Default (first launch, or Default was deleted/renamed away while settings still point to it):
            // create it whenever the requested name is the default profile name, or when no profiles exist at all.
            if (wantsDefault || names.Count == 0)
            {
                var defaultProfile = CreateDefaultApplicationProfile(
                    wantsDefault ? name : SettingsConstants.EngineDefaults.ProfileName);
                Save(defaultProfile);
                _activeProfile = defaultProfile;
                OnPropertyChanged(nameof(ActiveProfile));
                return;
            }

            // Requested profile missing, but others exist — activate the first available.
            ActivateFromFile(ProfilePath(names[0]!));
            return;
        }

        ActivateFromFile(path);
    }

    private void ActivateFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var dict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json, JsonOptions);
        if (dict is null)
            return;

        _activeProfile = new ApplicationProfile { Settings = dict };
        OnPropertyChanged(nameof(ActiveProfile));
    }

    private static ApplicationProfile CreateDefaultApplicationProfile(string name)
    {
        var settings = LoadDefaultProfileTemplate();
        if (!settings.TryGetValue(ScreenTranslatorSettingDescriptors.StKey, out var st))
            settings[ScreenTranslatorSettingDescriptors.StKey] = st = new();

        // Persist profileName so the file name and embedded name stay aligned.
        st[ScreenTranslatorSettingDescriptors.ProfileName] = name;
        return new ApplicationProfile { Settings = settings };
    }

    public IApplicationProfile CreateFromDefaultTemplate(string name) =>
        CreateDefaultApplicationProfile(name);

    private static Dictionary<string, Dictionary<string, object>> LoadDefaultProfileTemplate()
    {
        var asm = typeof(ApplicationProfileService).Assembly;
        const string resourceName = "Zaya.ScreenTranslator.Impl.Shared.Services.default-profile.json";

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(stream, JsonOptions)
               ?? new Dictionary<string, Dictionary<string, object>>();
    }

    public void SetActiveProfile(IApplicationProfile profile)
    {
        _activeProfile = profile;
        OnPropertyChanged(nameof(ActiveProfile));
    }

    public List<string> ListProfileNames()
    {
        if (!Directory.Exists(_profilesDir))
            return [];

        return Directory.EnumerateFiles(_profilesDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .ToList()!;
    }

    public void Save(IApplicationProfile profile)
    {
        var name = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName);
        var path = ProfilePath(name);
        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(profile.Settings, JsonOptions);
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    public void Delete(string name)
    {
        var names = ListProfileNames();
        if (names.Count <= 1)
            return; // cannot delete last profile

        var path = ProfilePath(name);
        if (File.Exists(path))
            File.Delete(path);
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
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json, JsonOptions);
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

        var json = File.ReadAllText(oldPath);
        var dict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json, JsonOptions);
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

        var tmp = newPath + ".tmp";
        var outJson = JsonSerializer.Serialize(dict, JsonOptions);
        File.WriteAllText(tmp, outJson);
        File.Move(tmp, newPath, overwrite: true);

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

    // ── Helpers ──

    private string ProfilePath(string name) =>
        Path.Combine(_profilesDir, SanitizeFileName(name) + ".json");

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        return string.IsNullOrWhiteSpace(sanitized) ? "Default" : sanitized;
    }
}
