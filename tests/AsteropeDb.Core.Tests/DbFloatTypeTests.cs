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
using AsteropeDb.Core;
using Xunit;

namespace AsteropeDb.Core.Tests;

/// <summary>
/// Unit tests for <see cref="DbFloatType"/>.
/// </summary>
public class DbFloatTypeTests
{
    private readonly DbFloatType type = DbFloatType.Instance;

    [Fact]
    public void Instance_ShouldReturnSameInstance()
    {
        DbFloatType instance1 = DbFloatType.Instance;
        DbFloatType instance2 = DbFloatType.Instance;
        
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void TypeName_ShouldReturnFloat()
    {
        Assert.Equal("float", type.TypeName);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(42.5)]
    [InlineData(-42.5)]
    [InlineData(double.MinValue)]
    [InlineData(double.MaxValue)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void IsValid_WithValidValues_ShouldReturnTrue(double value)
    {
        Assert.True(type.IsValid(value));
    }

    [Fact]
    public void IsValid_WithNaN_ShouldReturnFalse()
    {
        Assert.False(type.IsValid(double.NaN));
    }

    [Theory]
    [InlineData(-10.5, 5.5, -1)]
    [InlineData(5.5, -10.5, 1)]
    [InlineData(42.0, 42.0, 0)]
    [InlineData(double.NegativeInfinity, double.PositiveInfinity, -1)]
    [InlineData(double.PositiveInfinity, double.NegativeInfinity, 1)]
    public void Compare_ShouldReturnCorrectComparison(double left, double right, int expected)
    {
        int result = type.Compare(left, right);
        Assert.Equal(Math.Sign(expected), Math.Sign(result));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(42.5)]
    [InlineData(-42.5)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void GetIndexKey_WithValidValues_ShouldReturnEightBytes(double value)
    {
        ReadOnlySpan<byte> key = type.GetIndexKey(value);
        
        Assert.Equal(8, key.Length);
    }

    [Fact]
    public void GetIndexKey_WithNaN_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => type.GetIndexKey(double.NaN));
    }

    [Theory]
    [InlineData(42.5)]
    [InlineData(-42.5)]
    [InlineData(0.0)]
    public void GetHashCode_ShouldMatchSystemDoubleHashCode(double value)
    {
        int typeHashCode = type.GetHashCode(value);
        int systemHashCode = value.GetHashCode();
        
        Assert.Equal(systemHashCode, typeHashCode);
    }

    [Fact]
    public void GetIndexKey_ShouldHandleSpecialValues()
    {
        // Test that special values don't throw exceptions
        ReadOnlySpan<byte> positiveInfinityKey = type.GetIndexKey(double.PositiveInfinity);
        ReadOnlySpan<byte> negativeInfinityKey = type.GetIndexKey(double.NegativeInfinity);
        ReadOnlySpan<byte> zeroKey = type.GetIndexKey(0.0);
        ReadOnlySpan<byte> negativeZeroKey = type.GetIndexKey(-0.0);
        
        Assert.Equal(8, positiveInfinityKey.Length);
        Assert.Equal(8, negativeInfinityKey.Length);
        Assert.Equal(8, zeroKey.Length);
        Assert.Equal(8, negativeZeroKey.Length);
    }

    [Fact]
    public void GetIndexKey_ShouldProduceOrderedKeysForPositiveValues()
    {
        double[] values = { 0.0, 1.0, 2.5, 100.0, double.PositiveInfinity };
        byte[][] keys = new byte[values.Length][];
        
        for (int i = 0; i < values.Length; i++)
        {
            ReadOnlySpan<byte> key = type.GetIndexKey(values[i]);
            keys[i] = key.ToArray();
        }
        
        // For positive values, lexicographic byte comparison should match numeric ordering
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
            if (comparison != 0) return comparison;
        }
        return left.Length.CompareTo(right.Length);
    }
}