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
/// Unit tests for <see cref="DbBooleanType"/>.
/// </summary>
public class DbBooleanTypeTests
{
    private readonly DbBooleanType type = DbBooleanType.Instance;

    [Fact]
    public void Instance_ShouldReturnSameInstance()
    {
        DbBooleanType instance1 = DbBooleanType.Instance;
        DbBooleanType instance2 = DbBooleanType.Instance;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void TypeName_ShouldReturnBoolean()
    {
        Assert.Equal("boolean", type.TypeName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsValid_ShouldAlwaysReturnTrue(bool value)
    {
        Assert.True(type.IsValid(value));
    }

    [Theory]
    [InlineData(false, true, -1)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 0)]
    [InlineData(false, false, 0)]
    public void Compare_ShouldReturnCorrectComparison(bool left, bool right, int expected)
    {
        int result = type.Compare(left, right);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetIndexKey_WithFalse_ShouldReturnZeroByte()
    {
        ReadOnlySpan<byte> key = type.GetIndexKey(false);

        Assert.Equal(1, key.Length);
        Assert.Equal(0, key[0]);
    }

    [Fact]
    public void GetIndexKey_WithTrue_ShouldReturnOneByte()
    {
        ReadOnlySpan<byte> key = type.GetIndexKey(true);

        Assert.Equal(1, key.Length);
        Assert.Equal(1, key[0]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetHashCode_ShouldMatchSystemBooleanHashCode(bool value)
    {
        int typeHashCode = type.GetHashCode(value);
        int systemHashCode = value.GetHashCode();

        Assert.Equal(systemHashCode, typeHashCode);
    }

    [Fact]
    public void GetIndexKey_ShouldProduceOrderedKeys()
    {
        ReadOnlySpan<byte> falseKey = type.GetIndexKey(false);
        ReadOnlySpan<byte> trueKey = type.GetIndexKey(true);

        // false should sort before true
        Assert.True(falseKey[0] < trueKey[0]);
    }
}
