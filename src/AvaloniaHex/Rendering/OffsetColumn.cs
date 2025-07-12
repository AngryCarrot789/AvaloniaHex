using Avalonia;
using Avalonia.Media.TextFormatting;

namespace AvaloniaHex.Rendering;

/// <summary>
/// Represents the column rendering the line offsets.
/// </summary>
public class OffsetColumn : Column {
    /// <summary>
    /// Defines the <see cref="IsUppercase"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsUppercaseProperty = AvaloniaProperty.Register<HexColumn, bool>(nameof(IsUppercase), true);
    public static readonly StyledProperty<ulong> AdditionalOffsetProperty = AvaloniaProperty.Register<OffsetColumn, ulong>(nameof(AdditionalOffset));
    
    private Size _minimumSize;
    
    /// <inheritdoc />
    public override Size MinimumSize => this._minimumSize;

    /// <summary>
    /// Gets or sets a value indicating whether the hexadecimal digits should be rendered in uppercase or not.
    /// </summary>
    public bool IsUppercase {
        get => this.GetValue(IsUppercaseProperty);
        set => this.SetValue(IsUppercaseProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the additional value added to the actual drawn offset.
    /// </summary>
    public ulong AdditionalOffset {
        get => this.GetValue(AdditionalOffsetProperty);
        set => this.SetValue(AdditionalOffsetProperty, value);
    }

    public OffsetColumn() {
    }

    static OffsetColumn() {
        IsUppercaseProperty.Changed.AddClassHandler<OffsetColumn, bool>(OnIsUpperCaseChanged);
        AdditionalOffsetProperty.Changed.AddClassHandler<OffsetColumn, ulong>(OnAdditionalOffsetChanged);
    }

    private static void OnIsUpperCaseChanged(OffsetColumn arg1, AvaloniaPropertyChangedEventArgs<bool> arg2) {
        arg1.HexView?.InvalidateVisualLines();
    }
    
    private static void OnAdditionalOffsetChanged(OffsetColumn arg1, AvaloniaPropertyChangedEventArgs<ulong> arg2) {
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

        ulong offset = this.AdditionalOffset + line.Range.Start.ByteIndex;
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