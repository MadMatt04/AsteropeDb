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
/// Database type implementation for 64-bit signed integers.
/// Provides numeric comparison and efficient binary indexing.
/// </summary>
public sealed class DbIntegerType : IDbType<long>
{
    /// <summary>
    /// Gets the singleton instance of the DbIntegerType.
    /// </summary>
    public static DbIntegerType Instance { get; } = new();
    
    /// <summary>
    /// Prevents external instantiation. Use <see cref="Instance"/> instead.
    /// </summary>
    private DbIntegerType() { }
    
    /// <inheritdoc />
    public string TypeName => "integer";
    
    /// <inheritdoc />
    public int Compare(long left, long right)
    {
        return left.CompareTo(right);
    }
    
    /// <inheritdoc />
    public bool IsValid(long value)
    {
        return true;
    }
    
    /// <inheritdoc />
    public ReadOnlySpan<byte> GetIndexKey(long value)
    {
        byte[] buffer = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        return buffer;
    }
    
    /// <inheritdoc />
    public int GetHashCode(long value)
    {
        return value.GetHashCode();
    }
}