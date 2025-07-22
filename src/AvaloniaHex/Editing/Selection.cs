using AvaloniaHex.Base.Document;
using AvaloniaHex.Rendering;

namespace AvaloniaHex.Editing;

/// <summary>
/// Represents a selection within a hex editor.
/// </summary>
public class Selection {
    /// <summary>
    /// Gets the hex view the selection is rendered on.
    /// </summary>
    public HexView HexView { get; }

    /// <summary>
    /// Gets or sets the range the selection spans.
    /// </summary>
    public BitRange Range {
        get => this._range;
        set {
            value = this.HexView.Document is { } document
                ? value.Clamp(document.ValidRanges.EnclosingRange)
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

    internal Selection(HexView hexView) {
        this.HexView = hexView;
    }

    private void OnRangeChanged() {
        this.RangeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Selects the entire document.
    /// </summary>
    public void SelectAll() {
        this.Range = this.HexView is { Document.ValidRanges.EnclosingRange: var enclosingRange }
            ? enclosingRange
            : default;
    }
}