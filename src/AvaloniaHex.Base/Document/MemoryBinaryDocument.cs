namespace AvaloniaHex.Base.Document;

/// <summary>
/// Represents a binary document that is backed by an instance of a fixed <see cref="Memory{Byte}"/> buffer.
/// </summary>
public class MemoryBinaryDocument : IBinaryDocument {
    /// <summary>
    /// Gets the underlying memory backing buffer.
    /// </summary>
    public Memory<byte> Memory => this._memory;

    /// <inheritdoc />
    public ulong Length => (ulong) this._memory.Length;

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

    private readonly Memory<byte> _memory;

    /// <summary>
    /// Creates a new memory binary document using the provided memory backing storage.
    /// </summary>
    /// <param name="memory">The memory backing buffer.</param>
    public MemoryBinaryDocument(Memory<byte> memory)
        : this(memory, false) {
    }

    /// <summary>
    /// Creates a new memory binary document using the provided memory backing storage.
    /// </summary>
    /// <param name="memory">The memory backing buffer.</param>
    /// <param name="isReadOnly"><c>true</c> if the document can be edited, <c>false</c> otherwise.</param>
    public MemoryBinaryDocument(Memory<byte> memory, bool isReadOnly) {
        this._memory = memory;
        this.IsReadOnly = isReadOnly;
        this.ValidRanges = new BitRangeUnion([new BitRange(0, this.Length)]).AsReadOnly();
    }

    /// <inheritdoc />
    public void ReadBytes(ulong offset, Span<byte> buffer) {
        this._memory.Span[(int) offset..((int) offset + buffer.Length)].CopyTo(buffer);
    }

    /// <inheritdoc />
    public void WriteBytes(ulong offset, ReadOnlySpan<byte> buffer) {
        if (this.IsReadOnly)
            throw new InvalidOperationException("Document is read-only.");

        buffer.CopyTo(this._memory.Span[(int) offset..((int) offset + buffer.Length)]);
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