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
using System.IO;

namespace AsteropeDb.Serialization;

/// <summary>
/// Pluggable serialization interface for converting values to and from byte arrays.
/// Supports multiple serialization formats through extensible plugin architecture.
/// </summary>
public interface IDbSerializer
{
    /// <summary>
    /// Gets the unique identifier for this serializer.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Serializes a value to a byte array.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>A byte array containing the serialized value.</returns>
    /// <exception cref="NotSupportedException">Thrown when the serializer cannot handle the specified type.</exception>
    byte[] Serialize<T>(T value);

    /// <summary>
    /// Deserializes a value from a byte array.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="data">The byte array containing the serialized data.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="NotSupportedException">Thrown when the serializer cannot handle the specified type.</exception>
    /// <exception cref="InvalidDataException">Thrown when the data cannot be deserialized.</exception>
    T Deserialize<T>(byte[] data);

    /// <summary>
    /// Determines whether this serializer can handle the specified type.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns><see langword="true"/> if the serializer can handle the type; otherwise, <see langword="false"/>.</returns>
    bool CanSerialize<T>();
}
