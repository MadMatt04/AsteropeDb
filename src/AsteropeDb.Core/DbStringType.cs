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

namespace AsteropeDb.Core;

/// <summary>
/// Database type implementation for UTF-8 strings.
/// Provides lexicographic comparison with culture-invariant ordering.
/// </summary>
public sealed class DbStringType : IDbType<string>
{
    /// <summary>
    /// Gets the singleton instance of the DbStringType.
    /// </summary>
    public static DbStringType Instance { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DbStringType"/> class.
    /// </summary>
    private DbStringType()
    {
    }

    /// <inheritdoc />
    public string TypeName => "string";

    /// <inheritdoc />
    public int Compare(string left, string right)
    {
        return string.CompareOrdinal(left, right);
    }

    /// <inheritdoc />
    public bool IsValid(string value)
    {
        return value != null;
    }

    /// <inheritdoc />
    public ReadOnlySpan<byte> GetIndexKey(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value), "String value cannot be null for index key generation.");
        }

        return Encoding.UTF8.GetBytes(value);
    }

    /// <inheritdoc />
    public int GetHashCode(string value)
    {
        return value?.GetHashCode() ?? 0;
    }
}
