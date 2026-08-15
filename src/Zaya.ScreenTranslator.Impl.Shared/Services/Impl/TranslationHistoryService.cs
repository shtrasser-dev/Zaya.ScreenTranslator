using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// In-memory history of up to <see cref="ITranslationHistoryService.MaxEntries"/> unique translations.
/// When a new source text starts with an existing source, that existing entry is replaced.
/// </summary>
public sealed class TranslationHistoryService : ITranslationHistoryService
{
    private readonly object _lock = new();
    private readonly List<TranslationHistoryEntry> _entries = [];

    public event Action? Changed;

    public void AddRange(IEnumerable<(string Source, string Translation)> pairs)
    {
        var changed = false;
        lock (_lock)
        {
            foreach (var (source, translation) in pairs)
            {
                if (TryAddLocked(source, translation))
                    changed = true;
            }
        }

        if (changed)
            Changed?.Invoke();
    }

    public void Add(string source, string translation)
    {
        lock (_lock)
        {
            if (!TryAddLocked(source, translation))
                return;
        }

        Changed?.Invoke();
    }

    public IReadOnlyList<TranslationHistoryEntry> GetSnapshot()
    {
        lock (_lock)
            return _entries.ToList();
    }

    public string FormatDisplayText()
    {
        lock (_lock)
        {
            if (_entries.Count == 0)
                return string.Empty;

            return string.Join("\n\n", _entries.Select(e => $"{e.Source}\n{e.Translation}"));
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            if (_entries.Count == 0)
                return;
            _entries.Clear();
        }

        Changed?.Invoke();
    }

    private bool TryAddLocked(string source, string translation)
    {
        source = source.Trim();
        if (string.IsNullOrEmpty(source))
            return false;

        translation ??= string.Empty;

        // New source starts with an existing source → replace that entry (and any other prefixes).
        _entries.RemoveAll(e =>
            source.StartsWith(e.Source, StringComparison.Ordinal));

        _entries.Add(new TranslationHistoryEntry(source, translation));

        while (_entries.Count > ITranslationHistoryService.MaxEntries)
            _entries.RemoveAt(0);

        return true;
    }
}
