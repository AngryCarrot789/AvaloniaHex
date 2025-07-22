using System.Diagnostics;

namespace AvaloniaHex.Base.Document;

/// <summary>
/// Represents a bit range within a binary document.
/// </summary>
[DebuggerDisplay("[{Start}, {End})")]
public readonly struct BitRange : IEquatable<BitRange> {
    /// <summary>
    /// Represents the empty range.
    /// </summary>
    public static readonly BitRange Empty = new();

    /// <summary>
    /// Gets the start location of the range.
    /// </summary>
    public BitLocation Start { get; }

    /// <summary>
    /// Gets the exclusive end location of the range.
    /// </summary>
    public BitLocation End { get; }

    /// <summary>
    /// Gets the total number of bytes that the range spans.
    /// </summary>
    public ulong ByteLength => this.End.ByteIndex - this.Start.ByteIndex;

    /// <summary>
    /// Gets the total number of bits that the range spans.
    /// </summary>
    public ulong BitLength {
        get {
            ulong result = this.ByteLength * 8;
            result -= (ulong) this.Start.BitIndex;
            result += (ulong) this.End.BitIndex;
            return result;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the range is empty or not.
    /// </summary>
    public bool IsEmpty => this.BitLength == 0;

    /// <summary>
    /// Creates a new bit range.
    /// </summary>
    /// <param name="start">The start byte offset.</param>
    /// <param name="end">The (exclusive) end byte offset.</param>
    public BitRange(ulong start, ulong end)
        : this(new BitLocation(start), new BitLocation(end)) {
    }

    /// <summary>
    /// Creates a new bit range.
    /// </summary>
    /// <param name="start">The start location.</param>
    /// <param name="end">The (exclusive) end location.</param>
    public BitRange(BitLocation start, BitLocation end) {
        if (end < start)
            throw new ArgumentException("End location is smaller than start location.");

        this.Start = start;
        this.End = end;
    }
    
    public static BitRange FromLength(ulong start, ulong length) => new BitRange(start, checked(start + length));

    /// <summary>
    /// Determines whether the provided location is within the range.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns><c>true</c> if the location is within the range, <c>false</c> otherwise.</returns>
    public bool Contains(BitLocation location) => location >= this.Start && location < this.End;

    /// <summary>
    /// Determines whether the provided range falls completely within the current range.
    /// </summary>
    /// <param name="other">The other range.</param>
    /// <returns><c>true</c> if the provided range is completely enclosed, <c>false</c> otherwise.</returns>
    public bool Contains(BitRange other) => this.Contains(other.Start) && this.Contains(other.End.PreviousOrZero());

    /// <summary>
    /// Determines whether the current range overlaps with the provided range.
    /// </summary>
    /// <param name="other">The other range.</param>
    /// <returns><c>true</c> if the range overlaps, <c>false</c> otherwise.</returns>
    public bool OverlapsWith(BitRange other)
        =>
            this.Contains(other.Start)
            || this.Contains(other.End.PreviousOrZero())
            || other.Contains(this.Start)
            || other.Contains(this.End.PreviousOrZero());

    /// <summary>
    /// Extends the range to the provided location.
    /// </summary>
    /// <param name="location">The location to extend to.</param>
    /// <returns>The extended range.</returns>
    public BitRange ExtendTo(BitLocation location) => new(this.Start.Min(location), this.End.Max(location));

    /// <summary>
    /// Restricts the range to the provided range.
    /// </summary>
    /// <param name="range">The range to restrict to.</param>
    /// <returns>The restricted range.</returns>
    public BitRange Clamp(BitRange range) {
        BitLocation start = this.Start.Max(range.Start);
        BitLocation end = this.End.Min(range.End);
        if (start > end)
            return Empty;

        return new BitRange(start, end);
    }

    /// <summary>
    /// Splits the range at the provided location.
    /// </summary>
    /// <param name="location">The location to split at.</param>
    /// <returns>The two resulting ranges.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Occurs when the provided location does not fall within the current range.
    /// </exception>
    public (BitRange, BitRange) Split(BitLocation location) {
        if (!this.Contains(location))
            throw new ArgumentOutOfRangeException(nameof(location));

        return (
            new BitRange(this.Start, location),
            new BitRange(location, this.End)
        );
    }

    /// <inheritdoc />
    public bool Equals(BitRange other) => this.Start.Equals(other.Start) && this.End.Equals(other.End);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BitRange other && this.Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() {
        unchecked {
            return (this.Start.GetHashCode() * 397) ^ this.End.GetHashCode();
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"[{this.Start}, {this.End})";

    /// <summary>
    /// Determines whether two ranges are equal.
    /// </summary>
    /// <param name="a">The first range.</param>
    /// <param name="b">The second range.</param>
    /// <returns><c>true</c> if the ranges are equal, <c>false</c> otherwise.</returns>
    public static bool operator ==(BitRange a, BitRange b) => a.Equals(b);

    /// <summary>
    /// Determines whether two ranges are not equal.
    /// </summary>
    /// <param name="a">The first range.</param>
    /// <param name="b">The second range.</param>
    /// <returns><c>true</c> if the ranges are not equal, <c>false</c> otherwise.</returns>
    public static bool operator !=(BitRange a, BitRange b) => !a.Equals(b);
}