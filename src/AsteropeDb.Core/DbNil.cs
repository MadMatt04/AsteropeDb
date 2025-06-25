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
/// Represents a database null value.
/// This is a unit type that has only one possible value: null.
/// </summary>
public readonly struct DbNil : IEquatable<DbNil>
{
    /// <summary>
    /// Gets the singleton null value.
    /// </summary>
    public static DbNil Value { get; } = new();

    /// <inheritdoc />
    public bool Equals(DbNil other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DbNil;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public override string ToString() => "nil";

    /// <summary>
    /// Determines whether two <see cref="DbNil"/> values are equal.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>Always <see langword="true"/> since all nil values are equal.</returns>
    public static bool operator ==(DbNil left, DbNil right) => true;

    /// <summary>
    /// Determines whether two <see cref="DbNil"/> values are not equal.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns>Always <see langword="false"/> since all nil values are equal.</returns>
    public static bool operator !=(DbNil left, DbNil right) => false;
}

/// <summary>
/// Database type implementation for null values.
/// All nil values are considered equal and sort before all other values.
/// </summary>
public sealed class DbNilType : IDbType<DbNil>
{
    /// <summary>
    /// Gets the singleton instance of the DbNilType.
    /// </summary>
    public static DbNilType Instance { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DbNilType"/> class.
    /// </summary>
    private DbNilType()
    {
    }

    /// <inheritdoc />
    public string TypeName => "nil";

    /// <inheritdoc />
    public int Compare(DbNil left, DbNil right)
    {
        return 0;
    }

    /// <inheritdoc />
    public bool IsValid(DbNil value)
    {
        return true;
    }

    /// <inheritdoc />
    public ReadOnlySpan<byte> GetIndexKey(DbNil value)
    {
        return ReadOnlySpan<byte>.Empty;
    }

    /// <inheritdoc />
    public int GetHashCode(DbNil value)
    {
        return 0;
    }
}
