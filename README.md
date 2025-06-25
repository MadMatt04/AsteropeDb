# AsteropeDb

A high-performance .NET native database optimized for storing large binary files with queryable metadata.

## 🚀 Overview

AsteropeDb is designed for applications that need to efficiently store and query large binary data alongside structured metadata. Whether you're building medical imaging systems, scientific research platforms, or media management applications, AsteropeDb provides the performance and simplicity you need.

## ✨ Key Features

- **Large Binary Storage**: Handle files up to 2^63 bytes with streaming I/O
- **Queryable Metadata**: LINQ-based queries on MessagePack-serialized metadata
- **Dual Deployment**: Embedded and networked modes with consistent APIs
- **High Performance**: Memory-mapped files + B+ tree indexing for optimal performance
- **ACID Transactions**: Full transactional support with configurable isolation levels
- **Modular Architecture**: Distributed as separate NuGet packages for flexibility

## 🎯 Use Cases

- **Healthcare**: Store DICOM images, radiotherapy data with patient metadata
- **Scientific Research**: Manage large experimental datasets with searchable parameters
- **Media Management**: Archive images, audio, video with rich metadata
- **Backup Systems**: Efficient storage with indexable backup information
- **Asset Pipelines**: Game development and content creation workflows

## 🏗️ Architecture

AsteropeDb uses a hybrid storage approach:

- **Memory-Mapped Files**: Efficient storage and streaming of large binary data
- **B+ Tree Indexing**: Fast metadata queries and key-value lookups
- **Custom HTTP/2 Protocol**: Optimized for large file transfers in networked mode
- **1:1 Key-Value Mapping**: Each key stores exactly one binary file with metadata

## 📦 Packages

| Package | Description |
|---------|-------------|
| `AsteropeDb.Core` | Core types and interfaces |
| `AsteropeDb.Storage` | Storage engine implementation |
| `AsteropeDb.Serialization` | MessagePack integration |
| `AsteropeDb.Network` | HTTP/2 protocol for networked mode |

## 🚀 Quick Start

### Embedded Mode

```csharp
using AsteropeDb;

// Open database
using var db = new AsteropeDatabase("./mydata");

// Store binary data with metadata
var metadata = new { PatientId = "12345", StudyDate = DateTime.Now };
await db.StoreAsync("scan-001", binaryData, metadata);

// Query by metadata
var results = await db.QueryAsync<MyMetadata>(m => m.PatientId == "12345");

// Retrieve binary data
var (data, meta) = await db.GetAsync("scan-001");
```

### Networked Mode

```csharp
using AsteropeDb.Network;

// Connect to server
using var client = new AsteropeClient("https://localhost:5001");

// Same API as embedded mode
await client.StoreAsync("scan-001", binaryData, metadata);
var results = await client.QueryAsync<MyMetadata>(m => m.StudyDate > DateTime.Today);
```

## 🛠️ Development Status

**Current Status**: 🏗️ **In Development**

This project is currently in active development. The MVP is planned for release in Q4 2025.

### Roadmap

- **Phase 1-2** (Weeks 1-4): Project foundation and architecture
- **Phase 3-4** (Weeks 5-16): Core type system and storage engine
- **Phase 5-7** (Weeks 17-30): Serialization, transactions, and embedded API
- **Phase 8-9** (Weeks 31-42): Network protocol and networked API
- **Phase 10-11** (Weeks 43-52): Testing, optimization, and MVP release
- **Phase 12** (Future): Clustering and horizontal scaling

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

### Development Setup

*Note: Development setup instructions will be added as the project structure is established.*

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🎯 Performance Goals

- Efficient handling of gigabyte-scale binary files
- Streaming I/O with minimal memory footprint
- Concurrent access with ACID guarantees
- Linear performance scaling for future clustering

## 🔮 Future Plans

- Horizontal clustering and replication
- Advanced query capabilities
- Administration and monitoring tools
- Additional serialization format plugins

---

**Note**: This project is under active development. APIs and features may change before the first stable release.