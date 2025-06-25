# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AsteropeDb is a .NET native database optimized for storing large binary files (up to 2^63 bytes) with queryable metadata. The project targets healthcare, scientific research, media management, and enterprise data storage applications.

## Architecture

### Modular Design
The system follows a highly modular architecture distributed as separate NuGet packages:
- `AsteropeDb.Core`: Core types, interfaces, and abstractions
- `AsteropeDb.Storage`: Storage engine implementation
- `AsteropeDb.Serialization`: MessagePack integration and plugin architecture
- `AsteropeDb.Network`: Custom HTTP/2 protocol for networked mode

### Storage Engine
- **Hybrid Approach**: Memory-mapped files for binary data, B+ tree indexing for metadata
- **Data Model**: 1:1 key-to-file mapping (each key stores exactly one binary file + metadata)
- **Performance**: Streaming I/O, LRU caching, optional compression, designed for 2^63 byte files

### Operation Modes
- **Embedded Mode**: Native .NET API with LINQ expression queries
- **Networked Mode**: Custom HTTP/2 protocol optimized for large binary streaming
- **Future**: Clustering and horizontal scaling support (post-MVP)

## Technical Specifications

### Core Technologies
- .NET 9 runtime requirement
- MessagePack for serialization (extensible to other formats)
- ASP.NET Core for network services
- Custom HTTP/2 protocol for binary streaming without message size limitations

### Transaction Model
- ACID compliance with WAL (Write-Ahead Logging)
- Read Committed and Repeatable Read isolation levels
- Key-level atomic operations
- No cross-key joins or complex relational operations

### Query System
- LINQ expressions for embedded mode metadata queries
- Simple field-based queries (no joins)
- B+ tree indexes on MessagePack-encoded fields
- Network mode query API (TBD)

## Development Timeline

The project follows a 52-week development plan:
- **Weeks 1-8**: Foundation and architecture setup
- **Weeks 9-26**: Core storage engine and transaction system
- **Weeks 27-42**: API development (embedded + networked modes)
- **Weeks 43-52**: Testing, optimization, and MVP release
- **Post-MVP**: Clustering implementation (6+ months)

## Key Design Principles

- Performance over features for large binary data
- Streaming operations to avoid memory pressure
- Modular architecture for NuGet distribution
- Simple APIs that scale from embedded to networked deployment
- .NET-native implementation with minimal external dependencies
- Future-ready architecture for horizontal scaling

## Code Style
- Use 4 spaces for indentation
- PascalCase for classes/methods/constants/properties, camelCase for variables and fields
- Do not prefix private fields with _
- Braces on new lines for classes/methods
- File-scoped namespaces preferred
- Disallow implicit usings
- Avoid using var, specify type explicitly where possible
- When using the new operator, prefer this pattern MyClass c = new();
- Extensively comment all public, internal and protected classes and members. Use <see cref>, <see langword> and <paramref> liberally
- Private members do not need to be commented, unless the code is unclear or complicated
- End all comment sentences with a period .
- Put the MIT license header at the top of every file.
