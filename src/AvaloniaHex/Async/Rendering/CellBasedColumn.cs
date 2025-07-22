using Avalonia;
using Avalonia.Media.TextFormatting;
using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
/// Represents a column that contains individual selectable cells.
/// </summary>
public abstract class CellBasedColumn : Column {
    /// <summary>
    /// Defines the <see cref="InvalidCellChar"/> property.
    /// </summary>
    public static readonly StyledProperty<char> InvalidCellCharProperty =
        AvaloniaProperty.Register<CellBasedColumn, char>(nameof(InvalidCellChar), '?');

    /// <summary>
    /// Gets the size of an individual selectable cell.
    /// </summary>
    public Size CellSize { get; private set; }

    /// <summary>
    /// Gets the amount of padding in between groups of cells.
    /// </summary>
    public abstract double GroupPadding { get; }

    /// <summary>
    /// Gets the number of bits represented by each cell.
    /// </summary>
    public abstract int BitsPerCell { get; }

    /// <summary>
    /// Gets the number of cells that are within a single word.
    /// </summary>
    public abstract int CellsPerWord { get; }

    /// <summary>
    /// Gets the bit index of the first, left-most, selectable cell.
    /// </summary>
    public int FirstBitIndex => 8 - this.BitsPerCell;

    /// <summary>
    /// Gets the total amount of cells in a single line.
    /// </summary>
    public int CellCount => (this.HexView?.ActualBytesPerLine * 8 ?? 0) / this.BitsPerCell;

    /// <summary>
    /// Gets the total width of a single word of cells.
    /// </summary>
    public double WordWidth => this.CellSize.Width * this.CellsPerWord;

    /// <summary>
    /// Gets the total amount of words in a single line.
    /// </summary>
    public int WordCount => this.CellCount / this.CellsPerWord;

    /// <summary>
    /// Gets the total width of the column.
    /// </summary>
    public override double Width => base.Width + this.WordCount * this.WordWidth + (this.WordCount - 1) * this.GroupPadding;

    /// <summary>
    /// Gets or sets the character that is used for displaying invalid or inaccessible cells.
    /// </summary>
    public char InvalidCellChar {
        get => this.GetValue(InvalidCellCharProperty);
        set => this.SetValue(InvalidCellCharProperty, value);
    }

    /// <summary>
    /// Preprocesses the provided text input for insertion into the column.
    /// </summary>
    /// <param name="input">The input text to process.</param>
    /// <returns>The processed input.</returns>
    protected virtual string PrepareTextInput(string input) => input;

    /// <summary>
    /// Interprets a single character and writes it into the provided buffer.
    /// </summary>
    /// <param name="buffer">The buffer to modify.</param>
    /// <param name="bufferStart">The start address of the buffer.</param>
    /// <param name="writeLocation">The address to write to.</param>
    /// <param name="input">The textual input to write.</param>
    /// <returns><c>true</c> if the input was interpreted and written to the buffer, <c>false</c> otherwise.</returns>
    protected abstract bool TryWriteCell(Span<byte> buffer, BitLocation bufferStart, BitLocation writeLocation, char input);

