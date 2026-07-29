using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Data;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class LocalizationService : ObservableObject
{
    public static LocalizationService Instance { get; } = new();

    private readonly Properties.Resources _resources = Properties.Resources.Instance;

    public CultureInfo CurrentCulture { get; private set; } = new("en");

    public string this[string key]
    {
        get
        {
            var result = _resources.GetString(key, CurrentCulture);
            if (key == "Btn_Start")
                Debug.WriteLine($"[Loc] key={key}, culture={CurrentCulture.Name}, result='{result}'");
            return result;
        }
    }

    public void SetCulture(string cultureCode)
    {
        Debug.WriteLine($"[SetCulture] Switching from {CurrentCulture.Name} to {cultureCode}");
        CurrentCulture = new CultureInfo(cultureCode);
        CultureInfo.CurrentUICulture = CurrentCulture;
        OnPropertyChanged("Item[]");
    }
}
