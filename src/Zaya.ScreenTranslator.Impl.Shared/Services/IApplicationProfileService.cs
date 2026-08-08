using System.ComponentModel;
using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public interface IApplicationProfileService : INotifyPropertyChanged
{
    ScreenTranslatorProfile LoadScreenTranslatorProfile();
    void SaveScreenTranslatorProfile(ScreenTranslatorProfile settings);

    IApplicationProfile? ActiveProfile { get; }
    void SetActiveProfile(string name);
    void SetActiveProfile(IApplicationProfile profile);
    List<string> ListProfileNames();
    void Save(IApplicationProfile profile);
    void Delete(string name);

    /// <summary>Preferred name, or <c>preferred 1</c>, <c>preferred 2</c>, … when taken.</summary>
    string AllocateUniqueProfileName(string preferredName);

    /// <summary>Creates a new in-memory profile from the embedded Default template.</summary>
    IApplicationProfile CreateFromDefaultTemplate(string name);

    /// <summary>Loads a profile JSON file without activating it.</summary>
    bool TryLoadProfileFile(string path, out IApplicationProfile? profile, out string? errorMessage);

    /// <summary>
    /// Renames a profile file and updates the embedded profile name.
    /// Returns false when the new name is empty or already used by another profile.
    /// </summary>
    bool TryRename(string oldName, string newName, out string? errorCode);
}
