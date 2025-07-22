using Avalonia;
using Avalonia.Media.TextFormatting;

namespace AvaloniaHex.Async.Rendering;

/// <summary>
/// Represents the column rendering the line offsets.
/// </summary>
public class OffsetColumn : Column {
    /// <summary>
    /// Defines the <see cref="IsUppercase"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsUppercaseProperty =
        AvaloniaProperty.Register<OffsetColumn, bool>(nameof(IsUppercase), true);

    /// <inheritdoc />
    public override Size MinimumSize => this._minimumSize;

    /// <summary>
    /// Gets or sets a value indicating whether the hexadecimal digits should be rendered in uppercase or not.
    /// </summary>
    public bool IsUppercase {
        get => this.GetValue(IsUppercaseProperty);
        set => this.SetValue(IsUppercaseProperty, value);
    }

    private Size _minimumSize;
    
    public OffsetColumn() {
    }

    static OffsetColumn() {
        IsUppercaseProperty.Changed.AddClassHandler<OffsetColumn, bool>(OnIsUpperCaseChanged);
        IsHeaderVisibleProperty.OverrideDefaultValue<OffsetColumn>(false);
        HeaderProperty.OverrideDefaultValue<OffsetColumn>("Offset");
    }

    private static void OnIsUpperCaseChanged(OffsetColumn arg1, AvaloniaPropertyChangedEventArgs<bool> arg2) {
        arg1.HexView?.InvalidateVisualLines();
    }

    /// <inheritdoc />
    public override void Measure() {
        if (this.HexView is null) {
            this._minimumSize = default;
        }
        else {
            TextLine dummy = this.CreateTextLine("00000000:")!;
            this._minimumSize = new Size(dummy.Width, dummy.Height);
        }
    }

    /// <inheritdoc />
    public override TextLine? CreateTextLine(VisualBytesLine line) {
        if (this.HexView is null)
            throw new InvalidOperationException();

        return this.CreateTextLine(this.FormatOffset(line.Range.Start.ByteIndex));
    }

    /// <summary>
    /// Formats the provided offset to a string to be displayed in the column.
    /// </summary>
    /// <param name="offset">The offset to format.</param>
    /// <returns>The formatted offset.</returns>
    protected virtual string FormatOffset(ulong offset) => this.IsUppercase ? $"{offset:X8}:" : $"{offset:x8}:";

    private TextLine? CreateTextLine(string text) {
        if (this.HexView is null)
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