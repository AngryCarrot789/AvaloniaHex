using Avalonia;
using Avalonia.Media.TextFormatting;
using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
/// Represents a column that renders binary data using the ASCII text encoding.
/// </summary>
public class AsciiColumn : CellBasedColumn {
    /// <inheritdoc />
    public override Size MinimumSize => default;

    /// <inheritdoc />
    public override int BitsPerCell => 8;

    /// <inheritdoc />
    public override int CellsPerWord => 1;

    /// <inheritdoc />
    public override double GroupPadding => 0;

    static AsciiColumn() {
        CursorProperty.OverrideDefaultValue<AsciiColumn>(IBeamCursor);
        IsHeaderVisibleProperty.OverrideDefaultValue<AsciiColumn>(false);
        HeaderProperty.OverrideDefaultValue<AsciiColumn>("ASCII");
    }

    /// <inheritdoc />
    protected override bool TryWriteCell(Span<byte> buffer, BitLocation bufferStart, BitLocation writeLocation, char input) {
        buffer[(int) (writeLocation.ByteIndex - bufferStart.ByteIndex)] = (byte) input;
        return true;
    }

    /// <inheritdoc />
    public override string? GetText(BitRange range) {
        if (this.HexView?.BinarySource is null)
            return null;

        byte[] data = new byte[range.ByteLength];
        this.HexView.BinarySource.ReadAvailableData(range.Start.ByteIndex, data);

        char[] output = new char[data.Length];
        byte?[] nullableData = new byte?[data.Length];
        for (int i = 0; i < data.Length; i++)
            nullableData[i] = data[i];
        this.GetText(nullableData, range, output);

        return new string(output);
    }

    /// <inheritdoc />
    public override TextLine? CreateTextLine(VisualBytesLine line) {
        if (this.HexView is null)
            return null;

        GenericTextRunProperties properties = this.GetTextRunProperties();
        return TextFormatter.Current.FormatLine(
            new AsciiTextSource(this, line, properties),
            0,
            double.MaxValue,
            new GenericTextParagraphProperties(properties)
        );
    }

    private static char MapToPrintableChar(byte b) {
        return b switch {
            >= 0x20 and < 0x7f => (char) b,
            _ => '.'
        };
    }

    private void GetText(ReadOnlySpan<byte?> data, BitRange dataRange, Span<char> buffer) {
        char invalidCellChar = this.InvalidCellChar;

        if (this.HexView?.BinarySource?.ValidRanges is not { } valid) {
            buffer.Fill(invalidCellChar);
            return;
        }

        for (int i = 0; i < data.Length; i++) {
            BitLocation cellLocation = new BitLocation(dataRange.Start.ByteIndex + (ulong) i, 0);
            BitRange cellRange = new BitRange(cellLocation, cellLocation.AddBits(8));

            byte? b = data[i];
            buffer[i] = b.HasValue && valid.IsSuperSetOf(cellRange)
                ? MapToPrintableChar(b.Value)
                : invalidCellChar;
        }
    }

    private sealed class AsciiTextSource : ITextSource {
        private readonly AsciiColumn _column;
        private readonly GenericTextRunProperties _properties;
        private readonly VisualBytesLine _line;

        public AsciiTextSource(AsciiColumn column, VisualBytesLine line, GenericTextRunProperties properties) {
            this._column = column;
            this._line = line;
            this._properties = properties;
        }

        /// <inheritdoc />
        public TextRun? GetTextRun(int textSourceIndex) {
            // Find current segment we're in.
            BitLocation currentLocation = new BitLocation(this._line.Range.Start.ByteIndex + (ulong) textSourceIndex);
            VisualBytesLineSegment? segment = this._line.FindSegmentContaining(currentLocation);
            if (segment is null)
                return null;

            // Stringify the segment.
            BitRange range = segment.Range;
            ReadOnlySpan<byte?> data = this._line.AsAbsoluteSpan(range);
            Span<char> buffer = stackalloc char[(int) range.ByteLength];
            this._column.GetText(data, range, buffer);

            // Render
            return new TextCharacters(
                new string(buffer), this._properties.WithBrushes(
                    segment.ForegroundBrush ?? this._properties.ForegroundBrush,
                    segment.BackgroundBrush ?? this._properties.BackgroundBrush
                )
            );
        }
    }
}