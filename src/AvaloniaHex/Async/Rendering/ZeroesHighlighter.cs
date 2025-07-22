using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
/// Provides an implementation of a highlighter that highlights all zero bytes in a visual line.
/// </summary>
public class ZeroesHighlighter : ByteHighlighter {
    /// <inheritdoc />
    protected override bool IsHighlighted(AsyncHexView hexView, VisualBytesLine line, BitLocation location) {
        return hexView.BinarySource!.ValidRanges.Contains(location) && line.GetByteAtAbsolute(location.ByteIndex) is byte b && b == 0;
    }
}