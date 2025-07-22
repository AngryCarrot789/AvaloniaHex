using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
/// Provides an implementation of a highlighter that highlights all invalid ranges in a document.
/// </summary>
public class InvalidRangesHighlighter : ByteHighlighter {
    /// <inheritdoc />
    protected override bool IsHighlighted(AsyncHexView hexView, VisualBytesLine line, BitLocation location) {
        return !hexView.BinarySource?.ApplicableRange.Contains(location) ?? false;
    }
}