    /// <summary>
    /// Processes textual input in the column.
    /// </summary>
    /// <param name="location">The location to insert at.</param>
    /// <param name="input">The textual input to process.</param>
    /// <returns><c>true</c> if the document was changed, <c>false</c> otherwise.</returns>
    public bool HandleTextInput(ref BitLocation location, string input) {
        IBinarySource? document = this.HexView?.BinarySource;
        if (document is null || !document.CanWriteBackInto) {
            return false;
        }

        // Pre-process text (e.g., remove spaces etc.)
        input = this.PrepareTextInput(input);

        // We have special behavior if we are not at the beginning of a byte.
        bool isAtFirstCell = location.BitIndex == this.FirstBitIndex;

        // Compute affected bytes.
        uint byteCount = (uint) ((input.Length - 1) / this.CellsPerWord + 1);
        BitLocation alignedStart = new BitLocation(location.ByteIndex, 0);
        BitLocation alignedEnd = new BitLocation(alignedStart.ByteIndex + byteCount, 0);
        if (!isAtFirstCell && input.Length > 1)
            alignedEnd = alignedEnd.AddBytes(1);

        BitRange affectedRange = new BitRange(alignedStart, alignedEnd);

        // Determine the number of bytes to read from the original document.
        if (!document.ApplicableRange.Contains(affectedRange))
            return false;

        // We need to read the original bytes if we are overwriting, as cells do not necessarily encompass entire bytes.
        int originalDataReadCount = (int) affectedRange.ByteLength;

        // Allocate temporary buffer to write the data into.
        byte[] data = new byte[affectedRange.ByteLength];

        if (originalDataReadCount > 0)
            document.ReadAvailableData(location.ByteIndex, data.AsSpan(0, originalDataReadCount), null);

        // Write all the cells in the temporary buffer.
        BitLocation newLocation = location;
        for (int i = 0; i < input.Length; i++) {
            // Are we overwriting in a valid cell in the document?
            if (!document.ApplicableRange.Contains(new BitRange(newLocation, newLocation.AddBits((ulong) this.BitsPerCell)))) {
                return false;
            }

            // Try handling the textual input according to the column's string format.
            if (!this.TryWriteCell(data, location, newLocation, input[i]))
                return false;

            newLocation = this.GetNextLocation(newLocation, false, false);
        }

        // Apply changes to document.
        document.OnUserInput(location.ByteIndex, data);

        // Move to final location.
        location = newLocation;
        return true;
    }

    /// <summary>
    /// Gets the textual representation of the provided bit range.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <returns>The text.</returns>
    public abstract string? GetText(BitRange range);

    /// <inheritdoc />
    public override void Measure() {
        if (this.HexView is null) {
            this.CellSize = default;
        }
        else {
            GenericTextRunProperties properties = this.GetTextRunProperties();
            TextLine dummyTemplate = TextFormatter.Current.FormatLine(
                new SimpleTextSource(".", properties),
                0,
                double.MaxValue,
                new GenericTextParagraphProperties(properties)
            )!;

            this.CellSize = new Size(dummyTemplate.Width, dummyTemplate.Height);
        }
    }

    /// <summary>
    /// Gets the bounding box of the cell group containing the cell of the provided location.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="location">The location.</param>
    /// <returns>The bounding box.</returns>
    public Rect GetGroupBounds(VisualBytesLine line, BitLocation location) {
        Rect rect = this.GetRelativeGroupBounds(line, location);
        return new Rect(new Point(this.Bounds.Left, line.Bounds.Top) + rect.TopLeft, rect.Size);
    }

    /// <summary>
    /// Gets the bounding box of the cell containing the provided location.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="location">The location.</param>
    /// <returns>The bounding box.</returns>
    public Rect GetCellBounds(VisualBytesLine line, BitLocation location) {
        Rect rect = this.GetRelativeCellBounds(line, location);
        return new Rect(new Point(this.Bounds.Left, line.Bounds.Top) + rect.TopLeft, rect.Size);
    }

    /// <summary>
    /// Gets the bounding box of the cell group containing the cell of the provided location, relative to the current
    /// line.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="location">The location.</param>
    /// <returns>The bounding box.</returns>
    public Rect GetRelativeCellBounds(VisualBytesLine line, BitLocation location) {
        ulong relativeByteIndex = location.ByteIndex - line.VirtualRange.Start.ByteIndex;
        int nibbleIndex = (this.CellsPerWord - 1) - location.BitIndex / this.BitsPerCell;

        return new Rect(
            new Point((this.WordWidth + this.GroupPadding) * relativeByteIndex + this.CellSize.Width * nibbleIndex, 0), this.CellSize
        );
    }

