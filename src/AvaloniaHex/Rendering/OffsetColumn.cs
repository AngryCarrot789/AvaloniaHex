using Avalonia;
using Avalonia.Media.TextFormatting;

namespace AvaloniaHex.Rendering;

/// <summary>
/// Represents the column rendering the line offsets.
/// </summary>
public class OffsetColumn : Column {
    private Size _minimumSize;

    static OffsetColumn() {
        IsUppercaseProperty.Changed.AddClassHandler<HexColumn, bool>(OnIsUpperCaseChanged);
    }

    /// <inheritdoc />
    public override Size MinimumSize => this._minimumSize;

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
    public override void Measure() {
        if (this.HexView == null) {
            this._minimumSize = default;
        }
        else {
            TextLine dummy = this.CreateTextLine("00000000:")!;
            this._minimumSize = new Size(dummy.Width, dummy.Height);
        }
    }

    /// <inheritdoc />
    public override TextLine? CreateTextLine(VisualBytesLine line) {
        if (this.HexView == null)
            throw new InvalidOperationException();

        ulong offset = line.Range.Start.ByteIndex;
        string text = this.IsUppercase
            ? $"{offset:X8}:"
            : $"{offset:x8}:";

        return this.CreateTextLine(text);
    }

    public override TextLine? CreateHeaderLine() {
        if (this.HexView == null)
            throw new InvalidOperationException();

        return this.CreateTextLine("Offset");
    }

    private TextLine? CreateTextLine(string text) {
        if (this.HexView == null)
            return null;

        GenericTextRunProperties properties = this.GetTextRunProperties();
        return TextFormatter.Current.FormatLine(
            new SimpleTextSource(text, properties),
            0,
            double.MaxValue,
            new GenericTextParagraphProperties(properties)
        )!;
    }
}