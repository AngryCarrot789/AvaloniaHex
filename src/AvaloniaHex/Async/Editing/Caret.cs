using AvaloniaHex.Async.Rendering;
using AvaloniaHex.Base.Document;

namespace AvaloniaHex.Async.Editing;

/// <summary>
/// Represents a caret in a hex editor.
/// </summary>
public sealed class Caret {
    /// <summary>
    /// Gets the hex view the caret is rendered on.
    /// </summary>
    public AsyncHexView HexView { get; }

    /// <summary>
    /// Gets or sets the current location of the caret.
    /// </summary>
    public BitLocation Location {
        get => this._location;
        set {
            CellBasedColumn? primaryColumn = this.PrimaryColumn;

            if (primaryColumn is null || this.HexView.BinarySource is not { ApplicableRange: var enclosingRange }) {
                // We have no column or document to select bytes in...
                value = default;
            }
            else if (!enclosingRange.Contains(value)) {
                // Edge-case, we may not be in the enclosing document range
                // (e.g., virtual cell at the end of the document or trying to move before first valid range).
                value = value < enclosingRange.Start
                    ? new BitLocation(enclosingRange.Start.ByteIndex, primaryColumn.FirstBitIndex)
                    : new BitLocation(enclosingRange.End.ByteIndex, primaryColumn.FirstBitIndex);
            }
            else {
                // Otherwise, always make sure we are at a valid cell in the current column.
                value = primaryColumn.AlignToCell(value);
            }

            if (this._location != value) {
                this._location = value;
                this.OnLocationChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the index of the primary column the caret is active in.
    /// </summary>
    public int PrimaryColumnIndex {
        get => this._primaryColumnIndex;
        set {
            if (this._primaryColumnIndex != value) {
                this._primaryColumnIndex = value;

                // Force reclamp of caret location.
                this.Location = this._location;

                this.OnPrimaryColumnChanged();
            }
        }
    }

    /// <summary>
    /// Gets the primary column the caret is active in..
    /// </summary>
    public CellBasedColumn? PrimaryColumn => this.HexView.Columns[this.PrimaryColumnIndex] as CellBasedColumn;

    /// <summary>
    /// Fires when the location of the caret has changed.
    /// </summary>
    public event EventHandler? LocationChanged;

    /// <summary>
    /// Fires when the primary column of the caret has changed.
    /// </summary>
    public event EventHandler? PrimaryColumnChanged;

    private BitLocation _location;
    private int _primaryColumnIndex = 1;

    internal Caret(AsyncHexView view) {
        this.HexView = view;
    }

    private void OnLocationChanged() {
        this.HexView.BringIntoView(this.Location);
        this.LocationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPrimaryColumnChanged() {
        this.PrimaryColumnChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves the caret to the beginning of the document.
    /// </summary>
    public void GoToStartOfDocument() {
        if (this.PrimaryColumn is not { } primaryColumn)
            return;

        this.Location = primaryColumn.GetFirstLocation();
    }

    /// <summary>
    /// Moves the caret to the end of the document.
    /// </summary>
    public void GoToEndOfDocument() {
        if (this.PrimaryColumn is not { } primaryColumn)
            return;

        this.Location = primaryColumn.GetLastLocation(true);
    }

    /// <summary>
    /// Moves the caret to the beginning of the current line in the hex editor.
    /// </summary>
    public void GoToStartOfLine() {
        if (this.PrimaryColumn is not { } primaryColumn)
            return;

        if (this.HexView is not { BinarySource.ApplicableRange: var enclosingRange })
            return;

        ulong bytesPerLine = (ulong) this.HexView.ActualBytesPerLine;
        ulong lineIndex = (this.Location.ByteIndex - enclosingRange.Start.ByteIndex) / bytesPerLine;

        ulong byteIndex = enclosingRange.Start.ByteIndex + lineIndex * bytesPerLine;
        int bitIndex = primaryColumn.FirstBitIndex;

        this.Location = new BitLocation(byteIndex, bitIndex);
    }

    /// <summary>
    /// Moves the caret to the end of the current line in the hex editor.
    /// </summary>
    public void GoToEndOfLine() {
        if (this.HexView is not { BinarySource.ApplicableRange: var enclosingRange })
            return;

        ulong bytesPerLine = (ulong) this.HexView.ActualBytesPerLine;
        ulong lineIndex = (this.Location.ByteIndex - enclosingRange.Start.ByteIndex) / bytesPerLine;

        ulong byteIndex = Math.Min(
            enclosingRange.Start.ByteIndex + (lineIndex + 1) * bytesPerLine,
            enclosingRange.End.ByteIndex
        ) - 1;

        this.Location = new BitLocation(byteIndex, 0);
    }

    /// <summary>
    /// Moves the caret one cell to the left in the hex editor.
    /// </summary>
    public void GoLeft() {
        if (this.PrimaryColumn is { } column)
            this.Location = column.GetPreviousLocation(this.Location);
    }

    /// <summary>
    /// Moves the caret one cell up in the hex editor.
    /// </summary>
    public void GoUp() => this.GoBackward((ulong) this.HexView.ActualBytesPerLine);

    /// <summary>
    /// Moves the caret one page up in the hex editor.
    /// </summary>
    public void GoPageUp() => this.GoBackward((ulong) (this.HexView.ActualBytesPerLine * this.HexView.VisualLines.Count));

    /// <summary>
    /// Moves the caret the provided number of bytes backward in the hex editor.
    /// </summary>
    /// <param name="byteCount">The number of bytes to move.</param>
    public void GoBackward(ulong byteCount) {
        if (this.HexView is not { BinarySource.ApplicableRange: var enclosingRange } || this.PrimaryColumn is null)
            return;

        // Note: We cannot use BitLocation.Clamp due to unsigned overflow that may happen.

        this.Location = this.Location.ByteIndex - enclosingRange.Start.ByteIndex >= byteCount
            ? new BitLocation(this.Location.ByteIndex - byteCount, this.Location.BitIndex)
            : new BitLocation(enclosingRange.Start.ByteIndex, this.PrimaryColumn.FirstBitIndex);
    }

    /// <summary>
    /// Moves the caret one cell to the right in the hex editor.
    /// </summary>
    public void GoRight() {
        if (this.PrimaryColumn is { } column)
            this.Location = column.GetNextLocation(this.Location, true, true);
    }

    /// <summary>
    /// Moves the caret one cell down in the hex editor.
    /// </summary>
    public void GoDown() => this.GoForward((ulong) this.HexView.ActualBytesPerLine);

    /// <summary>
    /// Moves the caret one page down in the hex editor.
    /// </summary>
    public void GoPageDown() => this.GoForward((ulong) (this.HexView.ActualBytesPerLine * this.HexView.VisualLines.Count));

    /// <summary>
    /// Moves the caret the provided number of bytes forward in the hex editor.
    /// </summary>
    /// <param name="byteCount">The number of bytes to move.</param>
    public void GoForward(ulong byteCount) {
        if (this.HexView is not { BinarySource.ApplicableRange: var enclosingRange } || this.PrimaryColumn is null)
            return;

        // Note: We cannot use BitLocation.Clamp due to unsigned overflow that may happen.

        if (enclosingRange.End.ByteIndex < byteCount
            || this.Location.ByteIndex >= enclosingRange.End.ByteIndex - byteCount) {
            this.Location = new BitLocation(enclosingRange.End.ByteIndex, this.PrimaryColumn.FirstBitIndex);
            return;
        }

        this.Location = new BitLocation(this.Location.ByteIndex + byteCount, this.Location.BitIndex);
    }
}