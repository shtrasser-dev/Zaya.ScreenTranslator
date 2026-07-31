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

    /// <summary>
    /// Renames a profile file and updates the embedded profile name.
    /// Returns false when the new name is empty or already used by another profile.
    /// </summary>
    bool TryRename(string oldName, string newName, out string? errorCode);
}
