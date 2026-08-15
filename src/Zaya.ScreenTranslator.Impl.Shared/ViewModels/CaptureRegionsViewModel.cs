using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Views;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

public sealed partial class CaptureRegionsViewModel : ObservableObject
{
    private CaptureRegionsEditorCanvas? _editor;

    public CaptureRegionsViewModel(CaptureRegionsConfig initial, ILocalizationService localizationService)
    {
        Regions = [];
        foreach (var r in initial.CaptureRegions)
            Regions.Add(new EditableCaptureRegion { Kind = CaptureRegionKind.Capture, Rect = r });
        foreach (var r in initial.IgnoreRegions)
            Regions.Add(new EditableCaptureRegion { Kind = CaptureRegionKind.Ignore, Rect = r });

        Loc = new LocalizedStrings(localizationService);
    }

    public LocalizedStrings Loc { get; }

    public List<EditableCaptureRegion> Regions { get; }

    [ObservableProperty] private CaptureRegionKind? _activeDrawKind;

    public CaptureRegionsConfig ResultConfig => new()
    {
        CaptureRegions = Regions.Where(r => r.Kind == CaptureRegionKind.Capture).Select(r => r.Rect).ToList(),
        IgnoreRegions = Regions.Where(r => r.Kind == CaptureRegionKind.Ignore).Select(r => r.Rect).ToList(),
    };

    public void AttachEditor(CaptureRegionsEditorCanvas editor) => _editor = editor;

    [RelayCommand]
    private void ClearAll()
    {
        Regions.Clear();
        ActiveDrawKind = null;
        _editor?.CancelDrawMode();
        _editor?.RebuildVisuals();
    }

    [RelayCommand]
    private void AddCapture()
    {
        ActiveDrawKind = CaptureRegionKind.Capture;
        _editor?.BeginDraw(CaptureRegionKind.Capture);
    }

    [RelayCommand]
    private void AddIgnore()
    {
        ActiveDrawKind = CaptureRegionKind.Ignore;
        _editor?.BeginDraw(CaptureRegionKind.Ignore);
    }
}
