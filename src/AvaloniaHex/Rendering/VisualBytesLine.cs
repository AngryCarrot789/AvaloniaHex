using System.Diagnostics;
using Avalonia;
using Avalonia.Media.TextFormatting;
using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Rendering;

/// <summary>
/// Represents a single visual line in a hex view.
/// </summary>
[DebuggerDisplay("{Range}")]
public sealed class VisualBytesLine {
    /// <summary>
    /// Gets the parent ehx view the line is visible in.
    /// </summary>
    public HexView HexView { get; }

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
    public byte[] Data { get; }

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

    internal VisualBytesLine(HexView hexView) {
        this.HexView = hexView;

        this.Data = new byte[hexView.ActualBytesPerLine];
        this.ColumnTextLines = new TextLine?[hexView.Columns.Count];
        this.Segments = new List<VisualBytesLineSegment>();
    }

    /// <summary>
    /// Gets the byte in the visual line at the provided absolute byte offset.
    /// </summary>
    /// <param name="byteIndex">The byte offset.</param>
    /// <returns>The byte.</returns>
    public byte GetByteAtAbsolute(ulong byteIndex) {
        return this.Data[byteIndex - this.Range.Start.ByteIndex];
    }

    /// <summary>
    /// Obtains the span that includes the provided range.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <returns>The span.</returns>
    public Span<byte> AsAbsoluteSpan(BitRange range) {
        if (!this.Range.Contains(range))
            throw new ArgumentException("Provided range is not within the current line");

        return this.Data.AsSpan(
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
        foreach (var segment in this.Segments) {
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

        var range = this.HexView.Document is { ValidRanges.EnclosingRange: var enclosingRange }
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
        if (this.HexView.Document is null)
            return;

        this.ReadData();
        this.CreateLineSegments();
        this.CreateColumnTextLines();

        this.IsValid = true;
    }

    private void ReadData() {
        var document = this.HexView.Document!;
        var dataSpan = this.Data.AsSpan(0, (int) this.Range.ByteLength);

        // Fast path, just read entire range if possible.
        if (!document.ValidRanges.IsFragmented) {
            document.ReadBytes(this.Range.Start.ByteIndex, dataSpan);
            return;
        }

        // Only read valid segments in the line.
        Span<BitRange> ranges = stackalloc BitRange[dataSpan.Length];
        int count = document.ValidRanges.GetIntersectingRanges(this.Range, ranges);
        for (int i = 0; i < count; i++) {
            var range = ranges[i];
            int relativeOffset = (int) (range.Start.ByteIndex - this.Range.Start.ByteIndex);

            var chunk = dataSpan[relativeOffset..(relativeOffset + (int) range.ByteLength)];
            document.ReadBytes(range.Start.ByteIndex, chunk);
        }
    }

    private void CreateLineSegments() {
        this.Segments.Clear();
        this.Segments.Add(new VisualBytesLineSegment(this.Range));

        var transformers = this.HexView.LineTransformers;
        for (int i = 0; i < transformers.Count; i++)
            transformers[i].Transform(this.HexView, this);
    }

    private void CreateColumnTextLines() {
        for (int i = 0; i < this.HexView.Columns.Count; i++) {
            var column = this.HexView.Columns[i];
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
        foreach (var columns in this.ColumnTextLines)
            height = Math.Max(height, columns?.Height ?? 0);
        return height;
    }
}