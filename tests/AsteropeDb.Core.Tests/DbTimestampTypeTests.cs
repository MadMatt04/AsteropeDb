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
using AsteropeDb.Core;
using Xunit;

namespace AsteropeDb.Core.Tests;

/// <summary>
/// Unit tests for <see cref="DbTimestamp"/> and <see cref="DbTimestampType"/>.
/// </summary>
public class DbTimestampTypeTests
{
    private readonly DbTimestampType type = DbTimestampType.Instance;

    [Fact]
    public void DbTimestamp_Constructor_WithValidValues_ShouldSucceed()
    {
        DbTimestamp timestamp = new(123456789L, 50L);
        
        Assert.Equal(123456789L, timestamp.Ticks);
        Assert.Equal(50L, timestamp.Nanoseconds);
    }

    [Fact]
    public void DbTimestamp_Constructor_WithDefaultNanoseconds_ShouldSucceed()
    {
        DbTimestamp timestamp = new(123456789L);
        
        Assert.Equal(123456789L, timestamp.Ticks);
        Assert.Equal(0L, timestamp.Nanoseconds);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(100L)]
    public void DbTimestamp_Constructor_WithInvalidNanoseconds_ShouldThrow(long nanoseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DbTimestamp(0L, nanoseconds));
    }

    [Fact]
    public void DbTimestamp_FromDateTime_ShouldConvertCorrectly()
    {
        DateTime dateTime = new(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        DbTimestamp timestamp = DbTimestamp.FromDateTime(dateTime);
        
        long expectedTicks = dateTime.Ticks - DateTime.UnixEpoch.Ticks;
        Assert.Equal(expectedTicks, timestamp.Ticks);
        Assert.Equal(0L, timestamp.Nanoseconds);
    }

    [Fact]
    public void DbTimestamp_FromDateTime_WithLocalTime_ShouldConvertToUtc()
    {
        DateTime localTime = new(2023, 1, 1, 12, 0, 0, DateTimeKind.Local);
        DbTimestamp timestamp = DbTimestamp.FromDateTime(localTime);
        
        DateTime utcTime = localTime.ToUniversalTime();
        long expectedTicks = utcTime.Ticks - DateTime.UnixEpoch.Ticks;
        Assert.Equal(expectedTicks, timestamp.Ticks);
    }

    [Fact]
    public void DbTimestamp_FromDateTimeOffset_ShouldConvertCorrectly()
    {
        DateTimeOffset dateTimeOffset = new(2023, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));
        DbTimestamp timestamp = DbTimestamp.FromDateTimeOffset(dateTimeOffset);
        
        long expectedTicks = dateTimeOffset.UtcTicks - DateTime.UnixEpoch.Ticks;
        Assert.Equal(expectedTicks, timestamp.Ticks);
    }

    [Fact]
    public void DbTimestamp_ToDateTime_ShouldConvertCorrectly()
    {
        long ticks = 123456789L;
        DbTimestamp timestamp = new(ticks);
        DateTime dateTime = timestamp.ToDateTime();
        
        DateTime expected = new(DateTime.UnixEpoch.Ticks + ticks, DateTimeKind.Utc);
        Assert.Equal(expected, dateTime);
        Assert.Equal(DateTimeKind.Utc, dateTime.Kind);
    }

    [Fact]
    public void DbTimestamp_ToDateTimeOffset_ShouldConvertCorrectly()
    {
        long ticks = 123456789L;
        DbTimestamp timestamp = new(ticks);
        DateTimeOffset dateTimeOffset = timestamp.ToDateTimeOffset();
        
        DateTimeOffset expected = new(DateTime.UnixEpoch.Ticks + ticks, TimeSpan.Zero);
        Assert.Equal(expected, dateTimeOffset);
    }

    [Theory]
    [InlineData(100L, 50L, 200L, 30L, -1)]
    [InlineData(200L, 30L, 100L, 50L, 1)]
    [InlineData(100L, 50L, 100L, 50L, 0)]
    [InlineData(100L, 30L, 100L, 50L, -1)]
    [InlineData(100L, 50L, 100L, 30L, 1)]
    public void DbTimestamp_CompareTo_ShouldReturnCorrectComparison(long ticks1, long nanos1, long ticks2, long nanos2, int expected)
    {
        DbTimestamp timestamp1 = new(ticks1, nanos1);
        DbTimestamp timestamp2 = new(ticks2, nanos2);
        
        int result = timestamp1.CompareTo(timestamp2);
        Assert.Equal(Math.Sign(expected), Math.Sign(result));
    }

    [Fact]
    public void DbTimestamp_Equals_ShouldWorkCorrectly()
    {
        DbTimestamp timestamp1 = new(123L, 45L);
        DbTimestamp timestamp2 = new(123L, 45L);
        DbTimestamp timestamp3 = new(456L, 78L);
        
        Assert.True(timestamp1.Equals(timestamp2));
        Assert.True(timestamp1 == timestamp2);
        Assert.False(timestamp1.Equals(timestamp3));
        Assert.False(timestamp1 == timestamp3);
        Assert.True(timestamp1 != timestamp3);
    }

    [Fact]
    public void DbTimestamp_GetHashCode_ShouldBeDeterministic()
    {
        DbTimestamp timestamp1 = new(123L, 45L);
        DbTimestamp timestamp2 = new(123L, 45L);
        
        Assert.Equal(timestamp1.GetHashCode(), timestamp2.GetHashCode());
    }

    [Fact]
    public void DbTimestamp_ToString_ShouldFormatCorrectly()
    {
        DateTime baseTime = new(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        DbTimestamp timestamp = DbTimestamp.FromDateTime(baseTime);
        
        string result = timestamp.ToString();
        Assert.Contains("2023-01-01T12:00:00", result);
        Assert.EndsWith("Z", result);
    }

    [Fact]
    public void DbTimestamp_ToString_WithNanoseconds_ShouldIncludeNanoseconds()
    {
        DateTime baseTime = new(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        long baseTicks = baseTime.Ticks - DateTime.UnixEpoch.Ticks;
        DbTimestamp timestamp = new(baseTicks, 50L);
        
        string result = timestamp.ToString();
        Assert.Contains("50Z", result);
    }

    [Fact]
    public void DbTimestampType_Instance_ShouldReturnSameInstance()
    {
        DbTimestampType instance1 = DbTimestampType.Instance;
        DbTimestampType instance2 = DbTimestampType.Instance;
        
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void TypeName_ShouldReturnTimestamp()
    {
        Assert.Equal("timestamp", type.TypeName);
    }

    [Fact]
    public void IsValid_ShouldAlwaysReturnTrue()
    {
        DbTimestamp timestamp = new(123L, 45L);
        Assert.True(type.IsValid(timestamp));
    }

    [Fact]
    public void Compare_ShouldMatchDbTimestampCompareTo()
    {
        DbTimestamp timestamp1 = new(100L, 30L);
        DbTimestamp timestamp2 = new(200L, 50L);
        
        int typeResult = type.Compare(timestamp1, timestamp2);
        int timestampResult = timestamp1.CompareTo(timestamp2);
        
        Assert.Equal(timestampResult, typeResult);
    }

    [Fact]
    public void GetIndexKey_ShouldReturnSixteenBytes()
    {
        DbTimestamp timestamp = new(123L, 45L);
        ReadOnlySpan<byte> key = type.GetIndexKey(timestamp);
        
        Assert.Equal(16, key.Length);
    }

    [Fact]
    public void GetIndexKey_ShouldBeReversible()
    {
        DbTimestamp timestamp = new(123456789L, 50L);
        ReadOnlySpan<byte> key = type.GetIndexKey(timestamp);
        
        long reconstructedTicks = BinaryPrimitives.ReadInt64BigEndian(key[..8]);
        long reconstructedNanos = BinaryPrimitives.ReadInt64BigEndian(key[8..]);
        
        Assert.Equal(timestamp.Ticks, reconstructedTicks);
        Assert.Equal(timestamp.Nanoseconds, reconstructedNanos);
    }

    [Fact]
    public void GetHashCode_ShouldMatchDbTimestampHashCode()
    {
        DbTimestamp timestamp = new(123L, 45L);
        
        int typeHashCode = type.GetHashCode(timestamp);
        int timestampHashCode = timestamp.GetHashCode();
        
        Assert.Equal(timestampHashCode, typeHashCode);
    }

    [Fact]
    public void GetIndexKey_ShouldProduceOrderedKeys()
    {
        DbTimestamp[] timestamps = {
            new(100L, 0L),
            new(100L, 50L),
            new(200L, 0L),
            new(200L, 50L)
        };
        
        byte[][] keys = new byte[timestamps.Length][];
        
        for (int i = 0; i < timestamps.Length; i++)
        {
            ReadOnlySpan<byte> key = type.GetIndexKey(timestamps[i]);
            keys[i] = key.ToArray();
        }
        
        for (int i = 0; i < keys.Length - 1; i++)
        {
            int comparison = CompareByteArrays(keys[i], keys[i + 1]);
            Assert.True(comparison <= 0, $"Key for timestamp {i} should be <= key for timestamp {i + 1}");
        }
    }

    private static int CompareByteArrays(byte[] left, byte[] right)
    {
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            int comparison = left[i].CompareTo(right[i]);
            if (comparison != 0) return comparison;
        }
        return left.Length.CompareTo(right.Length);
    }
}