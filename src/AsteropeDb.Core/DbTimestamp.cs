// MIT License
//
// Copyright (c) 2025 Matija Kejžar
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Buffers.Binary;

namespace AsteropeDb.Core;

/// <summary>
/// Represents a timezone-independent instant in time with nanosecond precision.
/// Based on UTC time to ensure consistent ordering across different timezones.
/// </summary>
public readonly struct DbTimestamp : IEquatable<DbTimestamp>, IComparable<DbTimestamp>
{
    private const long TicksPerSecond = 10_000_000L;
    private const long NanosecondsPerTick = 100L;
    private const long MaxNanoseconds = NanosecondsPerTick - 1L;

    /// <summary>
    /// Gets the number of ticks since Unix epoch (1970-01-01 00:00:00 UTC).
    /// </summary>
    public long Ticks { get; }

    /// <summary>
    /// Gets the nanosecond component (0-99).
    /// This provides sub-tick precision beyond .NET's standard DateTime resolution.
    /// </summary>
    public long Nanoseconds { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DbTimestamp"/> structure.
    /// </summary>
    /// <param name="ticks">The number of ticks since Unix epoch.</param>
    /// <param name="nanoseconds">The nanosecond component (0-99).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when nanoseconds is not in the range 0-99.</exception>
    public DbTimestamp(long ticks, long nanoseconds = 0)
    {
        if (nanoseconds < 0 || nanoseconds > MaxNanoseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(nanoseconds), nanoseconds, $"Nanoseconds must be between 0 and {MaxNanoseconds}.");
        }

        Ticks = ticks;
        Nanoseconds = nanoseconds;
    }

    /// <summary>
    /// Creates a <see cref="DbTimestamp"/> from a <see cref="DateTime"/> value.
    /// The DateTime is converted to UTC if it has timezone information.
    /// </summary>
    /// <param name="dateTime">The DateTime to convert.</param>
    /// <returns>A new DbTimestamp representing the same instant.</returns>
    public static DbTimestamp FromDateTime(DateTime dateTime)
    {
        DateTime utc = dateTime.Kind == DateTimeKind.Local ? dateTime.ToUniversalTime() : dateTime;
        long ticks = utc.Ticks - DateTime.UnixEpoch.Ticks;
        return new DbTimestamp(ticks);
    }

    /// <summary>
    /// Creates a <see cref="DbTimestamp"/> from a <see cref="DateTimeOffset"/> value.
    /// </summary>
    /// <param name="dateTimeOffset">The DateTimeOffset to convert.</param>
    /// <returns>A new DbTimestamp representing the same instant.</returns>
    public static DbTimestamp FromDateTimeOffset(DateTimeOffset dateTimeOffset)
    {
        long ticks = dateTimeOffset.UtcTicks - DateTime.UnixEpoch.Ticks;
        return new DbTimestamp(ticks);
    }

    /// <summary>
    /// Converts this timestamp to a <see cref="DateTime"/> in UTC.
    /// </summary>
    /// <returns>A DateTime representing this timestamp in UTC.</returns>
    public DateTime ToDateTime()
    {
        return new DateTime(DateTime.UnixEpoch.Ticks + Ticks, DateTimeKind.Utc);
    }

    /// <summary>
    /// Converts this timestamp to a <see cref="DateTimeOffset"/> in UTC.
    /// </summary>
    /// <returns>A DateTimeOffset representing this timestamp in UTC.</returns>
    public DateTimeOffset ToDateTimeOffset()
    {
        return new DateTimeOffset(DateTime.UnixEpoch.Ticks + Ticks, TimeSpan.Zero);
    }

    /// <inheritdoc />
    public int CompareTo(DbTimestamp other)
    {
        int ticksComparison = Ticks.CompareTo(other.Ticks);
        return ticksComparison != 0 ? ticksComparison : Nanoseconds.CompareTo(other.Nanoseconds);
    }

    /// <inheritdoc />
    public bool Equals(DbTimestamp other)
    {
        return Ticks == other.Ticks && Nanoseconds == other.Nanoseconds;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is DbTimestamp other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Ticks, Nanoseconds);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        DateTime dt = ToDateTime();
        return Nanoseconds == 0 ? dt.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ") : $"{dt:yyyy-MM-ddTHH:mm:ss.fffffff}{Nanoseconds:D2}Z";
    }

    /// <summary>
    /// Determines whether two <see cref="DbTimestamp"/> values are equal.
    /// </summary>
    /// <param name="left">The first timestamp to compare.</param>
    /// <param name="right">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if the timestamps are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(DbTimestamp left, DbTimestamp right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="DbTimestamp"/> values are not equal.
    /// </summary>
    /// <param name="left">The first timestamp to compare.</param>
    /// <param name="right">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if the timestamps are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(DbTimestamp left, DbTimestamp right) => !left.Equals(right);

    /// <summary>
    /// Determines whether one <see cref="DbTimestamp"/> is less than another.
    /// </summary>
    /// <param name="left">The first timestamp to compare.</param>
    /// <param name="right">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is less than <paramref name="right"/>; otherwise, <see langword="false"/>.</returns>
    public static bool operator <(DbTimestamp left, DbTimestamp right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether one <see cref="DbTimestamp"/> is greater than another.
    /// </summary>
    /// <param name="left">The first timestamp to compare.</param>
    /// <param name="right">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is greater than <paramref name="right"/>; otherwise, <see langword="false"/>.</returns>
    public static bool operator >(DbTimestamp left, DbTimestamp right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether one <see cref="DbTimestamp"/> is less than or equal to another.
    /// </summary>
    /// <param name="left">The first timestamp to compare.</param>
    /// <param name="right">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is less than or equal to <paramref name="right"/>; otherwise, <see langword="false"/>.</returns>
    public static bool operator <=(DbTimestamp left, DbTimestamp right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Determines whether one <see cref="DbTimestamp"/> is greater than or equal to another.
    /// </summary>
    /// <param name="left">The first timestamp to compare.</param>
    /// <param name="right">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is greater than or equal to <paramref name="right"/>; otherwise, <see langword="false"/>.</returns>
    public static bool operator >=(DbTimestamp left, DbTimestamp right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// Database type implementation for timezone-independent timestamps with nanosecond precision.
/// Provides chronological ordering suitable for time-series data and temporal queries.
/// </summary>
public sealed class DbTimestampType : IDbType<DbTimestamp>
{
    /// <summary>
    /// Gets the singleton instance of the DbTimestampType.
    /// </summary>
    public static DbTimestampType Instance { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DbTimestampType"/> class.
    /// </summary>
    private DbTimestampType()
    {
    }

    /// <inheritdoc />
    public string TypeName => "timestamp";

    /// <inheritdoc />
    public int Compare(DbTimestamp left, DbTimestamp right)
    {
        return left.CompareTo(right);
    }

    /// <inheritdoc />
    public bool IsValid(DbTimestamp value)
    {
        return true;
    }

    /// <inheritdoc />
    public ReadOnlySpan<byte> GetIndexKey(DbTimestamp value)
    {
        byte[] buffer = new byte[16];
        BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan()[..8], value.Ticks);
        BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan()[8..], value.Nanoseconds);
        return buffer;
    }

    /// <inheritdoc />
    public int GetHashCode(DbTimestamp value)
    {
        return value.GetHashCode();
    }
}
