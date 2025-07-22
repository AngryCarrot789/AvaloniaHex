using System.Collections;
using System.Collections.Specialized;

namespace AvaloniaHex.Base.Document;

/// <summary>
/// Represents a read-only disjoint union of binary ranges in a document.
/// </summary>
public class ReadOnlyBitRangeUnion : IReadOnlyBitRangeUnion {
    /// <summary>
    /// The empty union.
    /// </summary>
    public static readonly ReadOnlyBitRangeUnion Empty = new(new BitRangeUnion());

    /// <inheritdoc />
    public int Count => this._union.Count;

    /// <inheritdoc />
    public BitRange EnclosingRange => this._union.EnclosingRange;

    /// <inheritdoc />
    public bool IsFragmented => this._union.IsFragmented;

    /// <inheritdoc />
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    private readonly BitRangeUnion _union;

    /// <summary>
    /// Wraps an existing disjoint binary range union into a <see cref="ReadOnlyBitRangeUnion"/>.
    /// </summary>
    /// <param name="union">The union to wrap.</param>
    public ReadOnlyBitRangeUnion(BitRangeUnion union) {
        this._union = union;
        this._union.CollectionChanged += this.UnionOnCollectionChanged;
    }

    private void UnionOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        this.CollectionChanged?.Invoke(this, e);
    }

    /// <inheritdoc />
    public bool Contains(BitLocation location) => this._union.Contains(location);

    /// <inheritdoc />
    public bool IsSuperSetOf(BitRange range) => this._union.IsSuperSetOf(range);

    /// <inheritdoc />
    public bool IntersectsWith(BitRange range) => this._union.IntersectsWith(range);

    /// <inheritdoc />
    public int GetOverlappingRanges(BitRange range, Span<BitRange> output) => this._union.GetOverlappingRanges(range, output);

    /// <inheritdoc />
    public int GetIntersectingRanges(BitRange range, Span<BitRange> output) => this._union.GetIntersectingRanges(range, output);

    /// <inheritdoc />
    public BitRangeUnion.Enumerator GetEnumerator() => this._union.GetEnumerator();

    IEnumerator<BitRange> IEnumerable<BitRange>.GetEnumerator() => this._union.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) this._union).GetEnumerator();
}