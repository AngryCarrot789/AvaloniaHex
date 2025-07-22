using Avalonia.Media;
using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
/// Provides a base for a byte-level highlighter in a hex view.
/// </summary>
public abstract class ByteHighlighter : ILineTransformer {
    /// <summary>
    /// Gets or sets the brush used for rendering the foreground of the highlighted bytes.
    /// </summary>
    public IBrush? Foreground { get; set; }

    /// <summary>
    /// Gets or sets the brush used for rendering the background of the highlighted bytes.
    /// </summary>
    public IBrush? Background { get; set; }

    /// <summary>
    /// Determines whether the provided location is highlighted or not.
    /// </summary>
    /// <param name="hexView"></param>
    /// <param name="line"></param>
    /// <param name="location"></param>
    /// <returns></returns>
    protected abstract bool IsHighlighted(AsyncHexView hexView, VisualBytesLine line, BitLocation location);

    /// <inheritdoc />
    public void Transform(AsyncHexView hexView, VisualBytesLine line) {
        for (int i = 0; i < line.Segments.Count; i++)
            this.ColorizeSegment(hexView, line, ref i);
    }

    private void ColorizeSegment(AsyncHexView hexView, VisualBytesLine line, ref int index) {
        VisualBytesLineSegment originalSegment = line.Segments[index];

        VisualBytesLineSegment currentSegment = originalSegment;

        bool isInModifiedRange = false;
        for (ulong j = 0; j < originalSegment.Range.ByteLength; j++) {
            BitLocation currentLocation = new BitLocation(originalSegment.Range.Start.ByteIndex + j);

            bool shouldSplit = this.IsHighlighted(hexView, line, currentLocation) ? !isInModifiedRange : isInModifiedRange;
            if (!shouldSplit)
                continue;

            isInModifiedRange = !isInModifiedRange;

            // Split the segment.
            (VisualBytesLineSegment left, VisualBytesLineSegment right) = currentSegment.Split(currentLocation);

            if (isInModifiedRange) {
                // We entered a highlighted segment.
                right.ForegroundBrush = this.Foreground;
                right.BackgroundBrush = this.Background;
            }
            else {
                // We left a highlighted segment.
                right.ForegroundBrush = originalSegment.ForegroundBrush;
                right.BackgroundBrush = originalSegment.BackgroundBrush;
            }

            // Insert the ranges.
            if (left.Range.IsEmpty) {
                // Optimization. Just replace the left segment if it is empty.
                line.Segments[index] = right;
            }
            else {
                line.Segments[index] = left;
                line.Segments.Insert(index + 1, right);
                index++;
            }

            currentSegment = right;
        }
    }
}