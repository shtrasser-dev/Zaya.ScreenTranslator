using Zaya.OCR.Models;
using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Joins layout paragraphs into translator source strings.
/// </summary>
public interface ITranslationBatchBuilder
{
    TranslationBatch Build(IReadOnlyList<ITextParagraph> paragraphs);
}
