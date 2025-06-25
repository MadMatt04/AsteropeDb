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
/// Registry for database types and their associated behaviors.
/// Provides type registration, lookup, and management capabilities.
/// </summary>
public interface IDbTypeRegistry
{
    /// <summary>
    /// Registers a database type with the registry.
    /// </summary>
    /// <typeparam name="T">The C# type to register.</typeparam>
    /// <param name="dbType">The database type implementation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbType"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the type is already registered.</exception>
    void RegisterType<T>(IDbType<T> dbType);

    /// <summary>
    /// Gets the registered database type for the specified C# type.
    /// </summary>
    /// <typeparam name="T">The C# type to get the database type for.</typeparam>
    /// <returns>The registered database type implementation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the type is not registered.</exception>
    IDbType<T> GetType<T>();

    /// <summary>
    /// Determines whether the specified C# type is registered.
    /// </summary>
    /// <typeparam name="T">The C# type to check.</typeparam>
    /// <returns><see langword="true"/> if the type is registered; otherwise, <see langword="false"/>.</returns>
    bool IsRegistered<T>();

    /// <summary>
    /// Attempts to get the registered database type for the specified C# type.
    /// </summary>
    /// <typeparam name="T">The C# type to get the database type for.</typeparam>
    /// <param name="dbType">When this method returns, contains the database type if found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the type was found; otherwise, <see langword="false"/>.</returns>
    bool TryGetType<T>(out IDbType<T>? dbType);
}
