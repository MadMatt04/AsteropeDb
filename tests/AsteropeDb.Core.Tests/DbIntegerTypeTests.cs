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
/// Unit tests for <see cref="DbIntegerType"/>.
/// </summary>
public class DbIntegerTypeTests
{
    private readonly DbIntegerType type = DbIntegerType.Instance;

    [Fact]
    public void Instance_ShouldReturnSameInstance()
    {
        DbIntegerType instance1 = DbIntegerType.Instance;
        DbIntegerType instance2 = DbIntegerType.Instance;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void TypeName_ShouldReturnInteger()
    {
        Assert.Equal("integer", type.TypeName);
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(long.MaxValue)]
    public void IsValid_ShouldAlwaysReturnTrue(long value)
    {
        Assert.True(type.IsValid(value));
    }

    [Theory]
    [InlineData(-10L, 5L, -1)]
    [InlineData(5L, -10L, 1)]
    [InlineData(42L, 42L, 0)]
    [InlineData(long.MinValue, long.MaxValue, -1)]
    [InlineData(long.MaxValue, long.MinValue, 1)]
    public void Compare_ShouldReturnCorrectComparison(long left, long right, int expected)
    {
        int result = type.Compare(left, right);
        Assert.Equal(Math.Sign(expected), Math.Sign(result));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(42L)]
    [InlineData(-42L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void GetIndexKey_ShouldReturnEightBytes(long value)
    {
        ReadOnlySpan<byte> key = type.GetIndexKey(value);

        Assert.Equal(8, key.Length);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(42L)]
    [InlineData(-42L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void GetIndexKey_ShouldBeReversible(long value)
    {
        ReadOnlySpan<byte> key = type.GetIndexKey(value);
        long transformed = BinaryPrimitives.ReadInt64BigEndian(key);
        long reconstructed = transformed ^ long.MinValue;

        Assert.Equal(value, reconstructed);
    }

    [Theory]
    [InlineData(42L)]
    [InlineData(-42L)]
    [InlineData(0L)]
    public void GetHashCode_ShouldMatchSystemLongHashCode(long value)
    {
        int typeHashCode = type.GetHashCode(value);
        int systemHashCode = value.GetHashCode();

        Assert.Equal(systemHashCode, typeHashCode);
    }

    [Fact]
    public void GetIndexKey_ShouldProduceOrderedKeys()
    {
        long[] values = { -100L, -1L, 0L, 1L, 100L };
        byte[][] keys = new byte[values.Length][];

        for (int i = 0; i < values.Length; i++)
        {
            ReadOnlySpan<byte> key = type.GetIndexKey(values[i]);
            keys[i] = key.ToArray();
        }

        // Verify that keys are in ascending order using big-endian comparison
        for (int i = 0; i < keys.Length - 1; i++)
        {
            int comparison = CompareByteArrays(keys[i], keys[i + 1]);
            Assert.True(comparison <= 0, $"Key for {values[i]} should be <= key for {values[i + 1]}");
        }
    }

    private static int CompareByteArrays(byte[] left, byte[] right)
    {
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            int comparison = left[i].CompareTo(right[i]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Length.CompareTo(right.Length);
    }
}
