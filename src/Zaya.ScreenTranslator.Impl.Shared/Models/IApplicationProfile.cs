using Zaya.Primitives;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

public interface IApplicationProfile
{
    SettingDescriptorList ScreenTranslatorSettings { get; }
    Dictionary<string, Dictionary<string, object>> Settings { get; }
}
