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
/// Unit tests for <see cref="DbNil"/> and <see cref="DbNilType"/>.
/// </summary>
public class DbNilTypeTests
{
    private readonly DbNilType type = DbNilType.Instance;

    [Fact]
    public void DbNil_Value_ShouldReturnSameInstance()
    {
        DbNil value1 = DbNil.Value;
        DbNil value2 = DbNil.Value;

        Assert.Equal(value1, value2);
    }

    [Fact]
    public void DbNil_Equals_ShouldAlwaysReturnTrue()
    {
        DbNil nil1 = DbNil.Value;
        DbNil nil2 = new();

        Assert.True(nil1.Equals(nil2));
        Assert.True(nil1 == nil2);
        Assert.False(nil1 != nil2);
    }

    [Fact]
    public void DbNil_GetHashCode_ShouldAlwaysReturnZero()
    {
        DbNil nil = DbNil.Value;
        Assert.Equal(0, nil.GetHashCode());
    }

    [Fact]
    public void DbNil_ToString_ShouldReturnNil()
    {
        DbNil nil = DbNil.Value;
        Assert.Equal("nil", nil.ToString());
    }

    [Fact]
    public void DbNilType_Instance_ShouldReturnSameInstance()
    {
        DbNilType instance1 = DbNilType.Instance;
        DbNilType instance2 = DbNilType.Instance;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void TypeName_ShouldReturnNil()
    {
        Assert.Equal("nil", type.TypeName);
    }

    [Fact]
    public void IsValid_ShouldAlwaysReturnTrue()
    {
        DbNil nil = DbNil.Value;
        Assert.True(type.IsValid(nil));
    }

    [Fact]
    public void Compare_ShouldAlwaysReturnZero()
    {
        DbNil nil1 = DbNil.Value;
        DbNil nil2 = new();

        int result = type.Compare(nil1, nil2);
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetIndexKey_ShouldReturnEmptySpan()
    {
        DbNil nil = DbNil.Value;
        ReadOnlySpan<byte> key = type.GetIndexKey(nil);

        Assert.True(key.IsEmpty);
        Assert.Equal(0, key.Length);
    }

    [Fact]
    public void GetHashCode_ShouldAlwaysReturnZero()
    {
        DbNil nil = DbNil.Value;
        int hashCode = type.GetHashCode(nil);

        Assert.Equal(0, hashCode);
    }

    [Fact]
    public void DbNil_EqualsObject_WithNonDbNil_ShouldReturnFalse()
    {
        DbNil nil = DbNil.Value;

        Assert.False(nil.Equals("not a DbNil"));
        Assert.False(nil.Equals(null));
        Assert.False(nil.Equals(42));
    }

    [Fact]
    public void DbNil_EqualsObject_WithDbNil_ShouldReturnTrue()
    {
        DbNil nil1 = DbNil.Value;
        DbNil nil2 = new();

        Assert.True(nil1.Equals((object)nil2));
    }
}
