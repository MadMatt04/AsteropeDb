# AsteropeDb.Core - Type System Implementation

This module contains the core type system for AsteropeDb, providing extensible database types with clean separation between indexing logic and serialization.

## Implemented Types

### Core Interfaces
- **`IDbType<T>`** - Defines database type behavior for indexing, comparison, and validation
- **`IDbTypeRegistry`** - Registry for database types and their associated behaviors

### Built-in Database Types

1. **`DbIntegerType`** - 64-bit signed integers (`long`)
   - Uses big-endian binary encoding for index keys
   - Standard numeric comparison

2. **`DbNilType`** - Null value representation (`DbNil`)
   - Special unit type with single value
   - Empty index keys, all nil values are equal

3. **`DbBooleanType`** - Boolean values (`bool`)
   - Single-byte index keys (0 for false, 1 for true)
   - Standard boolean comparison (false < true)

4. **`DbFloatType`** - IEEE 754 double precision (`double`)
   - Proper handling of NaN, infinity, and negative zero
   - NaN values are invalid for indexing
   - Bit-level comparison for consistent ordering

5. **`DbStringType`** - UTF-8 strings (`string`)
   - Culture-invariant ordinal comparison
   - UTF-8 encoded index keys
   - Null strings are invalid

6. **`DbTimestampType`** - Timezone-independent nanosecond precision (`DbTimestamp`)
   - Based on UTC ticks since Unix epoch
   - Additional nanosecond component (0-99) for sub-tick precision
   - Chronological ordering suitable for time-series data
   - 16-byte index keys (8 bytes ticks + 8 bytes nanoseconds)

## Key Features

- **Extensible Design**: Implement `IDbType<T>` for custom types
- **Performance Optimized**: Uses `ReadOnlySpan<byte>` for index keys to avoid allocations
- **B+ Tree Ready**: Index keys designed for efficient tree operations
- **Type Safety**: Strongly typed interfaces with compile-time guarantees
- **Singleton Pattern**: All built-in types use singleton instances for efficiency

## Usage Example

```csharp
// Using built-in types
IDbType<long> intType = DbIntegerType.Instance;
IDbType<string> stringType = DbStringType.Instance;
IDbType<DbTimestamp> timestampType = DbTimestampType.Instance;

// Type operations
int comparison = intType.Compare(42L, 100L); // -1
bool isValid = stringType.IsValid("hello"); // true
ReadOnlySpan<byte> indexKey = timestampType.GetIndexKey(DbTimestamp.FromDateTime(DateTime.UtcNow));
```

## Integration Points

- **Serialization**: Works with pluggable `IDbSerializer` implementations
- **B+ Tree Indexing**: Index keys designed for efficient tree operations
- **LINQ Queries**: Comparison logic supports LINQ expression evaluation
- **Storage Engine**: Compatible with memory-mapped file storage