using System.Collections;
using AvaloniaHex.Core.Document;

namespace AvaloniaHex.Rendering;

internal sealed class VisualBytesLinesBuffer : IReadOnlyList<VisualBytesLine> {
    private readonly HexView _owner;
    private readonly Stack<VisualBytesLine> _pool = new Stack<VisualBytesLine>();
    private readonly List<VisualBytesLine> _activeLines = new List<VisualBytesLine>();

    public VisualBytesLinesBuffer(HexView owner) {
        this._owner = owner;
    }

    public int Count => this._activeLines.Count;

    public VisualBytesLine this[int index] => this._activeLines[index];

    public VisualBytesLine? GetVisualLineByLocation(BitLocation location) {
        for (int i = 0; i < this._activeLines.Count; i++) {
            VisualBytesLine line = this._activeLines[i];
            if (line.VirtualRange.Contains(location))
                return line;

            if (line.Range.Start > location)
                return null;
        }

        return null;
    }

    public IEnumerable<VisualBytesLine> GetVisualLinesByRange(BitRange range) {
        foreach (VisualBytesLine line in this._activeLines) {
            if (line.VirtualRange.OverlapsWith(range))
                yield return line;

            if (line.Range.Start >= range.End)
                yield break;
        }
    }

    public VisualBytesLine GetOrCreateVisualLine(BitRange virtualRange) {
        VisualBytesLine? newLine = null;

        // Find existing line or create a new one, while keeping the list of visual lines ordered by range.
        for (int i = 0; i < this._activeLines.Count; i++) {
            // Exact match on start?
            VisualBytesLine currentLine = this._activeLines[i];
            if (currentLine.VirtualRange.Start == virtualRange.Start) {
                // Edge-case: if our range is not exactly the same, the line's range is outdated (e.g., as a result of
                // inserting or removing a character at the end of the document).
                if (currentLine.SetRange(virtualRange))
                    currentLine.Invalidate();

                return currentLine;
            }

            // If the next line is further than the requested start, the line does not exist.
            if (currentLine.Range.Start > virtualRange.Start) {
                newLine = this.Rent(virtualRange);
                this._activeLines.Insert(i, newLine);
                break;
            }
        }

        // We didn't find any line for the location, add it to the end.
        if (newLine == null) {
            newLine = this.Rent(virtualRange);
            this._activeLines.Add(newLine);
        }

        return newLine;
    }

    public void RemoveOutsideOfRange(BitRange range) {
        for (int i = 0; i < this._activeLines.Count; i++) {
            VisualBytesLine line = this._activeLines[i];
            if (!range.Contains(line.VirtualRange.Start)) {
                this.Return(line);
                this._activeLines.RemoveAt(i--);
            }
        }
    }

    public void Clear() {
        foreach (VisualBytesLine instance in this._activeLines)
            this.Return(instance);
        this._activeLines.Clear();
    }

    private VisualBytesLine Rent(BitRange virtualRange) {
        VisualBytesLine line = this.GetPooledLine();
        line.SetRange(virtualRange);
        line.Invalidate();
        return line;
    }

    private VisualBytesLine GetPooledLine() {
        while (this._pool.TryPop(out VisualBytesLine? line)) {
            if (line.Data.Length == this._owner.ActualBytesPerLine)
                return line;
        }

        return new VisualBytesLine(this._owner);
    }

    private void Return(VisualBytesLine line) {
        this._pool.Push(line);
    }

    public IEnumerator<VisualBytesLine> GetEnumerator() => this._activeLines.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}