using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
/// Highlights ranges of bytes within a document of a hex view.
/// </summary>
public class RangesHighlighter : ByteHighlighter {
    /// <summary>
    /// Gets the bit ranges that should be highlighted in the document.
    /// </summary>
    public BitRangeUnion Ranges { get; } = new();

    /// <inheritdoc />
    protected override bool IsHighlighted(AsyncHexView hexView, VisualBytesLine line, BitLocation location) {
        return this.Ranges.Contains(location);
    }
}