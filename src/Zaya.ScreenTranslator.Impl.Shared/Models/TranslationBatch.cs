using Zaya.OCR.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>
/// Paragraphs with joined source text for the translator (same order, same length).
/// </summary>
public sealed class TranslationBatch
{
    public TranslationBatch(
        IReadOnlyList<ITextParagraph> paragraphs,
        IReadOnlyList<string> texts)
    {
        Paragraphs = paragraphs;
        Texts = texts;
    }

    public IReadOnlyList<ITextParagraph> Paragraphs { get; }
    public IReadOnlyList<string> Texts { get; }

    public static TranslationBatch Empty { get; } = new([], []);
}
