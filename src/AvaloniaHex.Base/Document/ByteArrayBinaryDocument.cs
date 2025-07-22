namespace AvaloniaHex.Base.Document;

/// <summary>
/// Wraps a byte array into a binary document.
/// </summary>
[Obsolete("Use MemoryBinaryDocument instead.")]
public class ByteArrayBinaryDocument : IBinaryDocument {
    /// <summary>
    /// Gets the data stored in the document.
    /// </summary>
    public byte[] Data => this._data;

    /// <inheritdoc />
    public ulong Length => (ulong) this._data.Length;

    /// <inheritdoc />
    public bool IsReadOnly { get; }

    /// <inheritdoc />
    public bool CanInsert => false;

    /// <inheritdoc />
    public bool CanRemove => false;

    /// <inheritdoc />
    public IReadOnlyBitRangeUnion ValidRanges { get; }

    /// <inheritdoc />
    public event EventHandler<BinaryDocumentChange>? Changed;

    private readonly byte[] _data;

    /// <summary>
    /// Creates a new byte array document.
    /// </summary>
    /// <param name="data">The backing buffer.</param>
    public ByteArrayBinaryDocument(byte[] data)
        : this(data, false) {
    }

    /// <summary>
    /// Creates a new byte array document.
    /// </summary>
    /// <param name="data">The backing buffer.</param>
    /// <param name="isReadOnly"><c>true</c> if the document can be edited, <c>false</c> otherwise.</param>
    public ByteArrayBinaryDocument(byte[] data, bool isReadOnly) {
        this.IsReadOnly = isReadOnly;
        this._data = data;
        this.ValidRanges = new BitRangeUnion([new BitRange(0, this.Length)]).AsReadOnly();
    }

    /// <inheritdoc />
    public void ReadBytes(ulong offset, Span<byte> buffer) {
        this._data.AsSpan((int) offset, buffer.Length).CopyTo(buffer);
    }

    /// <inheritdoc />
    public void WriteBytes(ulong offset, ReadOnlySpan<byte> buffer) {
        if (this.IsReadOnly)
            throw new InvalidOperationException("Document is read-only.");

        buffer.CopyTo(this._data.AsSpan((int) offset, buffer.Length));
        this.OnChanged(new BinaryDocumentChange(BinaryDocumentChangeType.Modify, new BitRange(offset, offset + (ulong) buffer.Length)));
    }

    /// <inheritdoc />
    public void InsertBytes(ulong offset, ReadOnlySpan<byte> buffer) {
        if (this.IsReadOnly)
            throw new InvalidOperationException("Document is read-only.");

        throw new InvalidOperationException("Document cannot be resized.");
    }

    /// <inheritdoc />
    public void RemoveBytes(ulong offset, ulong length) {
        if (this.IsReadOnly)
            throw new InvalidOperationException("Document is read-only.");

        throw new InvalidOperationException("Document cannot be resized.");
    }

    void IBinaryDocument.Flush() {
    }

    /// <summary>
    /// Fires the <see cref="Changed"/> event.
    /// </summary>
    /// <param name="e">The event arguments describing the change.</param>
    protected virtual void OnChanged(BinaryDocumentChange e) => this.Changed?.Invoke(this, e);

    void IDisposable.Dispose() {
    }
}