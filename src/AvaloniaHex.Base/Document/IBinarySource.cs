namespace AvaloniaHex.Base.Document;

public delegate void BinarySourceDataReceivedEventHandler(IBinarySource source, ulong offset, ulong count);

public interface IBinarySource {
    /// <summary>
    /// Gets the ranges of binary data supported by this binary source
    /// </summary>
    IReadOnlyBitRangeUnion ValidRanges { get; }

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
    /// <returns>The amount of bytes written into buffer</returns>
    int ReadAvailableData(ulong offset, Span<byte> buffer);
    
    /// <summary>
    /// Requests to read data from this source, which will then result in <see cref="DataReceived"/> being fired later at some
    /// point (although it may not be fired with the same offset and count, and may get fired multiple times from the same request)
    /// </summary>
    /// <param name="offset">The offset within the document</param>
    /// <param name="count">The amount of bytes to read</param>
    void RequestDataLater(ulong offset, ulong count);
    
    /// <summary>
    /// Writes bytes back into the source. Does nothing if unsupported
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="data"></param>
    void WriteBytes(ulong offset, byte[] data);

    int ReadAvailableBytesOrRequest(ulong offset, Span<byte> span) {
        int count = this.ReadAvailableData(offset, span);
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