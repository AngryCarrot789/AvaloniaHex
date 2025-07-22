using Avalonia;
using Avalonia.Media.TextFormatting;
using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
/// Represents a column that renders binary data using the binary number encoding.
/// </summary>
public class BinaryColumn : CellBasedColumn {
    /// <summary>
    /// Dependency property for <see cref="UseDynamicHeader"/>
    /// </summary>
    public static readonly StyledProperty<bool> UseDynamicHeaderProperty =
        AvaloniaProperty.Register<HexColumn, bool>(nameof(UseDynamicHeader), true);

    /// <summary>
    /// Gets or sets a value indicating whether the header of this column should be dynamically
    /// </summary>
    public bool UseDynamicHeader {
        get => this.GetValue(IsHeaderVisibleProperty);
        set => this.SetValue(IsHeaderVisibleProperty, value);
    }

    /// <inheritdoc />
    public override Size MinimumSize => default;

    /// <inheritdoc />
    public override double GroupPadding => this.CellSize.Width;

    /// <inheritdoc />
    public override int BitsPerCell => 1;

    /// <inheritdoc />
    public override int CellsPerWord => 8;

    static BinaryColumn() {
        CursorProperty.OverrideDefaultValue<BinaryColumn>(IBeamCursor);
        UseDynamicHeaderProperty.Changed.AddClassHandler<BinaryColumn, bool>(OnUseDynamicHeaderChanged);
        HeaderProperty.OverrideDefaultValue<BinaryColumn>("Binary");
    }

    private static byte? ParseBit(char c) => c switch {
        '0' => 0,
        '1' => 1,
        _ => null
    };

    /// <inheritdoc />
    protected override string PrepareTextInput(string input) => input.Replace(" ", "");

    /// <inheritdoc />
    protected override bool TryWriteCell(Span<byte> buffer, BitLocation bufferStart, BitLocation writeLocation, char input) {
        if (ParseBit(input) is not { } bit)
            return false;

        int relativeByteIndex = (int) (writeLocation.ByteIndex - bufferStart.ByteIndex);
        buffer[relativeByteIndex] = (byte) (
            buffer[relativeByteIndex] & ~(1 << writeLocation.BitIndex) | (bit << writeLocation.BitIndex)
        );

        return true;
    }

    /// <inheritdoc />
    public override string? GetText(BitRange range) {
        if (this.HexView?.BinarySource is null)
            return null;

        byte[] data = new byte[range.ByteLength];
        this.HexView.BinarySource.ReadAvailableData(range.Start.ByteIndex, data);

        char[] output = new char[data.Length * 3 - 1];
        byte?[] nullableData = new byte?[data.Length];
        for (int i = 0; i < data.Length; i++)
            nullableData[i] = data[i];
        this.GetText(nullableData, range, output);

        return new string(output);
    }

    /// <inheritdoc />
    public override TextLine? CreateHeaderLine() {
        if (!this.UseDynamicHeader)
            return base.CreateHeaderLine();

        if (this.HexView is null)
            return null;

        // Generate header text.
        int count = this.HexView.ActualBytesPerLine;
        char[] buffer = new char[count * 9 - 1];

        for (int i = 0; i < count; i++) {
            for (int j = 0; j < 8; j++)
                buffer[i * 9 + j] = (char) (((i >> (7 - j)) & 1) + '0');

            if (i < count - 1)
                buffer[i * 9 + 8] = ' ';
        }

        // Render.
        GenericTextRunProperties properties = this.GetHeaderTextRunProperties();
        return TextFormatter.Current.FormatLine(
            new SimpleTextSource(new string(buffer), properties),
            0,
            double.MaxValue,
            new GenericTextParagraphProperties(properties)
        );
    }

    /// <inheritdoc />
    public override TextLine? CreateTextLine(VisualBytesLine line) {
        if (this.HexView is null)
            return null;

        GenericTextRunProperties properties = this.GetTextRunProperties();
        return TextFormatter.Current.FormatLine(
            new BinaryTextSource(this, line, properties),
            0,
            double.MaxValue,
            new GenericTextParagraphProperties(properties)
        );
    }

    private void GetText(ReadOnlySpan<byte?> data, BitRange dataRange, Span<char> buffer) {
        char invalidCellChar = this.InvalidCellChar;

        if (this.HexView?.BinarySource?.ValidRanges is not { } valid) {
            buffer.Fill(invalidCellChar);
            return;
        }

        int index = 0;
        for (int i = 0; i < data.Length; i++) {
            if (i > 0)
                buffer[index++] = ' ';

            byte? value = data[i];

            for (int j = 0; j < 8; j++) {
                BitLocation location = new BitLocation(dataRange.Start.ByteIndex + (ulong) i, 7 - j);
                buffer[index + j] = value.HasValue && valid.Contains(location)
                    ? (char) (((value.Value >> location.BitIndex) & 1) + '0')
                    : invalidCellChar;
            }

            index += 8;
        }
    }

    private static void OnUseDynamicHeaderChanged(BinaryColumn arg1, AvaloniaPropertyChangedEventArgs<bool> arg2) {
        arg1.HexView?.InvalidateHeaders();
    }

    private sealed class BinaryTextSource : ITextSource {
        private readonly BinaryColumn _column;
        private readonly GenericTextRunProperties _properties;
        private readonly VisualBytesLine _line;

        public BinaryTextSource(BinaryColumn column, VisualBytesLine line, GenericTextRunProperties properties) {
            this._column = column;
            this._line = line;
            this._properties = properties;
        }

        /// <inheritdoc />
        public TextRun? GetTextRun(int textSourceIndex) {
            // Calculate current byte location from text index.
            int byteIndex = Math.DivRem(textSourceIndex, 9, out int bitIndex);
            if (byteIndex < 0 || byteIndex >= this._line.Buffer.Length)
                return null;

            // Special case nibble index 8 (space after byte).
            if (bitIndex == 8) {
                if (byteIndex >= this._line.Buffer.Length - 1)
                    return null;

                return new TextCharacters(" ", this._properties);
            }

            // Find current segment we're in.
            BitLocation currentLocation = new BitLocation(this._line.Range.Start.ByteIndex + (ulong) byteIndex, bitIndex);
            VisualBytesLineSegment? segment = this._line.FindSegmentContaining(currentLocation);
            if (segment is null)
                return null;

            // Stringify the segment.
            BitRange range = segment.Range;
            ReadOnlySpan<byte?> data = this._line.AsAbsoluteSpan(range);
            Span<char> buffer = stackalloc char[(int) segment.Range.ByteLength * 9 - 1];
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