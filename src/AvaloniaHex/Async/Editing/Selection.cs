using AvaloniaHex.Async.Rendering;
using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Async.Editing;

/// <summary>
/// Represents a selection within a hex editor.
/// </summary>
public class Selection {
    /// <summary>
    /// Gets the hex view the selection is rendered on.
    /// </summary>
    public AsyncHexView HexView { get; }

    /// <summary>
    /// Gets or sets the range the selection spans.
    /// </summary>
    public BitRange Range {
        get => this._range;
        set {
            value = this.HexView.BinarySource is { } document
                ? value.Clamp(document.ApplicableRange)
                : BitRange.Empty;

            if (this._range != value) {
                this._range = value;
                this.OnRangeChanged();
            }
        }
    }

    /// <summary>
    /// Fires when the selection range has changed.
    /// </summary>
    public event EventHandler? RangeChanged;

    private BitRange _range;

    internal Selection(AsyncHexView hexView) {
        this.HexView = hexView;
    }

    private void OnRangeChanged() {
        this.RangeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Selects the entire document.
    /// </summary>
    public void SelectAll() {
        this.Range = this.HexView is { BinarySource.ApplicableRange: var enclosingRange }
            ? enclosingRange
            : default;
    }

    /// <summary>
    /// Selects the line where the caret is
    /// </summary>
    /// <param name="caret"></param>
    public void SelectLine(Caret caret) {
        VisualBytesLine? line = this.HexView.GetVisualLineByLocation(caret.Location);
        this.Range = line != null ? line.Range : default;
        caret.Location = this.Range.Start;
    }
}