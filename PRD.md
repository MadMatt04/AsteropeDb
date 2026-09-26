# Project Requirements Document (PRD)

**Document Version**: 2.0
**Last Updated**: 2026-09-25
**Status**: Draft
**Supersedes**: Version 1.0 (2025-06-25)

## Changes from Version 1.0

Version 1.0 described the features we wanted. It did not make most of the design decisions that determine whether a storage engine is correct. Version 2.0 makes those decisions, or lists them as open questions with criteria for deciding. The main changes:

- **Workload**: The engine must handle many small and medium objects as well as a few very large ones. A typical CT series is hundreds to thousands of files of about 0.5 MiB each, not one file of several GB.
- **Blob I/O**: Large binary data uses positional file I/O (`RandomAccess`), not memory-mapped files. The "zero-copy via memory-mapped files" claim is removed because it was incorrect.
- **Blob write path**: Blob contents never go through the WAL. They are written out of place, flushed, and then committed by a small metadata record.
- **Concurrency**: MVCC with a single writer. Read transactions see a snapshot and write transactions are serializable. This replaces the separate Read Committed and Repeatable Read levels.
- **Index keys**: Specified an order-preserving encoding with type tags that also works for composite keys. This fixes two ordering bugs in the existing code (see [Index Key Encoding](#index-key-encoding)).
- **Metadata**: One canonical metadata format (a MessagePack map with string keys). Other serializers are adapters at the API boundary.
- **Queries**: A serializable query AST is the core query model. LINQ is one way to build it, for a documented subset of expressions.
- **Network protocol**: Plain HTTP semantics for blobs and gRPC for control. No custom HTTP/2 frame types. The networked mode moves after the MVP.
- **Clustering**: Now a non-goal. Nothing in the MVP design is shaped by it.
- **New sections**: target scale, measurable success criteria, durability, space reclamation, backup and verification, on-disk format versioning, encryption and compliance, and competitive alternatives.
- **Plan**: The week-based plan is replaced by milestones with exit criteria. WAL and recovery are designed together with the page store. Crash testing starts at the first storage milestone.
- **Runtime**: .NET 10 (LTS).

## Project Overview

### Project Name
AsteropeDb

### Project Description
AsteropeDb is a .NET-native database for binary objects with queryable metadata. It stores each object's metadata and its binary content under one key. Metadata and content are committed together, and content is streamed without ever being fully loaded into memory. The first release is an embedded library. A networked server mode follows after the MVP.

### Project Vision
Give .NET applications one dependable store for "a file plus facts about the file". Today this usually takes a relational database plus a directory or object store, glued together by application code that has to keep the two consistent.

### Value Proposition
Our main competitor is the setup most teams already use: **SQLite or PostgreSQL for metadata, plus files on disk or an S3-compatible store for the binary content**. AsteropeDb has to be better than that setup on the following points:

1. **Blob and metadata are committed atomically.** You never get a metadata row without its file, a file without its metadata, or a half-written file that looks complete. Orphaned content is cleaned up after crashes.
2. **Streaming everywhere.** Reads and writes of any size have bounded memory use. Range reads let clients seek into video or large volumes.
3. **Indexed queries over metadata** with a .NET-native API. There is no SQL layer and no separate server process in embedded mode.
4. **Integrity is built in.** Everything stored is checksummed. A verification tool is included, and a consistent online backup of blobs and metadata together is supported.
5. **One package, no native dependencies**, and compatible with trimming and Native AOT.

If benchmarks show we can't beat the baseline on points 1, 2 and 4 at comparable performance, the project should change course.

### Target Audience
- Healthcare applications that handle medical images and radiotherapy data (DICOM objects, dose grids, structure sets)
- Scientific applications that manage experimental datasets
- Media and digital-asset management
- Backup and archival tools
- Asset pipelines in game development and content creation

## Workloads and Target Scale

### Workload Profiles

| Profile | Typical object size | Typical object count | Access pattern | Example |
|---------|--------------------|---------------------|----------------|---------|
| Many small objects | 1 KiB - 1 MiB | 10^6 - 10^8 | Mostly writes, lookups by metadata, bulk reads by group | DICOM CT/MR slices, thumbnails, textures |
| Medium objects | 1 MiB - 256 MiB | 10^4 - 10^6 | Balanced | RT dose grids, multi-frame DICOM, audio, models |
| Very large objects | 256 MiB - multi-TB | 10^1 - 10^4 | Sequential streaming, range reads | Video, raw instrument data, backup archives |

The engine has to perform well on all three profiles. The first profile shapes the page store and the commit path. The third shapes the blob store and streaming.

### Target Scale for Version 1

- **Objects per database**: up to 10^8.
- **Total content size**: tens of TB on a single volume. Placing blobs across several volumes is post-MVP.
- **Largest single blob**: 2^63 - 1 bytes. This is the theoretical limit set by `long` offsets. The largest tested size is 1 TiB.
- **Metadata document per object**: 64 KiB by default, configurable up to 1 MiB.
- **Key length**: up to 1 KiB after encoding.
- **Concurrency**: one writer at a time, many concurrent readers, and any number of concurrent blob uploads.

## Problem Statement and Alternatives

### Problem Statement
Applications that store large binary content with structured metadata usually combine a database with a filesystem or object store. The glue code is where things go wrong: files left behind after a failed transaction, metadata pointing to missing or truncated files, backups where the two halves don't match, and no end-to-end integrity check. General-purpose databases either load BLOBs into memory, run slowly with large BLOBs, or push the file-handling problem back to the application.

### Existing Alternatives

| Alternative | Strengths | Gap AsteropeDb addresses |
|-------------|-----------|---------------------------|
| SQLite/PostgreSQL + filesystem | Mature, well understood | No atomicity between the file and its row, integrity checks and backup consistency are left to the application |
| SQLite/PostgreSQL BLOBs | Atomic | Poor for large BLOBs, limited streaming, database files grow very large |
| S3/MinIO + a database | Scales out, cheap storage | Needs a server, no transactions spanning both, not embeddable |
| LiteDB FileStorage | Embedded, .NET-native | Designed for smaller data, limited indexing and concurrency |
| MongoDB GridFS, RavenDB attachments | Metadata and files in one product | Requires a server, not embeddable in-process |
| SQL Server FILESTREAM | Transactional file storage | Windows and SQL Server only, heavy to operate |
| Orthanc, dcm4chee (DICOM) | Complete DICOM servers | DICOM only, not a general embeddable store |

## Goals and Non-Goals

### Goals (MVP)
- An embedded, single-process, transactional store for objects (key, metadata, optional blob) organized in collections
- Streaming blob reads and writes with bounded memory, including range reads
- Secondary indexes on metadata fields (scalar, composite and multi-valued) and a query API over them
- Crash safety: acknowledged commits survive process crashes and power loss, and corruption is detected
- Online backup, verification and export

### Non-Goals (MVP)
- A networked server. This is planned post-MVP (see [Networked Mode](#11-networked-mode-post-mvp)).
- Joins, aggregations beyond count, or an SQL-like language
- Several writing processes on the same database
- Clustering, replication and horizontal scaling. These are a non-goal for the foreseeable future and are not a design constraint.
- GUI administration tools
- Serialization formats other than MessagePack for stored metadata

## Functional Requirements

### 1. Data Model

- A **database** is a directory that contains one AsteropeDb instance.
- A database contains named **collections**. Each collection has its own key space, indexes and settings, such as compression and the inline threshold.
- An **object** belongs to one collection and has:
  - a **key**: a byte string (with UTF-8 string convenience overloads) of up to 1 KiB, ordered by byte comparison.
  - a **metadata document**: a MessagePack map with string keys. It may be empty.
  - at most one **blob**: a byte sequence of any length.
  - **system fields** maintained by the engine: blob length, blob checksum, an optional content hash (SHA-256), created and modified timestamps, and the commit sequence number of the last change.
- **Blobs are immutable once committed.** Replacing a blob writes a new blob and atomically switches the object to it. You can update metadata without rewriting the blob.
- **Operations**: Get, Put (create or replace), Put-if (conditional on the commit sequence number, for optimistic concurrency), UpdateMetadata, Delete, Exists, and ordered key scans (full, range or prefix) in both directions.
- The first version has no append to a committed blob. Resumable uploads are handled by staged uploads (see [Blob Storage](#2-blob-storage)).

**Acceptance criteria**
- Put, UpdateMetadata and Delete on several keys in one transaction are all visible together or not at all.
- A key scan returns keys in byte order and supports continuation from any key.

### 2. Blob Storage

- **Streaming writes**: A blob is written through a `Stream` or `PipeWriter` into a **staged upload**. The upload is not visible to anyone until a transaction attaches it to an object and commits. Uploads do not hold the writer lock, so a 20-minute upload doesn't block other writers.
- **Streaming reads**: Blobs are read through a seekable read-only `Stream`, and through `PipeReader` or `ReadOnlySequence<byte>`-based APIs. Range reads with offset and length are supported.
- **Snapshot stability**: A reader that opened a blob keeps reading the version it opened, even if another transaction replaces or deletes the object in the meantime.
- **Two storage tiers**, chosen automatically by size:
  - **Inline**: blobs up to the collection's inline threshold (default 4 KiB) are stored in the page store next to the object record.
  - **External**: larger blobs are stored in the blob store (see [Blob Store Layout](#blob-store-layout)).
- **Integrity**: Each blob is checksummed in fixed-size chunks (default 1 MiB), and each chunk is checked when read. Computing a whole-blob SHA-256 content hash is optional, per collection.
- **Abandoned uploads**: Staged uploads that are never committed are removed when the upload is disposed, or at the next startup after a crash.

**Acceptance criteria**
- Writing or reading a blob of any size adds no more than the configured buffer size (default 1 MiB, up to 4 MiB) to managed heap use, and does not allocate on the large object heap.
- If the process is killed at any moment during an upload or commit, the object afterwards either keeps its previous state or has the complete new blob. Afterwards, no disk space leaks once recovery has finished.
- A range read of 1 MiB at any offset within a 100 GiB blob does not read the data before that offset.

### 3. Metadata

- Metadata is stored as **canonical MessagePack**: a map with string keys, holding nested maps and arrays. Indexes and queries use field paths such as `Study.Date` or `Tags[]`, and work without the CLR type that produced the data.
- **Typed API**: Documents are mapped to and from CLR types through the serializer layer. The default is MessagePack-CSharp with source-generated formatters, for AOT compatibility. Other formats (for example Protobuf or JSON) can be added as adapters that convert to and from the canonical map. They never define the stored format.
- **Supported value types** (see [Core Type System](#core-type-system)): nil, boolean, int64, float64, string, bytes, timestamp, date, and local date-time.
- **Size limit**: 64 KiB by default, configurable up to 1 MiB. Larger documents are rejected with a clear error message.
- MessagePack deserialization of untrusted input always uses the library's untrusted-data security mode. Typeless (type-embedding) serialization is never used.

### 4. Indexing

- **Primary index**: every collection is ordered by key.
- **Secondary indexes** are declared per collection, on one field path or several (composite). Each declaration has:
  - a value type, which is enforced. The mismatch policy is either `Reject` (the default: writes with a wrong type fail) or `Skip` (the object is left out of the index).
  - a missing-field policy: index the value as nil (the default), or skip the object.
  - uniqueness: unique or non-unique.
  - an optional **multi-valued** mode for arrays, which adds one index entry per element. This is needed for tags.
  - an optional **normalized string** mode (Unicode NFC normalization plus invariant case folding) for case-insensitive equality and prefix search, for example on patient names.
- **Online index build**: creating an index on a collection that already holds data runs in the background. The index is used by queries only when the build is finished. Writes continue during the build.
- Dropping an index frees its pages.
- Index entries are updated in the same transaction as the object they belong to.

**Acceptance criteria**
- Every index and value type satisfies the ordering property: `sign(Compare(a, b)) == sign(memcmp(encode(a), encode(b)))`. This is verified by property-based tests.
- After any crash, the `verify` tool reports that every index matches its collection exactly.

### 5. Querying

- The core of the query model is a **serializable query AST**. It is shared by the embedded API and by the networked mode later.
- **What a query can express**:
  - Predicates: equality, `<`, `<=`, `>`, `>=`, between, `IN`, string prefix, `exists` and `missing`, and `contains` on an array (through a multi-valued index), combined with `AND`, `OR` and `NOT`.
  - Order by indexed fields, with a limit, a continuation token for paging, and count.
  - Projection: return only system fields and metadata, without opening the blob.
- **Planner**: rules-based for the MVP. It picks the most selective usable index, including a prefix of a composite index. Predicates the index can't answer are evaluated against the decoded metadata. `Explain()` returns the chosen plan.
- **LINQ front-end**: Queries can be written as `Expression<Func<T, bool>>` predicates and OrderBy/Take chains, for a documented subset. Expressions outside that subset fail when the query is built, with a message naming the unsupported node. They are never silently evaluated on the client.
- **Not supported**: joins, grouping and aggregation other than count, and full-text search.
- Queries run against the snapshot of the transaction that issues them.

### 6. Transactions and Concurrency

- **Model**: multi-version concurrency control with **one writer at a time**.
  - **Read-only transactions** see a consistent snapshot as of the moment they start, and never block or get blocked.
  - **Read-write transactions** are serialized by the writer lock and see the latest committed state plus their own writes. The resulting isolation for writes is serializable.
- **Multi-key**: A transaction can change any number of objects across collections.
- **Blob uploads happen before the transaction**: stage the upload, then in a short transaction attach it to an object and commit.
- **Optimistic writes**: Put-if and Delete-if compare against the commit sequence number, for read-modify-write outside a long transaction.
- **Deferred reclamation**: A deleted or replaced blob, and old page versions, are reclaimed only after no live snapshot can still see them.
- **Long-lived snapshots**: A warning is logged when a snapshot is older than the configured age (default 10 minutes). Optionally the snapshot can be forced to end at a hard limit.
- **Timeouts and cancellation**: Waiting for the writer lock honors a `CancellationToken` and a configurable timeout.
- **Embedded mode is exclusive to one process**, enforced with a lock file. Opening the same database read-only from several processes is an open question.

**Acceptance criteria**
- Tests covering these anomalies show that none of them occur: dirty reads, non-repeatable reads and phantoms inside a read-only transaction, and lost updates between writers.
- A reader holding a snapshot sees no change while any number of writes commit.

### 7. Durability and Recovery

- **Write-ahead log** for the page store. Page images and object records are logged. Blob contents are not.
- **Durability modes**, per database, and can be overridden per transaction:
  - `Full` (the default): a commit returns only after its WAL records are durably flushed. Commits that arrive together share one flush (group commit).
  - `Relaxed`: the WAL is flushed on an interval (default 100 ms). A crash may lose recent commits but never corrupts the database.
- **Checkpointing**: WAL contents are written back to the page store in the background. The WAL is capped in size (configurable) so recovery time stays bounded.
- **Checksums**: CRC32C on every page and every WAL record. Blob chunk checksums are described under [Blob Storage](#2-blob-storage). Torn page writes are detected and repaired from the WAL.
- **Flush semantics per platform**:
  - Linux: `fsync` of the file, plus `fsync` of the parent directory after creating, renaming or deleting files.
  - macOS: a true flush requires `F_FULLFSYNC`. We must verify whether `FileStream.Flush(true)` and `RandomAccess.FlushToDisk` do that on the .NET version we target, and call it explicitly if they don't.
  - Windows: `FlushFileBuffers`.
- **A flush failure is fatal**: if an fsync fails, the database moves to a failed state that rejects writes, and must be reopened, which runs recovery. Retrying the flush is never safe, because after a failed fsync the operating system may have silently dropped the dirty pages.
- **Disk full**: writes fail with a specific exception, the database stays readable and consistent, and space is reserved so that a checkpoint can still complete.

**Acceptance criteria**
- Across at least 10,000 randomized crash-injection runs per release, there is no lost acknowledged commit in `Full` mode, no corruption in either mode, and no leaked space after recovery (see [Testing Strategy](#testing-strategy)).

### 8. Space Management

- Page store: free pages are tracked and reused. The file can be compacted to shrink it, offline in the MVP.
- Blob store: space held by unreachable blobs is reclaimed once no snapshot references them. The mechanism depends on the [Blob Store Layout](#blob-store-layout) decision.
- **Secure delete** is optional, per collection. Freed blob space is overwritten, or the blob's encryption key is destroyed when encryption is enabled.
- **Statistics**: live size, reclaimable size and fragmentation, for each collection.

### 9. Operations

- **Online backup**: produces a consistent copy (page store, WAL tail, and all blobs reachable from that snapshot) while the database stays open for reads and writes. Incremental backup is post-MVP.
- **Restore** from a backup, followed by automatic verification.
- **`verify`**: checks every checksum and checks that indexes match their collections. It reports orphaned or missing blobs, and can run online.
- **Export and import** in an open, documented format: a directory of blob files plus metadata as MessagePack or JSON lines. This gives users a way out and supports long-term archiving.
- **On-disk format version**: stored in the database header and kept separate from the package version. Each release opens the current format and the previous one, and upgrades are explicit. Opening a newer format fails with a clear error message.
- **Observability**: metrics through `System.Diagnostics.Metrics`, tracing through `ActivitySource` (both OpenTelemetry-compatible and needing no dependency), and structured logging through an optional `ILogger` abstraction.
- **CLI tool** (`asterope`): verify, backup, restore, export, import, stats, compact, and an index list/build/drop command.

### 10. Embedded API

- All I/O is async, with `CancellationToken` everywhere. Synchronous point reads may be added where profiling shows the async overhead matters.
- The API is built on interfaces (`IDatabase`, `ICollection<TMeta>`, `ITransaction`, `IQuery<TMeta>`) and uses `IAsyncDisposable` for resources.
- Exceptions come from one typed hierarchy (`AsteropeDbException` and subclasses such as `WriteConflictException`, `CorruptionException` and `DiskFullException`), with actionable messages.
- The API does not promise to behave the same in embedded and networked mode. The networked client offers the same operations and query AST, and also documents network failures, retries and idempotency.

### 11. Networked Mode (Post-MVP)

- **Server**: hosted in ASP.NET Core (Kestrel). It serves HTTP/1.1 and HTTP/2, with HTTP/3 optional. TLS is required outside loopback.
- **Blob transfer uses plain HTTP semantics.** Request and response bodies already stream without size limits.
  - `PUT` / `GET` / `HEAD` / `DELETE` on blob resources.
  - `Range` requests for partial reads, and `ETag` headers carrying the commit sequence number for conditional requests.
  - Resumable uploads that map onto staged uploads, following the IETF resumable-uploads work or an equivalent protocol.
- **Control, metadata, queries and transactions** go over gRPC. The query AST is carried as a protobuf message. Blobs never pass through gRPC messages, which avoids the message-size limit.
- **No custom HTTP/2 frame types.** Kestrel doesn't expose them, and proxies drop unknown frames.
- **HTTP/2 flow control**: stream and connection windows must be tuned for bulk transfer. Benchmark HTTP/2 against several HTTP/1.1 connections for bulk throughput before choosing a default for the client.
- **Transactions over the network**: batch transactions (a list of operations plus preconditions on commit sequence numbers, applied atomically) are the main model. Interactive server-side transactions are optional and, if offered, hold a lease with a short timeout.
- **Idempotency keys** on all mutating calls, so clients can retry safely.
- **Authentication**: mTLS or OAuth2/JWT bearer tokens. **Authorization**: per collection, and optionally per key prefix, with separate read, write and admin rights.
- **Audit log hook**: records who read or changed which object, and when. This is required for the healthcare deployments.
- The server never deserializes client-supplied expression trees or types. It only accepts the query AST.

## Technical Design

### Technology Stack
- **Runtime**: .NET 10 (LTS). Libraries target `net10.0`. We follow the LTS line and add multi-targeting only if users ask for it.
- **Language**: C#.
- **Serialization**: MessagePack-CSharp with source-generated formatters.
- **Networking (post-MVP)**: ASP.NET Core and Grpc.AspNetCore.
- **Distribution**: NuGet packages.
- **Dependencies**: only the BCL, except MessagePack in Serialization and ASP.NET Core and gRPC in Network. Any new dependency needs approval.
- **AOT and trimming**: the Core, Storage and Serialization packages must pass the trimming and AOT analyzers with no warnings. So we use no runtime `Expression.Compile()` on hot paths and no reflection-based serialization.

### Package Layout

| Package | Contents |
|---------|----------|
| `AsteropeDb.Core` | Core type system, index key encoding, query AST, public interfaces, exceptions |
| `AsteropeDb.Serialization` | `IDbSerializer`, the canonical MessagePack format, field-path extraction |
| `AsteropeDb.Storage` | Page store, WAL, B+tree, MVCC, blob store, query execution, the embedded `AsteropeDatabase` |
| `AsteropeDb` | Metapackage for embedded use (Core, Serialization and Storage) |
| `AsteropeDb.Tools` | The `asterope` CLI as a .NET tool |
| `AsteropeDb.Network.Server`, `AsteropeDb.Network.Client` | Post-MVP; they replace the current `AsteropeDb.Network` placeholder |

### On-Disk Layout

A database directory contains:

- `asterope.db`: the **page store**. It holds fixed-size pages (default 8 KiB, fixed when the database is created) with a header page containing the format version, page size, database ID and settings. B+tree pages hold object records, inline blobs, secondary indexes and the catalog (collections and index definitions).
- `asterope.wal`: the **write-ahead log**. It is a sequence of records, each with a CRC32C, a commit sequence number, and the database ID (so a WAL from another database can't be applied).
- `blobs/`: the **blob store** for external blobs (see the next section).
- `staging/`: uploads that aren't committed yet.
- `asterope.lock`: the lock file that keeps a second process out.

### Blob Store Layout

This is an **open decision, to be made at milestone M2** using benchmarks on each workload profile. The candidates:

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| A. One file per blob | `blobs/<xx>/<yy>/<blob-id>`, with directories sharded by the ID | Simplest. Reclaiming space means deleting a file. The filesystem handles fragmentation. Moving a staged upload into place is a rename. | Millions of files. Metadata overhead for each file. Backups and antivirus scanners slow down. |
| B. Append-only segment files | Blobs are packed into segment files of about 1 GiB. Objects refer to (segment, offset, length). | Few files, sequential writes, efficient for medium objects | Needs compaction (garbage collection) to reclaim space, and that uses write bandwidth. More complex. |
| C. Hybrid | Segments for blobs below a threshold (for example 64 MiB), one file each above it | Suits all three profiles | Two code paths |

The decision criteria are: small-object throughput at 10^7 objects, cost of reclaiming space, backup time, and implementation complexity. The object record stores a **blob locator** that is independent of the layout, so the layout can change later without touching indexes.

### I/O Strategy

- Large blob data uses **positional I/O** (`RandomAccess.Read`/`Write` on a `SafeFileHandle`, including the scatter/gather overloads), with pooled, aligned buffers. The OS page cache and read-ahead do the caching.
- **We don't use memory-mapped files for blobs.** The reasons:
  - An I/O error on a mapped page crashes the process with SIGBUS or an access violation, which .NET can't catch.
  - We can't control when or in what order dirty mapped pages are written, so durability becomes unpredictable.
  - Growing a mapped file means remapping it, and on Windows a mapped file can't be truncated or deleted.
  - Mapping gives no advantage for sequential streaming.
  - It doesn't give zero-copy network sends either. Those come from `sendfile`/`TransmitFile`, and TLS rules them out anyway.
- **Page store**: starts with positional I/O and its own buffer pool. A read-only memory mapping of the page file (the LMDB approach) may be evaluated in M2 as an optimization. It is never used for writes.
- Direct or unbuffered I/O (`O_DIRECT`, `FILE_FLAG_NO_BUFFERING`) is not in the MVP.

### Blob Write Protocol

1. The client writes into a staged upload file under `staging/`. Chunk checksums, and the optional SHA-256, are computed while streaming.
2. When the upload completes, the staged file is flushed and moved into the blob store. That means a rename (option A) or a copy into a segment (option B). Directory entries are flushed where the platform needs it.
3. A read-write transaction writes the object record with the blob locator, length and checksums, and commits through the WAL.
4. Recovery: any blob-store file or segment range that no committed record refers to is an orphan and is reclaimed. Everything under `staging/` is deleted when the database opens.
5. Deleting or replacing an object marks the old blob as unreachable as of that commit's sequence number. It is physically reclaimed once the oldest live snapshot is newer than that sequence number.

Blob contents never go through the WAL, so large objects are written only once.

### MVCC and Page Management

- Pages are copy-on-write: each commit writes new versions of the pages it changed, and the WAL records them. Old page versions stay readable while snapshots use them.
- Reclamation is epoch-based: each snapshot pins its commit sequence number, and pages and blobs freed before the oldest pinned number are released.
- **To decide at M3**: a copy-on-write B+tree (append-only, LMDB-style) or an update-in-place B+tree with version chains. The decision criteria are write amplification for the small-object profile and how complex recovery gets.

### Index Key Encoding

Every stored index key is a byte string that sorts correctly with plain `memcmp`, is self-delimiting, and carries a type tag. So composite keys are concatenations of their encoded parts, and values of different types never collide.

| Type | Tag | Payload |
|------|-----|---------|
| Nil | `0x01` | none |
| Boolean | `0x02` | `0x00` for false, `0x01` for true |
| Integer (int64) | `0x03` | 8 bytes big-endian of `value ^ long.MinValue` |
| Float (float64) | `0x04` | 8 bytes big-endian of `bits ^ long.MinValue` when `bits >= 0`, else `~bits`. `-0.0` is normalized to `+0.0`, and NaN is rejected. |
| Timestamp | `0x05` | 8 bytes big-endian of `ticks ^ long.MinValue`, then 1 byte of sub-tick nanoseconds (0-99) |
| Date | `0x06` | 4 bytes big-endian of `dayNumber ^ int.MinValue` |
| LocalDateTime | `0x07` | 8 bytes big-endian of `ticks ^ long.MinValue` (no time zone) |
| String | `0x08` | UTF-8 bytes with each `0x00` escaped as `0x00 0xFF`, terminated by `0x00` |
| Bytes | `0x09` | the same escaping and terminator as String |

- Values of different types sort by tag. Typed indexes (the default) never contain mixed types.
- Secondary index entries are `encoded field values || encoded primary key`. This makes every entry unique and lets queries fetch the object without a second lookup.
- Strings compare by **Unicode code point**, which is what UTF-8 byte order gives. So `DbStringType.Compare` must compare code points, not use `string.CompareOrdinal`, which compares UTF-16 code units and disagrees for characters outside the Basic Multilingual Plane.
- **API**: `IDbType<T>` exposes `int GetMaxEncodedLength(T value)` and `bool TryWriteIndexKey(T value, Span<byte> destination, out int bytesWritten)`. It no longer returns an allocated `ReadOnlySpan<byte>`.
- **Known bugs in the current code, fixed by this spec**:
  - `DbFloatType` writes positive values without flipping the sign bit, so negative values sort among the positives. Today's order is `0.0, 1.0, -2.0, 2.0, -1.0, -0.0`.
  - `DbTimestampType` writes `Ticks` without flipping the sign bit, so timestamps before 1970 sort after every later timestamp.
  - Neither key has a type tag, and `DbStringType` keys have no terminator, so composite keys are ambiguous.
  - Every `GetIndexKey` call allocates a new array.

### Core Type System

The types that exist today are `DbNil`, `DbBooleanType`, `DbIntegerType`, `DbFloatType`, `DbStringType` and `DbTimestampType`. Changes:

- Add `DbBytesType`.
- Add `DbDate` (a calendar date without a time zone).
- Add `DbLocalDateTime` (a date and time without a time zone). DICOM `DA`, `TM` and `DT` values usually have no time zone, and forcing them into UTC instants is a well-known source of bugs.
- `DbTimestamp.FromDateTime` must reject `DateTimeKind.Unspecified`, or require the caller to state how to interpret it. Treating it as UTC silently is wrong.
- **Unsigned 64-bit integers** above `long.MaxValue`, which MessagePack can represent, are rejected in indexed fields for now. This is an open question.
- `GetHashCode` results must never be persisted, because .NET string hashing is randomized per process.

### Serialization Boundary

- `IDbSerializer` changes from `byte[]` to buffer-based APIs: `void Serialize<T>(IBufferWriter<byte> writer, T value)` and `T Deserialize<T>(ReadOnlySequence<byte> data)`.
- The Serialization package also provides a **field-path reader**. It pulls indexed values out of canonical MessagePack without deserializing the whole document.
- Stored metadata always uses string keys, never MessagePack's integer-keyed array layout, so a document can be understood without its CLR type.

### Memory and Caching

- **Page cache**: bounded (configurable, default 256 MiB), with a scan-resistant eviction policy such as CLOCK-Pro, 2Q or W-TinyLFU. Plain LRU is not enough, because a single large scan or bulk query evicts the working set.
- **Decoded-metadata cache**: optional and bounded, for hot objects.
- Blob contents are never cached in the managed heap. The OS page cache handles them.
- Buffers come from `ArrayPool<byte>`, or from a dedicated pool of aligned native buffers. Hot paths are checked with allocation benchmarks.

### Compression

- Optional, per collection. Blobs are compressed **in independent chunks** (default 1 MiB), so range reads and verification still work on each chunk.
- The MVP uses BCL codecs (Brotli, Deflate). Zstandard needs a dependency approval.
- Compression is skipped automatically for content types that are already compressed (JPEG, JPEG 2000, H.264/H.265, ZIP) and for chunks that don't shrink by at least 10 percent.
- Metadata pages are not compressed in the MVP.

### Encryption at Rest

- **Required before any production healthcare use.** It is an explicit milestone (post-MVP), not a "maybe".
- AES-256-GCM (`System.Security.Cryptography.AesGcm`), applied to each page and each blob chunk, with a unique nonce per write and the page or chunk ID as associated data.
- The page store must be encrypted as well as blobs, because metadata contains personal data.
- Envelope encryption: data keys are wrapped by a pluggable `IKeyProvider` (a local key file, DPAPI or Keychain, or a cloud KMS through an adapter).
- Blobs are compressed before they are encrypted.
- **Crypto-shredding**: an optional per-object data key. Destroying the key makes the object unreadable, including in backups. This supports the right to erasure.

## Non-Functional Requirements

### Performance Targets

These are initial targets. Milestone M2 validates or revises them on the reference hardware (a local NVMe SSD, 8+ cores, Linux and Windows). Benchmarks use BenchmarkDotNet plus a custom harness, and each target is measured against a **baseline**: SQLite in WAL mode for metadata, plus one file per blob, with equivalent durability.

| Metric | Target |
|--------|--------|
| Large-blob sequential write (64 MiB objects or larger, `Full` durability) | At least 80% of the volume's raw sequential write bandwidth |
| Large-blob sequential read | At least 90% of raw sequential read bandwidth |
| Small-object ingest (0.5 MiB blob plus 2 KiB metadata, batches of 100 per transaction) | At least the baseline's throughput |
| Point lookup by key, warm cache, 10^7 objects | p99 of 1 ms or less |
| Indexed equality query returning 100 or fewer results, warm cache | p99 of 5 ms or less |
| Managed heap growth while streaming a 10 GiB blob | No more than the configured buffer size, and no large-object-heap allocations |
| Recovery time after a crash, with the default WAL cap | 10 s or less |
| Cold open of a database with 10^7 objects | 2 s or less |

### Reliability
- There is no lost acknowledged commit in `Full` mode and no corruption in any mode, across the crash-injection suite (see [Testing Strategy](#testing-strategy)).
- All stored bytes are covered by checksums. Corruption is detected on read and reported with the location, and never silently returned.
- The `verify` tool finds every injected corruption in the fault-injection suite.

### Security
- Networked mode (post-MVP): TLS, authentication, and authorization per collection and key prefix. See [Networked Mode](#11-networked-mode-post-mvp).
- Encryption at rest, as described under [Encryption at Rest](#encryption-at-rest).
- Untrusted input is deserialized with a hardened configuration, and expressions or types supplied by clients are never deserialized.
- Dependencies are scanned in CI.

### Usability
- A small API that is easy to learn. Documented examples exist for every workload profile.
- Error messages say what happened and what to do next.
- Defaults are safe: `Full` durability, typed indexes, and `Reject` for type mismatches.

### Maintainability
- Modular packages with clear dependencies. `AsteropeDb.Core` has no dependencies.
- High coverage from unit, property and integration tests. Crash and fault-injection suites run in CI.
- Code style and process are defined in `AGENTS.md`. Design changes go through specs in `docs/superpowers/specs/`.

### Compatibility
- .NET 10 (LTS). Windows, Linux and macOS on x64 and arm64.
- Local filesystems: NTFS, ext4, XFS, APFS and ReFS. Network filesystems (NFS, SMB) are **not supported** for database files, because locking and flush semantics can't be relied on there. The engine should detect them where it can and warn.
- On-disk format compatibility follows the versioning policy under [Operations](#9-operations).

## Regulatory and Compliance Considerations

AsteropeDb is a general-purpose library, not a medical device, and it makes no certification claims. The healthcare use case still puts requirements on it:

- **Data protection (HIPAA, GDPR)**:
  - encryption at rest and in transit
  - access control and audit logging in networked mode (in embedded mode, the host application is responsible for these)
  - a real right to erasure, covering secure delete, backups and crypto-shredding
  - retention policies set by the host application
- **Use in regulated medical device software (IEC 62304)**: a product that embeds AsteropeDb will classify it as SOUP. To support that, the project publishes:
  - these requirements
  - versioned releases with changelogs
  - a list of known anomalies for each release
  - test and crash-test evidence for each release
  - a support and end-of-life policy for each major version
- **Integrity and traceability**: checksums, verification, the commit sequence number, and created and modified timestamps on every object.

## Scope

### MVP (Embedded)
- Collections, objects, keys, metadata, and inline and external blobs
- Streaming and range reads, staged uploads
- MVCC transactions with a single writer, both durability modes, WAL, checkpointing, recovery
- Secondary indexes: scalar, composite, multi-valued, normalized strings, built online
- Query AST, the rules-based planner, and the LINQ front-end subset
- Optional per-collection compression
- Online backup and restore, verify, export and import, stats, and the CLI
- Metrics, tracing and logging
- Crash-injection and fault-injection test suites

### Post-MVP
- Encryption at rest (P1: required for production healthcare use)
- Networked server and client
- Incremental backup
- Online compaction
- Read-only access from several processes
- More serializer adapters (Protobuf, JSON)
- Placing blobs across several volumes

### Non-Goals
- SQL, joins and aggregations
- Clustering, replication and horizontal scaling
- GUI administration tools
- Network filesystems as database storage

## Assumptions

Each assumption is listed with how we'll check it.

| Assumption | How it is validated |
|------------|---------------------|
| Users value atomic blob and metadata storage enough to adopt a new store instead of SQLite plus files | Talk to 3-5 potential users, including an internal medical imaging team, before M3 |
| One writer is enough for the target workloads | Benchmark small-object ingest in M2 and M4. Staged uploads keep long transfers outside the writer lock. |
| Positional I/O can reach the bandwidth targets without memory mapping | M2 spike |
| A rules-based planner is enough for single-collection queries | Test queries from the use cases below in M6 |
| .NET 10 LTS is acceptable to target users | User interviews, and checking where .NET 8 LTS users stand as it reaches end of support |
| MessagePack is a suitable canonical metadata format | M6. The format is internal and can be versioned if it needs to change. |

## Risk Assessment

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Getting correctness right (crash safety, MVCC) takes far longer than planned | High | High | Single-writer MVCC, crash testing from M3, an I/O abstraction that supports fault injection, and a small MVP scope |
| The existing baseline (SQLite plus files, or an object store) is good enough for users | High | Medium | Validate demand early, benchmark against the baseline, and make atomicity, integrity and backup the differentiators |
| Small-object throughput limited by fsync latency and the single writer | Medium | Medium | Group commit, batch APIs, `Relaxed` mode, and measuring in M2 |
| Space reclamation in the blob store (compaction) costs too much | Medium | Medium | Settle the layout in M2 with benchmarks, and keep blob locators independent of the layout |
| Different flush semantics per platform cause data loss (for example macOS) | High | Medium | Flush code written per platform, and crash tests on every OS in CI |
| The LINQ front-end grows without bound | Medium | High | AST first, a documented subset, and failing when a query is built |
| Bus factor and team size (currently one part-time maintainer) | High | High | Small scope, milestones with clear exit criteria, and thorough design docs |
| Serialization security issues | Medium | Low | Untrusted-data mode and no typeless serialization |

## Milestones

Milestones replace the fixed weekly schedule. Estimates will be set once the team size is known.

| # | Milestone | Deliverables | Exit criteria | Status |
|---|-----------|--------------|---------------|--------|
| M0 | Foundation | Repo, MIT license, README, `AGENTS.md`, solution structure (`AsteropeDb.slnx`), central package management (`Directory.Packages.props`) with lock files, .NET 10 with a pinned SDK (`global.json`) | Build and tests pass, and a CI build-and-test pipeline runs on every PR | Mostly done. The .NET 10 modernization is in progress and the CI pipeline for build and tests is still needed. |
| M1 | Core types and key encoding | Fix the float and timestamp encodings, add type tags and string escaping, the `TryWriteIndexKey` API, code-point string comparison, the new types (Bytes, Date, LocalDateTime), and the buffer-based `IDbSerializer` | Ordering property tests pass for every type, and key encoding doesn't allocate | Not started. The existing types need rework. |
| M2 | Storage spike and decisions | Throwaway prototypes: positional I/O versus memory mapping for pages, blob layouts A, B and C, fsync behavior per OS, the baseline harness | Written decision records for the blob layout, the page I/O strategy and the B+tree style. Revised performance targets. | Not started |
| M3 | Page store, WAL and recovery | Page format, WAL, checkpointing, recovery, the I/O abstraction with fault injection, the crash-test harness | Crash suite (at least 1,000 runs) passes on Linux, Windows and macOS | Not started |
| M4 | B+tree and MVCC transactions | Primary-key B+tree, snapshots, the writer lock, group commit, reclamation | Anomaly tests and the crash suite pass. Small-object benchmark within the M2 targets. | Not started |
| M5 | Blob store | Staged uploads, the blob write protocol, chunk checksums, range reads, orphan cleanup, optional compression | Streaming memory targets met. Crash suite covers uploads. | Not started |
| M6 | Metadata, indexes and queries | Canonical MessagePack, field-path reader, secondary indexes, online build, query AST, planner, LINQ subset | Queries from the use cases below run through indexes, confirmed with `Explain()` | Not started |
| M7 | Operations and MVP release | Backup and restore, verify, export and import, the CLI, metrics and tracing, docs, NuGet packages | Every MVP acceptance criterion met, crash suite at 10,000 or more runs, published SOUP documentation | Not started |
| M8 | Encryption at rest | Page and blob encryption, key providers, crypto-shredding | Tests with known vectors pass, encryption overhead is measured | Post-MVP |
| M9 | Networked mode | Server, client, auth, audit hook | Transfer benchmarks meet targets. The protocol is documented. | Post-MVP |

## Testing Strategy

Testing runs throughout the project, not in a phase at the end. Every storage milestone ships with its crash and fault tests.

- **Unit tests**: xUnit, for all components.
- **Property-based tests**: the key-encoding ordering property, round-trips for encoding and serialization, and B+tree invariants under random operation sequences. The testing library needs dependency approval.
- **Model-based tests**: random operation sequences run against the engine and against a simple in-memory reference model, with the results compared after each step, including across snapshots.
- **Crash-injection tests**: an `IFileSystem` / I/O abstraction that simulates power loss, meaning unflushed writes are dropped or torn at arbitrary points. Tests also kill real processes during randomized workloads. Each run checks that no acknowledged commit is lost, that there is no corruption, and that `verify` reports a clean database.
- **Fault-injection tests**: checksum mismatches, failing fsync (the database must move to its failed state), disk full, and short reads.
- **Isolation tests**: explicit tests for each anomaly listed under [Transactions and Concurrency](#6-transactions-and-concurrency).
- **Performance tests**: BenchmarkDotNet micro-benchmarks, plus workload benchmarks for each profile against the baseline. CI tracks regressions.
- **Memory tests**: allocation assertions on streaming and hot paths.
- **Cross-platform**: every suite runs on Linux, Windows and macOS in CI.

## Release and Deployment

- **Distribution**: NuGet.org packages and the `asterope` .NET tool.
- **Versioning**: SemVer for packages. The on-disk format version is separate and documented. Every release notes whether it changes the format and whether an upgrade is needed.
- **Release artifacts**: changelog, known anomalies, and the test and crash-suite report.
- **Pipeline**: build, test (all suites), pack, sign, publish, then update the docs.
- **Rollback**: packages can be reverted through NuGet versioning. An on-disk format upgrade can't be undone, so the release notes tell users to back up before upgrading.

## Documentation and Support

- API reference generated from XML docs.
- Guides for each workload profile (medical imaging, scientific data, media, backup, asset pipelines).
- A design and architecture doc for contributors: on-disk format, WAL, MVCC, key encoding.
- An operations guide covering backup, verify, capacity planning, durability modes, and unsupported filesystems.
- Community support through GitHub issues, and a published support policy for each major version.

## Use Cases

1. **Radiology and radiotherapy data**
   - A collection per modality or per study type. Each DICOM object is one object in the store. Indexed fields: `PatientId`, `StudyInstanceUid`, `SeriesInstanceUid`, `Modality`, and `StudyDate` (a `DbDate`, no time zone).
   - Typical queries:
     - all instances of a series, ordered by `InstanceNumber` (composite index)
     - studies for a patient within a date range
   - Object sizes: 0.5 MiB CT slices (the many-small-objects profile), multi-frame and RT dose objects of tens to hundreds of MiB.
   - Requires encryption at rest, audit logging (in networked mode), and erasure.
2. **Scientific data**
   - Experimental datasets from MiB to TB in size, with metadata for instrument, run, parameters and researcher. Range reads over large arrays.
3. **Media archives**
   - Images, audio and video with multi-valued `Tags`, and author and creation date. Range reads for seeking in video. Compression skipped automatically for already-compressed formats.
4. **Backups**
   - Archives or chunks keyed by content hash, which gives deduplication through Put-if on the key. Metadata holds source, date and type. Very large objects, and consistent snapshots of the store itself.
5. **Asset pipelines**
   - Textures, models and audio with build-pipeline metadata. Dependencies are stored as a multi-valued field of keys. There are no joins, so the application follows the references itself.

## Open Questions

1. The blob store layout: A, B or C. Decided in M2.
2. A copy-on-write B+tree or update-in-place with version chains. Decided at M3.
3. Whether to allow read-only access to a database from several processes, and how, given the lock file and the page cache.
4. How to handle unsigned 64-bit integers above `long.MaxValue` in indexed fields.
5. Whether a decimal type is needed (for example for financial or measurement data).
6. Which property-based testing library to use. Needs dependency approval.
7. Whether Zstandard compression is worth adding a dependency.
8. Whether per-object keys for crypto-shredding are the default or opt-in, and what they cost.
9. What the resumable-upload protocol for networked mode should be.
10. Whether a DICOM-specific adapter package is in scope, for example mapping DICOM tags to indexed fields, or QIDO-RS-style queries.

## Approval and Sign-off

### Stakeholders
- **Project Owner**: Matija Kejžar
- **Technical Lead**: To be determined
- **Community Contributors**: Open source contributors and maintainers

### Review Process
1. Technical review of this document, focused on the open questions and the decision records from M2.
2. Collect community and user feedback, including the assumption-validation interviews.
3. Approval. After that, design changes go through specs in `docs/superpowers/specs/` and are then merged into this document.

---

**Document Version**: 2.0
**Last Updated**: 2026-09-25
**Created By**: Project Team
**Status**: Draft
