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
/// Database type implementation for IEEE 754 double precision floating point numbers.
/// Provides proper handling of NaN, infinity, and negative zero values.
/// </summary>
public sealed class DbFloatType : IDbType<double>
{
    /// <summary>
    /// Gets the singleton instance of the DbFloatType.
    /// </summary>
    public static DbFloatType Instance { get; } = new();
    
    /// <summary>
    /// Prevents external instantiation. Use <see cref="Instance"/> instead.
    /// </summary>
    private DbFloatType() { }
    
    /// <inheritdoc />
    public string TypeName => "float";
    
    /// <inheritdoc />
    public int Compare(double left, double right)
    {
        return left.CompareTo(right);
    }
    
    /// <inheritdoc />
    public bool IsValid(double value)
    {
        return !double.IsNaN(value);
    }
    
    /// <inheritdoc />
    public ReadOnlySpan<byte> GetIndexKey(double value)
    {
        if (double.IsNaN(value))
        {
            throw new ArgumentException("NaN values cannot be used as index keys.", nameof(value));
        }
        
        byte[] buffer = new byte[8];
        long bits = BitConverter.DoubleToInt64Bits(value);
        
        if (bits >= 0)
        {
            BinaryPrimitives.WriteInt64BigEndian(buffer, bits);
        }
        else
        {
            BinaryPrimitives.WriteInt64BigEndian(buffer, ~bits);
        }
        
        return buffer;
    }
    
    /// <inheritdoc />
    public int GetHashCode(double value)
    {
        return value.GetHashCode();
    }
}