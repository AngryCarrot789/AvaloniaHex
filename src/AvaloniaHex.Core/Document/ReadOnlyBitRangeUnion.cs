using System.Collections;
using System.Collections.Specialized;

namespace AvaloniaHex.Core.Document;

/// <summary>
/// Represents a read-only disjoint union of binary ranges in a document.
/// </summary>
public class ReadOnlyBitRangeUnion : IReadOnlyBitRangeUnion {
    /// <inheritdoc />
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    /// The empty union.
    /// </summary>
    public static readonly ReadOnlyBitRangeUnion Empty = new ReadOnlyBitRangeUnion(new BitRangeUnion());

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
    public int Count => this._union.Count;

    /// <inheritdoc />
    public BitRange EnclosingRange => this._union.EnclosingRange;

    /// <inheritdoc />
    public bool IsFragmented => this._union.IsFragmented;

    /// <inheritdoc />
    public bool Contains(BitLocation location) => this._union.Contains(location);

    /// <inheritdoc />
    public bool IsSuperSetOf(BitRange range) => this._union.IsSuperSetOf(range);

    /// <inheritdoc />
    public bool IntersectsWith(BitRange range) => this._union.IntersectsWith(range);

    /// <inheritdoc />
    public BitRangeUnion.Enumerator GetEnumerator() => this._union.GetEnumerator();

    IEnumerator<BitRange> IEnumerable<BitRange>.GetEnumerator() => this._union.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) this._union).GetEnumerator();
}