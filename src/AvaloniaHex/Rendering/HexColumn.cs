using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Media.TextFormatting;
using AvaloniaHex.Document;

namespace AvaloniaHex.Rendering;

/// <summary>
/// Represents a column that renders binary data using hexadecimal number encoding.
/// </summary>
public class HexColumn : CellBasedColumn {
    static HexColumn() {
        IsUppercaseProperty.Changed.AddClassHandler<HexColumn, bool>(OnIsUpperCaseChanged);
        CursorProperty.OverrideDefaultValue<HexColumn>(IBeamCursor);
    }

    /// <inheritdoc />
    public override Size MinimumSize => default;

    /// <inheritdoc />
    public override double GroupPadding => this.CellSize.Width;

    /// <inheritdoc />
    public override int BitsPerCell => 4;

    /// <inheritdoc />
    public override int CellsPerWord => 2;

    /// <summary>
    /// Defines the <see cref="IsUppercase"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsUppercaseProperty =
        AvaloniaProperty.Register<HexColumn, bool>(nameof(IsUppercase), true);

    /// <summary>
    /// Gets or sets a value indicating whether the hexadecimal digits should be rendered in uppercase or not.
    /// </summary>
    public bool IsUppercase {
        get => this.GetValue(IsUppercaseProperty);
        set => this.SetValue(IsUppercaseProperty, value);
    }

    private static void OnIsUpperCaseChanged(HexColumn arg1, AvaloniaPropertyChangedEventArgs<bool> arg2) {
        arg1.HexView?.InvalidateVisualLines();
    }

    /// <inheritdoc />
    protected override string PrepareTextInput(string input) => input.Replace(" ", "");

    private static byte? ParseNibble(char c) => c switch {
        >= '0' and <= '9' => (byte?) (c - '0'),
        >= 'a' and <= 'f' => (byte?) (c - 'a' + 10),
        >= 'A' and <= 'F' => (byte?) (c - 'A' + 10),
        _ => null
    };

    /// <inheritdoc />
    protected override bool TryWriteCell(Span<byte> buffer, BitLocation bufferStart, BitLocation writeLocation, char input) {
        if (ParseNibble(input) is not { } nibble)
            return false;

        int relativeIndex = (int) (writeLocation.ByteIndex - bufferStart.ByteIndex);
        buffer[relativeIndex] = writeLocation.BitIndex == 4
            ? (byte) ((buffer[relativeIndex] & 0xF) | (nibble << 4))
            : (byte) ((buffer[relativeIndex] & 0xF0) | nibble);
        return true;
    }

    /// <inheritdoc />
    public override async Task<string?> GetTextFromDocumentAsync(BitRange range) {
        if (this.HexView?.Document is null)
            return null;

        byte[] data = new byte[range.ByteLength];
        await this.HexView.Document.ReadBytesAsync(range.Start.ByteIndex, data);

        char[] output = new char[data.Length * 3 - 1];
        this.GetText(data, range, output);

        return new string(output);
    }

    /// <inheritdoc />
    public override TextLine? CreateTextLine(VisualBytesLine line) {
        if (this.HexView == null)
            return null;

        GenericTextRunProperties properties = this.GetTextRunProperties();
        return TextFormatter.Current.FormatLine(
            new HexTextSource(this, line, properties),
            0,
            double.MaxValue,
            new GenericTextParagraphProperties(properties)
        );
    }
    
