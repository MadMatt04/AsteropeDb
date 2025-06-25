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

namespace AsteropeDb.Core;

/// <summary>
/// Database type implementation for boolean values.
/// Uses standard boolean comparison where false sorts before true.
/// </summary>
public sealed class DbBooleanType : IDbType<bool>
{
    /// <summary>
    /// Gets the singleton instance of the DbBooleanType.
    /// </summary>
    public static DbBooleanType Instance { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DbBooleanType"/> class.
    /// </summary>
    private DbBooleanType()
    {
    }

    /// <inheritdoc />
    public string TypeName => "boolean";

    /// <inheritdoc />
    public int Compare(bool left, bool right)
    {
        return left.CompareTo(right);
    }

    /// <inheritdoc />
    public bool IsValid(bool value)
    {
        return true;
    }

    /// <inheritdoc />
    public ReadOnlySpan<byte> GetIndexKey(bool value)
    {
        byte[] buffer = new byte[1];
        buffer[0] = value ? (byte)1 : (byte)0;
        return buffer;
    }

    /// <inheritdoc />
    public int GetHashCode(bool value)
    {
        return value.GetHashCode();
    }
}
