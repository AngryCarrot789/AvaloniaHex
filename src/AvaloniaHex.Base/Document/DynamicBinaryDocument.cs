using System.Runtime.InteropServices;

namespace AvaloniaHex.Base.Document;

/// <summary>
/// Represents a binary document that can be dynamically resized.
/// </summary>
public class DynamicBinaryDocument : IBinaryDocument {
    /// <inheritdoc />
    public ulong Length => (ulong) this._data.Count;

    /// <inheritdoc />
    public bool IsReadOnly { get; set; }

    /// <inheritdoc />
    public bool CanInsert { get; set; } = true;

    /// <inheritdoc />
    public bool CanRemove { get; set; } = true;

    /// <inheritdoc />
    public IReadOnlyBitRangeUnion ValidRanges { get; }

    /// <inheritdoc />
    public event EventHandler<BinaryDocumentChange>? Changed;

    // TODO: List<byte> should be replaced with something that is more efficient for insert/remove operations
    //       such as a Rope or gap-buffer.
    private readonly List<byte> _data;

    private readonly BitRangeUnion _validRanges;

    /// <summary>
    /// Creates a new empty dynamic binary document.
    /// </summary>
    public DynamicBinaryDocument() {
        this._data = new List<byte>();
        this._validRanges = new BitRangeUnion();
        this.ValidRanges = this._validRanges.AsReadOnly();
    }

    /// <summary>
    /// Creates a new dynamic binary document with the provided initial data.
    /// </summary>
    /// <param name="initialData">The data to initialize the document with.</param>
    public DynamicBinaryDocument(byte[] initialData) {
        this._data = new List<byte>(initialData);
        this._validRanges = new BitRangeUnion([new BitRange(0ul, (ulong) initialData.Length)]);
        this.ValidRanges = this._validRanges.AsReadOnly();
    }

    private void AssertIsWriteable() {
        if (this.IsReadOnly)
            throw new InvalidOperationException("Document is read-only.");
    }

    /// <inheritdoc />
    public void ReadBytes(ulong offset, Span<byte> buffer) {
        CollectionsMarshal.AsSpan(this._data).Slice((int) offset, buffer.Length).CopyTo(buffer);
    }

    /// <inheritdoc />
    public void WriteBytes(ulong offset, ReadOnlySpan<byte> buffer) {
        this.AssertIsWriteable();

        buffer.CopyTo(CollectionsMarshal.AsSpan(this._data).Slice((int) offset, buffer.Length));
        this.OnChanged(new BinaryDocumentChange(BinaryDocumentChangeType.Modify, new BitRange(offset, offset + (ulong) buffer.Length)));
    }

    /// <inheritdoc />
    public void InsertBytes(ulong offset, ReadOnlySpan<byte> buffer) {
        this.AssertIsWriteable();

        if (!this.CanInsert)
            throw new InvalidOperationException("Data cannot be inserted into the document.");

        this._data.InsertRange((int) offset, buffer.ToArray());
        this._validRanges.Add(new BitRange(this._validRanges.EnclosingRange.End, this._validRanges.EnclosingRange.End.AddBytes((ulong) buffer.Length)));

        this.OnChanged(new BinaryDocumentChange(BinaryDocumentChangeType.Insert, new BitRange(offset, offset + (ulong) buffer.Length)));
    }

    /// <inheritdoc />
    public void RemoveBytes(ulong offset, ulong length) {
        this.AssertIsWriteable();

        if (!this.CanRemove)
            throw new InvalidOperationException("Data cannot be removed from the document.");

        this._data.RemoveRange((int) offset, (int) length);
        this._validRanges.Remove(new BitRange(this._validRanges.EnclosingRange.End.SubtractBytes(length), this._validRanges.EnclosingRange.End));

        this.OnChanged(new BinaryDocumentChange(BinaryDocumentChangeType.Remove, new BitRange(offset, offset + length)));
    }

    /// <summary>
    /// Fires the <see cref="Changed"/> event.
    /// </summary>
    /// <param name="e">The event arguments describing the change.</param>
    protected virtual void OnChanged(BinaryDocumentChange e) => this.Changed?.Invoke(this, e);

    /// <inheritdoc />
    public void Flush() {
    }

    /// <inheritdoc />
    public void Dispose() {
    }

    /// <summary>
    /// Serializes the contents of the document into a byte array.
    /// </summary>
    /// <returns>The serialized contents.</returns>
    public byte[] ToArray() => this._data.ToArray();
}