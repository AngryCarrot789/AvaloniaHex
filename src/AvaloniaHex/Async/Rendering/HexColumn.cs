using Avalonia;
using Avalonia.Media.TextFormatting;
using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
///     Represents a column that renders binary data using hexadecimal number encoding.
/// </summary>
public class HexColumn : CellBasedColumn {
    /// <summary>
    ///     Dependency property for <see cref="UseDynamicHeader" />
    /// </summary>
    public static readonly StyledProperty<bool> UseDynamicHeaderProperty =
        AvaloniaProperty.Register<HexColumn, bool>(nameof(UseDynamicHeader), true);

    /// <summary>
    ///     Defines the <see cref="IsUppercase" /> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsUppercaseProperty =
        AvaloniaProperty.Register<HexColumn, bool>(nameof(IsUppercase), true);

    /// <summary>
    ///     Gets or sets a value indicating whether the header of this column should be dynamically
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
    public override int BitsPerCell => 4;

    /// <inheritdoc />
    public override int CellsPerWord => 2;

    /// <summary>
    ///     Gets or sets a value indicating whether the hexadecimal digits should be rendered in uppercase or not.
    /// </summary>
    public bool IsUppercase {
        get => this.GetValue(IsUppercaseProperty);
        set => this.SetValue(IsUppercaseProperty, value);
    }

    static HexColumn() {
        IsUppercaseProperty.Changed.AddClassHandler<HexColumn, bool>(OnIsUpperCaseChanged);
        UseDynamicHeaderProperty.Changed.AddClassHandler<BinaryColumn, bool>(OnUseDynamicHeaderChanged);
        CursorProperty.OverrideDefaultValue<HexColumn>(IBeamCursor);
        HeaderProperty.OverrideDefaultValue<HexColumn>("Hex");
    }

    private static byte? ParseNibble(char c) {
        return c switch {
            >= '0' and <= '9' => (byte?) (c - '0'),
            >= 'a' and <= 'f' => (byte?) (c - 'a' + 10),
            >= 'A' and <= 'F' => (byte?) (c - 'A' + 10),
            _ => null
        };
    }

    private static char GetHexDigit(byte nibble, bool uppercase) {
        return nibble switch {
            < 10 => (char) (nibble + '0'),
            < 16 => (char) (nibble - 10 + (uppercase ? 'A' : 'a')),
            _ => throw new ArgumentOutOfRangeException(nameof(nibble))
        };
    }

    private static void OnIsUpperCaseChanged(HexColumn arg1, AvaloniaPropertyChangedEventArgs<bool> arg2) {
        if (arg1.HexView is null) {
            return;
        }

        arg1.HexView.InvalidateVisualLines();
        arg1.HexView.InvalidateHeaders();
    }

    private static void OnUseDynamicHeaderChanged(BinaryColumn arg1, AvaloniaPropertyChangedEventArgs<bool> arg2) {
        arg1.HexView?.InvalidateHeaders();
    }

    /// <inheritdoc />
    protected override string PrepareTextInput(string input) {
        return input.Replace(" ", "");
    }

    /// <inheritdoc />
    protected override bool TryWriteCell(Span<byte> buffer, BitLocation bufferStart, BitLocation writeLocation, char input) {
        if (ParseNibble(input) is not { } nibble) {
            return false;
        }

        int relativeIndex = (int) (writeLocation.ByteIndex - bufferStart.ByteIndex);
        buffer[relativeIndex] = writeLocation.BitIndex == 4
            ? (byte) ((buffer[relativeIndex] & 0xF) | (nibble << 4))
            : (byte) ((buffer[relativeIndex] & 0xF0) | nibble);
        return true;
    }

    /// <inheritdoc />
    public override string? GetText(BitRange range) {
        if (this.HexView?.BinarySource is null) {
            return null;
        }

        byte[] data = new byte[range.ByteLength];
        this.HexView.BinarySource.ReadAvailableData(range.Start.ByteIndex, data, null);

        char[] output = new char[data.Length * 3 - 1];
        byte?[] nullableData = new byte?[data.Length];
        for (int i = 0; i < data.Length; i++)
            nullableData[i] = data[i];
        this.GetText(nullableData, range, output);

        return new string(output);
    }

    /// <inheritdoc />
    public override TextLine? CreateHeaderLine() {
        if (!this.UseDynamicHeader) {
            return base.CreateHeaderLine();
        }

        if (this.HexView is null) {
            return null;
        }

        // Generate header text.
        int count = this.HexView.ActualBytesPerLine;
        char[] buffer = new char[count * 3 - 1];
        for (int i = 0; i < count; i++) {
            buffer[i * 3] = GetHexDigit((byte) ((i >> 4) & 0xF), this.IsUppercase);
            buffer[i * 3 + 1] = GetHexDigit((byte) (i & 0xF), this.IsUppercase);
            if (i < count - 1) {
                buffer[i * 3 + 2] = ' ';
            }
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
        if (this.HexView is null) {
            return null;
        }

        GenericTextRunProperties properties = this.GetTextRunProperties();
        return TextFormatter.Current.FormatLine(
            new HexTextSource(this, line, properties),
            0,
            double.MaxValue,
            new GenericTextParagraphProperties(properties)
        );
    }

    private void GetText(ReadOnlySpan<byte?> data, BitRange dataRange, Span<char> buffer) {
        bool uppercase = this.IsUppercase;
        char invalidCellChar = this.InvalidCellChar;

        if (this.HexView?.BinarySource?.ApplicableRange is not { } valid) {
            buffer.Fill(invalidCellChar);
            return;
        }

        int index = 0;
        for (int i = 0; i < data.Length; i++) {
            if (i > 0) {
                buffer[index++] = ' ';
            }

            BitLocation location1 = new(dataRange.Start.ByteIndex + (ulong) i, 0);
            BitLocation location2 = new(dataRange.Start.ByteIndex + (ulong) i, 4);
            BitLocation location3 = new(dataRange.Start.ByteIndex + (ulong) i + 1, 0);

            byte? value = data[i];

            buffer[index] = value.HasValue && valid.Contains(new BitRange(location2, location3))
                ? GetHexDigit((byte) ((value.Value >> 4) & 0xF), uppercase)
                : invalidCellChar;

            buffer[index + 1] = value.HasValue && valid.Contains(new BitRange(location1, location2))
                ? GetHexDigit((byte) (value.Value & 0xF), uppercase)
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
            if (byteIndex < 0 || byteIndex >= this._line.Buffer.Length) {
                return null;
            }

            // Special case nibble index 2 (space after byte).
            if (nibbleIndex == 2) {
                if (byteIndex >= this._line.Buffer.Length - 1) {
                    return null;
                }

                return new TextCharacters(" ", this._properties);
            }

            // Find current segment we're in.
            BitLocation currentLocation = new(this._line.Range.Start.ByteIndex + (ulong) byteIndex, nibbleIndex * 4);
            VisualBytesLineSegment? segment = this._line.FindSegmentContaining(currentLocation);
            if (segment is null) {
                return null;
            }

            // Stringify the segment.
            BitRange range = segment.Range;
            ReadOnlySpan<byte?> data = this._line.AsAbsoluteSpan(range);
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