    public override TextLine? CreateHeaderLine() {
        if (this.HexView == null)
            return null;

        int cellCount = this.CellCount;
        int cpw = this.CellsPerWord;
        int wordCount = cellCount / cpw;
        if (wordCount < 1) {
            return null;
        }

        StringBuilder sb1 = new StringBuilder();
        for (int i = 0; i < cpw; i++) {
            sb1.Append('F');
        }

        int limit = int.Parse(sb1.ToString(), NumberStyles.HexNumber);
        IBinaryDocument? document = this.HexView.Document;
        if (document == null) {
            return null;
        }

        BitLocation startLocation = new BitLocation((ulong) this.HexView.ScrollOffset.Y * (ulong) this.HexView.ActualBytesPerLine);
        BitRange currentRange = new BitRange(startLocation, startLocation);
        ulong offset = currentRange.End.ByteIndex;
        StringBuilder sb = new StringBuilder(cellCount + wordCount - 1);
        for (int i = 0; i < wordCount; i++) {
            ulong j = (offset + (ulong) i) % (ulong) limit;
            sb.Append(j.ToString("X2"));
            if (i != (wordCount - 1)) {
                sb.Append(' ');
            }
        }
        
        GenericTextRunProperties properties = this.GetTextRunProperties();
        return TextFormatter.Current.FormatLine(
            new SimpleTextSource(sb.ToString(), properties),
            0,
            double.MaxValue,
            new GenericTextParagraphProperties(properties)
        );
    }

    private static char GetHexDigit(byte nibble, bool uppercase) => nibble switch {
        < 10 => (char) (nibble + '0'),
        < 16 => (char) (nibble - 10 + (uppercase ? 'A' : 'a')),
        _ => throw new ArgumentOutOfRangeException(nameof(nibble))
    };

    private void GetText(ReadOnlySpan<byte> data, BitRange dataRange, Span<char> buffer) {
        bool uppercase = this.IsUppercase;
        char invalidCellChar = this.InvalidCellChar;

        if (this.HexView?.Document?.ValidRanges is not { } valid) {
            buffer.Fill(invalidCellChar);
            return;
        }

        int index = 0;
        for (int i = 0; i < data.Length; i++) {
            if (i > 0)
                buffer[index++] = ' ';

            BitLocation location1 = new BitLocation(dataRange.Start.ByteIndex + (ulong) i, 0);
            BitLocation location2 = new BitLocation(dataRange.Start.ByteIndex + (ulong) i, 4);
            BitLocation location3 = new BitLocation(dataRange.Start.ByteIndex + (ulong) i + 1, 0);

            byte value = data[i];

            buffer[index] = valid.IsSuperSetOf(new BitRange(location2, location3))
                ? GetHexDigit((byte) ((value >> 4) & 0xF), uppercase)
                : invalidCellChar;

            buffer[index + 1] = valid.IsSuperSetOf(new BitRange(location1, location2))
                ? GetHexDigit((byte) (value & 0xF), uppercase)
                : invalidCellChar;

            index += 2;
        }
    }

    private sealed class HexTextSource : ITextSource {
        private readonly HexColumn _column;
        private readonly GenericTextRunProperties _properties;
        private readonly VisualBytesLine _line;

        public HexTextSource(HexColumn column, VisualBytesLine line, GenericTextRunProperties properties) {
            this._column = column;
            this._line = line;
            this._properties = properties;
        }

        /// <inheritdoc />
        public TextRun? GetTextRun(int textSourceIndex) {
            // Calculate current byte location from text index.
            int byteIndex = Math.DivRem(textSourceIndex, 3, out int nibbleIndex);
            if (byteIndex < 0 || byteIndex >= this._line.Data.Length)
                return null;

            // Special case nibble index 2 (space after byte).
            if (nibbleIndex == 2) {
                if (byteIndex >= this._line.Data.Length - 1)
                    return null;

                return new TextCharacters(" ", this._properties);
            }

            // Find current segment we're in.
            BitLocation currentLocation = new BitLocation(this._line.Range.Start.ByteIndex + (ulong) byteIndex, nibbleIndex * 4);
            VisualBytesLineSegment? segment = this._line.FindSegmentContaining(currentLocation);
            if (segment is null)
                return null;

            // Stringify the segment.
            BitRange range = segment.Range;
            ReadOnlySpan<byte> data = this._line.AsAbsoluteSpan(range);
            Span<char> buffer = stackalloc char[(int) segment.Range.ByteLength * 3 - 1];
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