    /// <summary>
    /// Gets the bounding box of the cell containing the provided location, relative to the current line.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="location">The location.</param>
    /// <returns>The bounding box.</returns>
    public Rect GetRelativeGroupBounds(VisualBytesLine line, BitLocation location) {
        ulong relativeByteIndex = location.ByteIndex - line.VirtualRange.Start.ByteIndex;

        return new Rect(
            new Point((this.WordWidth + this.GroupPadding) * relativeByteIndex, 0),
            new Size(this.CellSize.Width * this.CellsPerWord, this.CellSize.Height)
        );
    }

    /// <summary>
    /// Aligns the provided location to the beginning of the cell that contains the location.
    /// </summary>
    /// <param name="location">The location to align.</param>
    /// <returns>The aligned location.</returns>
    public BitLocation AlignToCell(BitLocation location) {
        return new BitLocation(location.ByteIndex, location.BitIndex / this.BitsPerCell * this.BitsPerCell);
    }

    /// <summary>
    /// Gets the location of the first selectable cell.
    /// </summary>
    /// <returns>The location.</returns>
    public BitLocation GetFirstLocation() {
        return this.HexView is { BinarySource.ApplicableRange: var enclosingRange }
            ? new BitLocation(enclosingRange.Start.ByteIndex, this.FirstBitIndex)
            : default;
    }

    /// <summary>
    /// Gets the location of the last selectable cell.
    /// </summary>
    /// <returns>The location.</returns>
    public BitLocation GetLastLocation(bool includeVirtualCell) {
        return this.HexView is { BinarySource.ApplicableRange: var enclosingRange }
            ? new BitLocation(enclosingRange.End.ByteIndex - (!includeVirtualCell ? 1u : 0u), 0)
            : default;
    }

    /// <summary>
    /// Given a bit location, gets the location of the cell before it.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns>The previous cell's location.</returns>
    public BitLocation GetPreviousLocation(BitLocation location) {
        if (location.BitIndex < 8 - this.BitsPerCell)
            return this.AlignToCell(new BitLocation(location.ByteIndex, location.BitIndex + this.BitsPerCell));

        if (this.HexView is not { BinarySource.ApplicableRange: var enclosingRange }
            || location.ByteIndex == enclosingRange.Start.ByteIndex) {
            return this.GetFirstLocation();
        }

        return new BitLocation(location.ByteIndex - 1, 0);
    }

    /// <summary>
    /// Given a bit location, gets the location of the cell after it.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <param name="includeVirtualCell"><c>true</c> if the virtual cell at the end of the document should be included.</param>
    /// <param name="clamp"><c>true</c> if the location should be restricted to the current document length.</param>
    /// <returns>The next cell's location.</returns>
    public BitLocation GetNextLocation(BitLocation location, bool includeVirtualCell, bool clamp) {
        if (this.HexView is not { BinarySource.ApplicableRange: var enclosingRange })
            return default;

        if (location.BitIndex != 0)
            return this.AlignToCell(new BitLocation(location.ByteIndex, location.BitIndex - this.BitsPerCell));

        if (clamp && location.ByteIndex >= enclosingRange.End.ByteIndex - (!includeVirtualCell ? 1u : 0u))
            return this.GetLastLocation(includeVirtualCell);

        return new BitLocation(location.ByteIndex + 1, 8 - this.BitsPerCell);
    }

    /// <summary>
    /// Gets the bit location of the cell under the provided point.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="point">The point.</param>
    /// <returns>The cell's location.</returns>
    public BitLocation? GetLocationByPoint(VisualBytesLine line, Point point) {
        Point relativePoint = point - this.Bounds.TopLeft;
        double totalGroupWidth = this.WordWidth + this.GroupPadding;

        ulong byteIndex = (ulong) (relativePoint.X / totalGroupWidth);
        int nibbleIndex = Math.Clamp(
            (int) (this.CellsPerWord * (relativePoint.X - byteIndex * totalGroupWidth) / this.WordWidth),
            0, this.CellsPerWord - 1
        );

        BitLocation location = new BitLocation(
            line.VirtualRange.Start.ByteIndex + byteIndex,
            (this.CellsPerWord - 1 - nibbleIndex) * this.BitsPerCell
        );

        return location.Clamp(line.VirtualRange);
    }
}