using System.Numerics;
using Zaya.OCR.Models;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Layout.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// One <see cref="OverlayItem"/> per OCR line; shared <see cref="OverlayItem.Id"/> =
/// paragraph id so Layout can translate once and wrap across line boxes.
/// </summary>
public sealed class OverlayFrameMapper : IOverlayFrameMapper
{
    public OverlayFrameView Map(
        IOCRResult ocr,
        ITextResult layout,
        TranslationBatch batch,
        int originX,
        int originY)
        => new(
            BuildOverlaySourceParagraphItems(batch, originX, originY),
            BuildOverlayDebugWords(ocr.Words, originX, originY),
            BuildOverlayDebugMatchedLines(layout.Lines, originX, originY));

    private static List<OverlayItem> BuildOverlaySourceParagraphItems(
        TranslationBatch batch,
        int originX,
        int originY)
    {
        var items = new List<OverlayItem>();
        foreach (var paragraph in batch.Paragraphs)
        {
            var lines = paragraph.Lines;
            if (lines.Count == 0)
                continue;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line.Text))
                    continue;

                items.Add(new OverlayItem
                {
                    Id = paragraph.Id,
                    Text = line.Text,
                    Bounds = OffsetBounds(line.Bounds, originX, originY),
                });
            }
        }

        return items;
    }

    private static BoundingBox OffsetBounds(BoundingBox bounds, int originX, int originY)
    {
        if (originX == 0 && originY == 0)
            return bounds;

        var delta = new Vector2(originX, originY);
        return new BoundingBox(
            bounds.P1 + delta,
            bounds.P2 + delta,
            bounds.P3 + delta,
            bounds.P4 + delta);
    }

    private static IReadOnlyList<OverlayDebugWord> BuildOverlayDebugWords(
        IReadOnlyList<IOCRWord> words,
        int originX,
        int originY)
    {
        if (words.Count == 0)
            return [];

        var list = new List<OverlayDebugWord>(words.Count);
        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word.Text) && word.Bounds.IsEmpty)
                continue;
            list.Add(new OverlayDebugWord
            {
                Text = word.Text,
                Bounds = OffsetBounds(word.Bounds, originX, originY),
            });
        }

        return list;
    }

    private static IReadOnlyList<OverlayDebugLine> BuildOverlayDebugMatchedLines(
        IReadOnlyList<ITextLine> lines,
        int originX,
        int originY)
    {
        if (lines.Count == 0)
            return [];

        var list = new List<OverlayDebugLine>();
        foreach (var line in lines)
        {
            if (!line.HasPreviousFrameMatch || line.Bounds.IsEmpty)
                continue;
            list.Add(new OverlayDebugLine
            {
                Text = line.Text,
                Bounds = OffsetBounds(line.Bounds, originX, originY),
            });
        }

        return list;
    }
}
