namespace Zaya.ScreenTranslator.Impl.Shared.Constants;

internal static class LocalizationConstants
{
    public static class Buttons
    {
        public const string Start = "Btn_Start";
        public const string Stop = "Btn_Stop";
        public const string ShowText = "Btn_ShowText";
        public const string HideText = "Btn_HideText";
        public const string Settings = "Btn_Settings";
        public const string CaptureRegions = "Btn_CaptureRegions";
        public const string ThemeSwitchToLight = "Theme_SwitchToLight";
        public const string ThemeSwitchToDark = "Theme_SwitchToDark";
    }

    public static class CaptureRegions
    {
        public const string Title = "CaptureRegions_Title";
        public const string ClearAll = "CaptureRegions_ClearAll";
        public const string AddCapture = "CaptureRegions_AddCapture";
        public const string AddIgnore = "CaptureRegions_AddIgnore";
        public const string WaitingCapture = "CaptureRegions_WaitingCapture";
        public const string ConfiguredTooltip = "CaptureRegions_ConfiguredTooltip";
        public const string NotConfiguredTooltip = "CaptureRegions_NotConfiguredTooltip";
    }

    public static class History
    {
        public const string WindowTitle = "HistoryWindow_Title";
        public const string ClearAll = "History_ClearAll";
    }

    public static class Status
    {
        public const string Label = "Status_Label";
        public const string Idle = "Status_Idle";
        public const string Running = "Status_Running";
        public const string Stopping = "Status_Stopping";
        public const string Stopped = "Status_Stopped";
        public const string Starting = "Status_Starting";
        public const string CreatingSessions = "Status_CreatingSessions";
        public const string Error = "Status_Error";
        public const string NoActiveProfile = "Status_NoActiveProfile";
        public const string EngineNotFound = "Status_EngineNotFound";
        public const string TextLayoutNotFound = "Status_TextLayoutNotFound";
        public const string TranslatorNotFound = "Status_TranslatorNotFound";
        public const string TranslatorCacheNotFound = "Status_TranslatorCacheNotFound";
        public const string OverlayUnavailable = "Status_OverlayUnavailable";
        public const string OverlayFailed = "Status_OverlayFailed";
        public const string SelectTargetWindow = "Status_SelectTargetWindow";
        public const string WaitingForProcess = "Status_WaitingForProcess";
        public const string WaitingForCapture = "Status_WaitingForCapture";
        public const string PleaseWait = "Status_PleaseWait";
    }

    public static class Update
    {
        public const string AvailableTitle = "Update_Available_Title";
        public const string AvailableBody = "Update_Available_Body";
        public const string AvailableBodyNamed = "Update_Available_BodyNamed";
        public const string OpenPage = "Update_OpenPage";
        public const string Later = "Update_Later";
        public const string Checking = "Update_Checking";
        public const string PreparingPlugins = "Update_PreparingPlugins";
        public const string UpdatingPlugins = "Update_UpdatingPlugins";
        public const string PluginsRequiredTitle = "Update_PluginsRequired_Title";
        public const string PluginsRequiredBody = "Update_PluginsRequired_Body";
        public const string StartupErrorTitle = "Update_StartupError_Title";
        public const string Failed = "Update_Failed";
        public const string RestartRequired = "Update_RestartRequired";
        public const string UpToDate = "Update_UpToDate";
        public const string PluginsOk = "Update_PluginsOk";
    }

    public static class Plugin
    {
        public const string RequiredMissing = "Plugin_RequiredMissing";
        public const string NoNetwork = "Plugin_NoNetwork";
        public const string RemovingIncompatible = "Plugin_RemovingIncompatible";
        public const string Downloading = "Plugin_Downloading";
        public const string RequiredInstallFailed = "Plugin_RequiredInstallFailed";
        public const string OptionalSkipped = "Plugin_OptionalSkipped";
        public const string UpdateCheckFailed = "Plugin_UpdateCheckFailed";
        public const string RequiredAssetMissing = "Plugin_RequiredAssetMissing";
        public const string Updating = "Plugin_Updating";
    }

    public static class Profile
    {
        public const string CreateNew = "Profile_CreateNew";
        public const string CopyCurrent = "Profile_CopyCurrent";
        public const string Import = "Profile_Import";
        public const string Export = "Profile_Export";
        public const string ExportSection = "Profile_Export_Section";
        public const string ImportFailed = "Profile_Import_Failed";
        public const string ExportFailed = "Profile_Export_Failed";
        public const string DeleteLast = "Profile_Delete_Last";
        public const string DeleteTitle = "Profile_Delete_Title";
        public const string DeleteConfirm = "Profile_Delete_Confirm";
        public const string Delete = "Profile_Delete";
        public const string CreateLimit = "Profile_CreateLimit";
        public const string NewName = "Profile_NewName";
    }

    public static class SaveAsNew
    {
        public const string ErrorEmpty = "SaveAsNew_Error_Empty";
        public const string ErrorExists = "SaveAsNew_Error_Exists";
        public const string Cancel = "SaveAsNew_Cancel";
    }

    public static class Dialog
    {
        public const string Ok = "Dialog_OK";
        public const string Exit = "Dialog_Exit";
    }

    public static class DisplayMode
    {
        public const string TextWindow = "DisplayMode_TextWindow";
        public const string Overlay = "DisplayMode_Overlay";
    }

    public static class Table
    {
        public const string AddRule = "Filter_AddRule";
        public const string RemoveRule = "Filter_RemoveRule";
    }

    public static class Settings
    {
        public const string TargetProcess = "Settings_TargetProcess";
        public const string TargetProcessDesc = "Settings_TargetProcess_Desc";
        public const string FramePauseMs = "Settings_FramePauseMs";
        public const string FramePauseMsDesc = "Settings_FramePauseMs_Desc";
        public const string TranslatorNone = "Settings_TranslatorNone";
        public const string TranslatorCacheNone = "Settings_TranslatorCacheNone";
        public const string TranslatorCacheEngine = "Settings_TranslatorCacheEngine";
    }

    public static class Validation
    {
        public const string IntegerRange = "Validation_IntegerRange";
        public const string IntegerMinOnly = "Validation_IntegerMinOnly";
    }

    public static class Timing
    {
        public const string Format = "Timing_Format";
    }

    public static class Text
    {
        public const string AvgConfidence = "Text_AvgConfidence";
    }

    public static class Overlay
    {
        public const string NeedStart = "Overlay_NeedStart";
    }

    public static class Main
    {
        public const string WindowTitle = "MainWindow_Title";
    }
}
