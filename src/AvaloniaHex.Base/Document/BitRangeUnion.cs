using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;

namespace AvaloniaHex.Base.Document;

/// <summary>
/// Represents a disjoint union of binary ranges.
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public class BitRangeUnion : IReadOnlyBitRangeUnion, ICollection<BitRange> {
    /// <inheritdoc />
    public BitRange EnclosingRange => this._ranges.Count == 0 ? BitRange.Empty : new(this._ranges[0].Start, this._ranges[^1].End);

    /// <inheritdoc />
    public bool IsFragmented => this._ranges.Count > 1;

    /// <inheritdoc cref="IReadOnlyCollection{T}.Count" />
    public int Count => this._ranges.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    private readonly ObservableCollection<BitRange> _ranges = new();

    /// <summary>
    /// Creates a new empty union.
    /// </summary>
    public BitRangeUnion() {
        this._ranges.CollectionChanged += (sender, args) => this.CollectionChanged?.Invoke(this, args);
    }

    /// <summary>
    /// Initializes a new union of bit ranges.
    /// </summary>
    /// <param name="ranges">The ranges to unify.</param>
    public BitRangeUnion(IEnumerable<BitRange> ranges)
        : this() {
        foreach (BitRange range in ranges)
            this.Add(range);
    }

    private (SearchResult Result, int Index) FindFirstOverlappingRange(BitRange range) {
        // TODO: binary search

        range = new BitRange(range.Start, range.End.NextOrMax());
        for (int i = 0; i < this._ranges.Count; i++) {
            BitRange candidate = this._ranges[i];
            if (candidate.ExtendTo(candidate.End.NextOrMax()).OverlapsWith(range)) {
                if (candidate.Start >= range.Start)
                    return (SearchResult.PresentAfterIndex, i);
                return (SearchResult.PresentBeforeIndex, i);
            }

            if (candidate.Start > range.End) {
                return (SearchResult.NotPresentAtIndex, i);
            }
        }

        return (SearchResult.NotPresentAtIndex, this._ranges.Count);
    }

    private void MergeRanges(int startIndex) {
        for (int i = startIndex; i < this._ranges.Count - 1; i++) {
            if (!this._ranges[i].ExtendTo(this._ranges[i].End.Next()).OverlapsWith(this._ranges[i + 1]))
                return;

            this._ranges[i] = this._ranges[i].ExtendTo(this._ranges[i + 1].Start).ExtendTo(this._ranges[i + 1].End);

            this._ranges.RemoveAt(i + 1);
            i--;
        }
    }

    /// <inheritdoc />
    public void Add(BitRange item) {
        (SearchResult result, int index) = this.FindFirstOverlappingRange(item);

        switch (result) {
            case SearchResult.PresentBeforeIndex: this._ranges.Insert(index + 1, item); break;

            case SearchResult.PresentAfterIndex:
            case SearchResult.NotPresentAtIndex:
                this._ranges.Insert(index, item);
                break;

            default: throw new ArgumentOutOfRangeException();
        }

        this.MergeRanges(index);
    }

    /// <inheritdoc />
    public void Clear() => this._ranges.Clear();

    /// <inheritdoc />
    public bool Contains(BitRange item) => this._ranges.Contains(item);

    /// <inheritdoc />
    public bool Contains(BitLocation location) => this.IsSuperSetOf(new BitRange(location, location.NextOrMax()));

    /// <inheritdoc />
    public bool IsSuperSetOf(BitRange range) {
        (SearchResult result, int index) = this.FindFirstOverlappingRange(range);
        if (result == SearchResult.NotPresentAtIndex)
            return false;

        return this._ranges[index].Contains(range);
    }

    /// <inheritdoc />
    public bool IntersectsWith(BitRange range) {
        (SearchResult result, int index) = this.FindFirstOverlappingRange(range);
        if (result == SearchResult.NotPresentAtIndex)
            return false;

        return this._ranges[index].OverlapsWith(range);
    }

    /// <inheritdoc />
    public int GetOverlappingRanges(BitRange range, Span<BitRange> output) {
        (SearchResult result, int index) = this.FindFirstOverlappingRange(range);
        if (result == SearchResult.NotPresentAtIndex)
            return 0;

        int count = 0;
        for (int i = index; i < this._ranges.Count && count < output.Length; i++) {
            BitRange current = this._ranges[i];
            if (current.Start >= range.End)
                break;

            if (current.OverlapsWith(range))
                output[count++] = current;
        }

        return count;
    }

    /// <inheritdoc />
    public int GetIntersectingRanges(BitRange range, Span<BitRange> output) {
        // Get overlapping ranges.
        int count = this.GetOverlappingRanges(range, output);

        // Cut off first and last ranges.
        if (count > 0) {
            output[0] = output[0].Clamp(range);
            if (count > 1)
                output[count - 1] = output[count - 1].Clamp(range);
        }

        return count;
    }

    /// <inheritdoc />
    public void CopyTo(BitRange[] array, int arrayIndex) => this._ranges.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public bool Remove(BitRange item) {
        (SearchResult result, int index) = this.FindFirstOverlappingRange(item);

        if (result == SearchResult.NotPresentAtIndex)
            return false;

        for (int i = index; i < this._ranges.Count; i++) {
            // Is this an overlapping range?
            if (!this._ranges[i].OverlapsWith(item))
                break;

            if (this._ranges[i].Contains(new BitRange(item.Start, item.End.NextOrMax()))) {
                // The range contains the entire range-to-remove, split up the range.
                (BitRange a, BitRange rest) = this._ranges[i].Split(item.Start);
                (BitRange b, BitRange c) = rest.Split(item.End);

                if (a.IsEmpty)
                    this._ranges.RemoveAt(i--);
                else
                    this._ranges[i] = a;

                if (!c.IsEmpty)
                    this._ranges.Insert(i + 1, c);
                break;
            }

            if (item.Contains(this._ranges[i])) {
                // The range-to-remove contains the entire current range.
                this._ranges.RemoveAt(i--);
            }
            else if (item.Start < this._ranges[i].Start) {
                // We are truncating the current range from the left.
                this._ranges[i] = this._ranges[i].Clamp(new BitRange(item.End, BitLocation.Maximum));
            }
            else if (item.End >= this._ranges[i].End) {
                // We are truncating the current range from the right.
                this._ranges[i] = this._ranges[i].Clamp(new BitRange(BitLocation.Minimum, item.Start));
            }
        }

        return true;
    }

    /// <inheritdoc />
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<BitRange> IEnumerable<BitRange>.GetEnumerator() => this.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    /// <summary>
    /// Wraps the union into a <see cref="ReadOnlyBitRangeUnion"/>.
    /// </summary>
    /// <returns>The resulting read-only union.</returns>
    public ReadOnlyBitRangeUnion AsReadOnly() => new(this);

    private enum SearchResult {
        PresentBeforeIndex,
        PresentAfterIndex,
        NotPresentAtIndex,
    }

    /// <summary>
    /// An implementation of an enumerator that enumerates all disjoint ranges within a bit range union.
    /// </summary>
    public struct Enumerator : IEnumerator<BitRange> {
        /// <inheritdoc />
        public BitRange Current =>
            this._index < this._union._ranges.Count
                ? this._union._ranges[this._index]
                : default;

        /// <inheritdoc />
        object IEnumerator.Current => this.Current;

        private readonly BitRangeUnion _union;
        private int _index;

        /// <summary>
        /// Creates a new disjoint bit range union enumerator.
        /// </summary>
        /// <param name="union">The disjoint union to enumerate.</param>
        public Enumerator(BitRangeUnion union) : this() {
            this._union = union;
            this._index = -1;
        }

        /// <inheritdoc />
        public bool MoveNext() {
            this._index++;
            return this._index < this._union._ranges.Count;
        }

        /// <inheritdoc />
        void IEnumerator.Reset() {
        }

        /// <inheritdoc />
        public void Dispose() {
        }
    }
}