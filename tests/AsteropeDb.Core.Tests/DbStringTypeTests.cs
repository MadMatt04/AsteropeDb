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
using System.Text;
using AsteropeDb.Core;
using Xunit;

namespace AsteropeDb.Core.Tests;

/// <summary>
/// Unit tests for <see cref="DbStringType"/>.
/// </summary>
public class DbStringTypeTests
{
    private readonly DbStringType type = DbStringType.Instance;

    [Fact]
    public void Instance_ShouldReturnSameInstance()
    {
        DbStringType instance1 = DbStringType.Instance;
        DbStringType instance2 = DbStringType.Instance;
        
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void TypeName_ShouldReturnString()
    {
        Assert.Equal("string", type.TypeName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("Hello, World!")]
    [InlineData("Unicode: 你好 🌍")]
    public void IsValid_WithNonNullString_ShouldReturnTrue(string value)
    {
        Assert.True(type.IsValid(value));
    }

    [Fact]
    public void IsValid_WithNull_ShouldReturnFalse()
    {
        Assert.False(type.IsValid(null));
    }

    [Theory]
    [InlineData("apple", "banana", -1)]
    [InlineData("banana", "apple", 1)]
    [InlineData("hello", "hello", 0)]
    [InlineData("", "a", -1)]
    [InlineData("a", "", 1)]
    [InlineData("", "", 0)]
    public void Compare_ShouldReturnCorrectOrdinalComparison(string left, string right, int expected)
    {
        int result = type.Compare(left, right);
        Assert.Equal(Math.Sign(expected), Math.Sign(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("Unicode: 你好")]
    public void GetIndexKey_WithValidString_ShouldReturnUtf8Bytes(string value)
    {
        ReadOnlySpan<byte> key = type.GetIndexKey(value);
        byte[] expected = Encoding.UTF8.GetBytes(value);
        
        Assert.Equal(expected.Length, key.Length);
        Assert.True(key.SequenceEqual(expected));
    }

    [Fact]
    public void GetIndexKey_WithNull_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => type.GetIndexKey(null));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData("Unicode: 你好")]
    public void GetHashCode_WithNonNullString_ShouldMatchSystemStringHashCode(string value)
    {
        int typeHashCode = type.GetHashCode(value);
        int systemHashCode = value.GetHashCode();
        
        Assert.Equal(systemHashCode, typeHashCode);
    }

    [Fact]
    public void GetHashCode_WithNull_ShouldReturnZero()
    {
        int hashCode = type.GetHashCode(null);
        Assert.Equal(0, hashCode);
    }

    [Fact]
    public void GetIndexKey_ShouldProduceOrderedKeys()
    {
        string[] values = { "", "a", "apple", "banana", "z" };
        byte[][] keys = new byte[values.Length][];
        
        for (int i = 0; i < values.Length; i++)
        {
            ReadOnlySpan<byte> key = type.GetIndexKey(values[i]);
            keys[i] = key.ToArray();
        }
        
        // UTF-8 byte ordering should match lexicographic string ordering for ASCII
        for (int i = 0; i < keys.Length - 1; i++)
        {
            int comparison = CompareByteArrays(keys[i], keys[i + 1]);
            Assert.True(comparison <= 0, $"Key for '{values[i]}' should be <= key for '{values[i + 1]}'");
        }
    }

    [Fact]
    public void GetIndexKey_WithUnicodeStrings_ShouldHandleCorrectly()
    {
        string[] values = { "café", "cafés", "naïve" };
        
        foreach (string value in values)
        {
            ReadOnlySpan<byte> key = type.GetIndexKey(value);
            byte[] expected = Encoding.UTF8.GetBytes(value);
            
            Assert.True(key.SequenceEqual(expected));
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