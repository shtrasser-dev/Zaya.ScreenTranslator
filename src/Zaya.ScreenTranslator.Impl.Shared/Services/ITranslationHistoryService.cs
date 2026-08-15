namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// In-memory history of recent source/translation pairs for the text window.
/// </summary>
public interface ITranslationHistoryService
{
    const int MaxEntries = 500;

    event Action? Changed;

    void AddRange(IEnumerable<(string Source, string Translation)> pairs);
    void Add(string source, string translation);
    IReadOnlyList<TranslationHistoryEntry> GetSnapshot();
    string FormatDisplayText();
    void Clear();
}

public sealed record TranslationHistoryEntry(string Source, string Translation);
