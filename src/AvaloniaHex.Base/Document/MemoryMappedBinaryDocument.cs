using System.IO.MemoryMappedFiles;

namespace AvaloniaHex.Base.Document;

/// <summary>
/// Represents a binary document that is backed by a file that is mapped into memory.
/// </summary>
public class MemoryMappedBinaryDocument : IBinaryDocument {
    /// <summary>
    /// Gets the underlying memory mapped file that is used as a backing storage for this document.
    /// </summary>
    public MemoryMappedFile File { get; }

    /// <inheritdoc />
    public ulong Length { get; }

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

    private readonly MemoryMappedViewAccessor _accessor;
    private readonly bool _leaveOpen;

    /// <summary>
    /// Opens a file as a memory mapped document.
    /// </summary>
    /// <param name="filePath">The file to memory map.</param>
    public MemoryMappedBinaryDocument(string filePath)
        : this(MemoryMappedFile.CreateFromFile(filePath, FileMode.OpenOrCreate), false, false) {
    }

    /// <summary>
    /// Wraps a memory mapped file in a document.
    /// </summary>
    /// <param name="file">The file to use as a backing storage.</param>
    /// <param name="leaveOpen"><c>true</c> if <paramref name="file"/> should be kept open on disposing, <c>false</c> otherwise.</param>
    public MemoryMappedBinaryDocument(MemoryMappedFile file, bool leaveOpen)
        : this(file, leaveOpen, false) {
    }

    /// <summary>
    /// Wraps a memory mapped file in a document.
    /// </summary>
    /// <param name="file">The file to use as a backing storage.</param>
    /// <param name="leaveOpen"><c>true</c> if <paramref name="file"/> should be kept open on disposing, <c>false</c> otherwise.</param>
    /// <param name="isReadOnly"><c>true</c> if the document can be edited, <c>false</c> otherwise.</param>
    public MemoryMappedBinaryDocument(MemoryMappedFile file, bool leaveOpen, bool isReadOnly) {
        this.File = file;
        this._leaveOpen = leaveOpen;
        this._accessor = file.CreateViewAccessor();

        // Yuck! But this seems to be the only way to get the length from a MemoryMappedFile.
        using MemoryMappedViewStream stream = file.CreateViewStream();
        this.Length = (ulong) stream.Length;

        this.ValidRanges = new BitRangeUnion([new BitRange(0, this.Length)]).AsReadOnly();
        this.IsReadOnly = isReadOnly;
    }

    /// <inheritdoc />
    public void ReadBytes(ulong offset, Span<byte> buffer) {
        this._accessor.SafeMemoryMappedViewHandle.ReadSpan(offset, buffer);
    }

    /// <inheritdoc />
    public void WriteBytes(ulong offset, ReadOnlySpan<byte> buffer) {
        if (this.IsReadOnly)
            throw new InvalidOperationException("Document is read-only.");

        this._accessor.SafeMemoryMappedViewHandle.WriteSpan(offset, buffer);
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

    /// <inheritdoc />
    public void Flush() => this._accessor.Flush();

    /// <summary>
    /// Fires the <see cref="Changed"/> event.
    /// </summary>
    /// <param name="e">The event arguments describing the change.</param>
    protected virtual void OnChanged(BinaryDocumentChange e) => this.Changed?.Invoke(this, e);

    /// <inheritdoc />
    public void Dispose() {
        this._accessor.Dispose();

        if (!this._leaveOpen)
            this.File.Dispose();
    }
}