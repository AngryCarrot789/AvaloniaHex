namespace AvaloniaHex.Base.Document;

public delegate void BinarySourceDataReceivedEventHandler(IBinarySource source, ulong offset, ulong count);

public interface IBinarySource {
    /// <summary>
    /// Gets the range of binary data readable by this binary source.
    /// </summary>
    BitRange ApplicableRange { get; }

    /// <summary>
    /// An event fired when requested data is received
    /// </summary>
    event BinarySourceDataReceivedEventHandler DataReceived;

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
    /// Writes bytes back into the source. 
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="data"></param>
    void WriteBytesForUserInput(ulong offset, byte[] data);

    void ReadAvailableBytesOrRequest(ulong offset, Span<byte> span, BitRangeUnion union) {
        int count = this.ReadAvailableData(offset, span, union);
        if (count < span.Length) {
            ulong newOffset = offset + (ulong) count;
            int newCount = span.Length - count;
            if (newOffset >= offset && newCount > 0) {
                this.RequestDataLater(newOffset, (ulong) newCount);
            }
        }
    }
}