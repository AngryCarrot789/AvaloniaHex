namespace AvaloniaHex.Base.Document;

/// <summary>
/// Event args when data is received from a binary source
/// </summary>
/// <param name="Offset">The offset within the hex editor</param>
/// <param name="Count">The amount of bytes received from the source</param>
public record struct DataReceivedEventArgs(ulong Offset, ulong Count);

public interface IBinarySource {
    /// <summary>
    /// Gets the range of binary data readable by this binary source. Reading outside of this range results in undefined behaviour
    /// </summary>
    BitRange ApplicableRange { get; }

    /// <summary>
    /// Gets whether writing data back into the source is allowed (via <see cref="OnUserInput"/>).
    /// </summary>
    bool CanWriteBackInto { get; }

    /// <summary>
    /// An event fired when requested data is received
    /// </summary>
    event EventHandler<DataReceivedEventArgs> DataReceived;

    /// <summary>
    /// Signal from the hex view that the range is no longer in use, so this source can clear it from internal memory.
    /// When visual lines are cleared, an offset of 0 and count of ulong.MaxValue is given
    /// </summary>
    /// <param name="offset">The document offset</param>
    /// <param name="count">The amount of bytes invalidated</param>
    void InvalidateCache(ulong offset, ulong count);

    /// <summary>
    /// Reads bytes that are stored in the internal buffer. 
    /// </summary>
    /// <param name="offset">The offset within the document</param>
    /// <param name="buffer">The destination buffer</param>
    /// <param name="affectedRanges">A union that affected ranges in the buffer should be added to, if non-null</param>
    /// <returns>The amount of bytes written into buffer. Should equal the total bytes in the affectedRanges union, if non-null</returns>
    int ReadAvailableData(ulong offset, Span<byte> buffer, BitRangeUnion? affectedRanges);

    /// <summary>
    /// Reads bytes in the internal cache or requests data and waits for the data to be fully read
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="buffer"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    Task<int> ReadAvailableDataAsync(ulong offset, Memory<byte> buffer, CancellationToken cancellation);
    
    /// <summary>
    /// Requests to read data from this source, which will then result in <see cref="DataReceived"/> being fired later at some
    /// point (although it may not be fired with the same offset and count, and may get fired multiple times from the same request)
    /// </summary>
    /// <param name="offset">The offset within the document</param>
    /// <param name="count">The amount of bytes to read</param>
    void RequestDataLater(ulong offset, ulong count);
    
    /// <summary>
    /// Invoked when the user types or pastes binary data into the hex editor. This method is only invoked
    /// when <see cref="CanWriteBackInto"/> is true.
    /// At least, this method should write into an internal buffer such that immediate reads from
    /// <see cref="ReadAvailableData"/> will return the data just written.
    /// <para>
    /// What this method actually does is completely up to the implementation. E.g., a file-based binary source
    /// may wish to write the data back to the underlying file immediately, or maybe do it later, or perhaps
    /// just write it to the internal cache and wait for the user to execute a save command to write data back
    /// </para>
    /// </summary>
    /// <param name="offset">The offset where the data was written</param>
    /// <param name="data">The data that was written</param>
    void OnUserInput(ulong offset, byte[] data);

    int ReadAvailableBytesOrRequest(ulong offset, Span<byte> span, BitRangeUnion union) {
        int count = this.ReadAvailableData(offset, span, union);
        if (count < span.Length) {
            ulong newOffset = offset + (ulong) count;
            int newCount = span.Length - count;
            if (newOffset >= offset && newCount > 0) {
                this.RequestDataLater(newOffset, (ulong) newCount);
            }
        }

        return count;
    }
}