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
/// Defines database type behavior for indexing, comparison, and validation.
/// Serialization is handled separately by the pluggable serialization system.
/// </summary>
/// <typeparam name="T">The C# type that corresponds to this database type.</typeparam>
public interface IDbType<in T>
{
    /// <summary>
    /// Gets the unique type identifier for this database type.
    /// </summary>
    string TypeName { get; }
    
    /// <summary>
    /// Compares two values for B+ tree indexing and sorting.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>
    /// A signed integer that indicates the relative values:
    /// Less than zero if <paramref name="left"/> is less than <paramref name="right"/>.
    /// Zero if <paramref name="left"/> equals <paramref name="right"/>.
    /// Greater than zero if <paramref name="left"/> is greater than <paramref name="right"/>.
    /// </returns>
    int Compare(T left, T right);
    
    /// <summary>
    /// Validates a value before storage operations.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <returns><see langword="true"/> if the value is valid; otherwise, <see langword="false"/>.</returns>
    bool IsValid(T value);
    
    /// <summary>
    /// Extracts an indexable key for B+ tree operations.
    /// This key is used for efficient range queries and ordering.
    /// </summary>
    /// <param name="value">The value to extract the index key from.</param>
    /// <returns>A byte span representing the index key.</returns>
    ReadOnlySpan<byte> GetIndexKey(T value);
    
    /// <summary>
    /// Gets the hash code for efficient lookups.
    /// </summary>
    /// <param name="value">The value to get the hash code for.</param>
    /// <returns>A hash code for the specified value.</returns>
    int GetHashCode(T value);
}
