using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media.TextFormatting;
using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
/// Represents a single visual line in a hex view.
/// </summary>
[DebuggerDisplay("{Range}")]
public sealed class VisualBytesLine {
    /// <summary>
    /// Gets the parent ehx view the line is visible in.
    /// </summary>
    public AsyncHexView HexView { get; }

    /// <summary>
    /// Gets the bit range the visual line spans. If this line is the last visible line in the document, this may include
    /// the "virtual" cell to insert into.
    /// </summary>
    public BitRange VirtualRange { get; private set; }

    /// <summary>
    /// Gets the bit range the visual line spans.
    /// </summary>
    public BitRange Range { get; private set; }

    /// <summary>
    /// Gets the data that is displayed in the line.
    /// </summary>
    public byte?[] Buffer { get; }

    /// <summary>
    /// Gets the bounding box in the hex view the line is rendered at.
    /// </summary>
    public Rect Bounds { get; internal set; }

    /// <summary>
    /// Gets the individual segments the line comprises.
    /// </summary>
    public List<VisualBytesLineSegment> Segments { get; }

    /// <summary>
    /// Gets the individual text lines for every column.
    /// </summary>
    public TextLine?[] ColumnTextLines { get; }

    /// <summary>
    /// Gets a value indicating whether the data and line segments present in the visual line are up to date.
    /// </summary>
    public bool IsValid { get; private set; }

    internal VisualBytesLine(AsyncHexView hexView) {
        this.HexView = hexView;

        this.Buffer = new byte?[hexView.ActualBytesPerLine];
        this.ColumnTextLines = new TextLine?[hexView.Columns.Count];
        this.Segments = new List<VisualBytesLineSegment>();
    }

    /// <summary>
    /// Gets the byte in the visual line at the provided absolute byte offset.
    /// </summary>
    /// <param name="byteIndex">The byte offset.</param>
    /// <returns>The byte.</returns>
    public byte? GetByteAtAbsolute(ulong byteIndex) {
        return this.Buffer[byteIndex - this.Range.Start.ByteIndex];
    }

    /// <summary>
    /// Obtains the span that includes the provided range.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <returns>The span.</returns>
    public Span<byte?> AsAbsoluteSpan(BitRange range) {
        if (!this.Range.Contains(range))
            throw new ArgumentException("Provided range is not within the current line");

        return this.Buffer.AsSpan(
            (int) (range.Start.ByteIndex - this.Range.Start.ByteIndex),
            (int) range.ByteLength
        );
    }

    /// <summary>
    /// Finds the segment that contains the provided location.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns>The segment, or <c>null</c> if no segment contains the provided location.</returns>
    public VisualBytesLineSegment? FindSegmentContaining(BitLocation location) {
        foreach (VisualBytesLineSegment segment in this.Segments) {
            if (segment.Range.Contains(location))
                return segment;
        }

        return null;
    }

    internal bool SetRange(BitRange virtualRange) {
        bool hasChanged = false;

        if (this.VirtualRange != virtualRange) {
            this.VirtualRange = virtualRange;
            hasChanged = true;
        }

        BitRange range = this.HexView.BinarySource is { ApplicableRange: var enclosingRange }
            ? virtualRange.Clamp(enclosingRange)
            : BitRange.Empty;

        if (this.Range != range) {
            this.Range = range;
            hasChanged = true;
        }

        return hasChanged;
    }

    /// <summary>
    /// Ensures the visual line is populated with the latest binary data and line segments.
    /// </summary>
    public void EnsureIsValid() {
        if (!this.IsValid)
            this.Refresh();
    }

    /// <summary>
    /// Marks the visual line, its binary data and line segments as out of date.
    /// </summary>
    public void Invalidate() => this.IsValid = false;

    /// <summary>
    /// Updates the visual line with the latest data of the document and reconstructs all line segments.
    /// </summary>
    public void Refresh() {
        if (this.HexView.BinarySource is null)
            return;

        this.ReadData();
        this.CreateLineSegments();
        this.CreateColumnTextLines();

        this.IsValid = true;
    }

    private void ReadData() {
        IBinarySource document = this.HexView.BinarySource!;
        Span<byte> readBuffer = stackalloc byte[(int) this.Range.ByteLength];
        Span<byte?> dstDataSpan = this.Buffer.AsSpan(0, readBuffer.Length);

        // Fast path, just read entire range if possible.
        BitRangeUnion union = new BitRangeUnion();
        document.ReadAvailableBytesOrRequest(this.Range.Start.ByteIndex, readBuffer, union);
        dstDataSpan.Clear();
        foreach (BitRange range in union) {
            int length = (int) range.ByteLength;
            // int offset = (int) (this.Range.Start.ByteIndex - range.Start.ByteIndex);
            for (int i = 0, offset = (int) range.Start.ByteIndex; i < length; i++) {
                dstDataSpan[i + offset] = readBuffer[i];
            }
        }

        // // Only read valid segments in the line.
        // Span<BitRange> ranges = stackalloc BitRange[readBuffer.Length];
        // int count = document.AvailableDataRanges.GetIntersectingRanges(this.Range, ranges);
        // for (int i = 0; i < count; i++) {
        //     BitRange range = ranges[i];
        //     int relativeOffset = (int) (range.Start.ByteIndex - this.Range.Start.ByteIndex);
        //     Span<byte?> dstDataChunk = dstDataSpan[relativeOffset..(relativeOffset + (int) range.ByteLength)];
        //     int read = document.ReadAvailableBytesOrRequest(range.Start.ByteIndex, readBuffer.Slice(0, dstDataChunk.Length));
        //     for (int j = 0; j < read; j++) {
        //         dstDataSpan[j] = readBuffer[j];
        //     }
        //     if (read < dstDataChunk.Length) {
        //         dstDataChunk.Slice(read).Clear();
        //     }
        // }
    }

    private void CreateLineSegments() {
        this.Segments.Clear();
        this.Segments.Add(new VisualBytesLineSegment(this.Range));

        ObservableCollection<ILineTransformer> transformers = this.HexView.LineTransformers;
        for (int i = 0; i < transformers.Count; i++)
            transformers[i].Transform(this.HexView, this);
    }

    private void CreateColumnTextLines() {
        for (int i = 0; i < this.HexView.Columns.Count; i++) {
            Column column = this.HexView.Columns[i];
            if (column.IsVisible) {
                this.ColumnTextLines[i]?.Dispose();
                this.ColumnTextLines[i] = column.CreateTextLine(this);
            }
        }
    }

    /// <summary>
    /// Computes the required height required to the visual line occupies.
    /// </summary>
    /// <returns>The height.</returns>
    public double GetRequiredHeight() {
        double height = 0;
        foreach (TextLine? columns in this.ColumnTextLines)
            height = Math.Max(height, columns?.Height ?? 0);
        return height;
    }
}