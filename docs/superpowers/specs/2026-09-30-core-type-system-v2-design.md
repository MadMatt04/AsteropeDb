# Core Type System v2 - Design

## Problem

The repo targets net10.0 with `LangVersion=latest`, but the only real code in
it - the Core type system - was written against the pre-PRD-v2 shape. Its
`IDbType<T>.GetIndexKey` returns an allocated `ReadOnlySpan<byte>` from a
fresh array on every call; its keys have no type tags, no string terminator,
and two of them (`DbFloatType`, `DbTimestampType`) sort incorrectly (positive
floats are written without a sign-bit flip; pre-1970 timestamps sort after
post-1970 ones - the exact class of bug that corrupts B+ tree lookups while
every test passes). `DbStringType.Compare` uses `string.CompareOrdinal`
(UTF-16 code units), which orders astral characters against the PRD's
code-point requirement. `DbTimestamp.FromDateTime` silently treats
`DateTimeKind.Unspecified` as UTC. `IDbSerializer` round-trips whole `byte[]`
arrays where the PRD requires buffer-based streaming. `GetHashCode` on
`IDbType<T>` exposes randomized, non-persistable string hashes through a
public contract that the PRD forbids persisting.

The PRD has already specified the target for every one of these (lines
328-369): tag-prefixed self-delimiting keys, `TryWriteIndexKey` into caller
buffers, code-point string comparison, `IBufferWriter`/`ReadOnlySequence`
serialization, three new types, and no persisted hash codes. This cycle
implements that specification, lands the C# 14 idioms on the value types, and
retires `GetIndexKey` - while the only consumers are the 126 tests in this
repo, so the interface break is nearly free. Doing it later is not nearly
free: after M2 every on-disk key and every dependent package pins the old
shape.

## Scope

In scope:

- Replace `IDbType<T>` with the PRD contract (`GetMaxEncodedLength`,
  `TryWriteIndexKey`); delete `GetHashCode` and `GetIndexKey` from it.
- Rework all six existing `Db*Type` implementations to the new contract with
  PRD-tag encodings (Nil 0x01, Boolean 0x02, Integer 0x03, Float 0x04,
  Timestamp 0x05, String 0x08), including the two ordering bug fixes, string
  `0x00` escaping + terminator, and code-point string comparison.
- Add the three PRD types: `DbBytesType` (0x09), `DbDate` (0x06),
  `DbLocalDateTime` (0x07).
- `DbTimestamp.FromDateTime`: reject `DateTimeKind.Unspecified`.
- Replace `IDbSerializer` with the PRD buffer-based contract (verbatim from
  PRD line 367).
- Modernize `DbTimestamp` and `DbNil` with the verified C# 14 idioms:
  generic-math operator interfaces, `ISpanFormattable`,
  `IUtf8SpanFormattable`, `ISpanParsable`, primary constructors.
- Rewrite the six existing Core test files against the new contract
  (behavior-preserving tests migrate; behavior-changing tests are rewritten
  to assert the new ordering).
- Update PRD.md M1 status row (done via this spec, as the tripwire requires).

Out of scope (aggressively):

- `DbTypeRegistry` (the `IDbTypeRegistry` implementation). `IDbTypeRegistry`
  still compiles against the new contract; the implementation is its own
  cycle and the PRD does not specify it.
- MessagePack itself, the canonical format, and the field-path reader. The
  new `IDbSerializer` is a contract; no implementation ships this cycle. No
  new dependency (MessagePack-CSharp) is added.
- AOT/trimming analyzer enablement (`IsAotCompatible`, PublishAot). Build
  shape change; PRD lists it as a goal but not as a type-system API.
- `IDbType<T>` `where T : notnull` tightening. PRD does not ask for it; the
  current implementations accept nulls deliberately (`IsValid` gates them).
- Null handling semantics in `TryWriteIndexKey` (PRD does not specify;
  current `GetIndexKey` throws on null strings - that behavior is preserved
  and documented).
- Docs for `DbTimestamp`'s new parse/format methods, beyond the `<inheritdoc/>`
  that is required by the style rules.
- `DbDate`/`DbLocalDateTime` conversions to `DateTimeOffset`/`DateTime` with
  assumed UTC. Only the PRD-listed members are introduced; extending them is
  a separate decision.
- PRD's open question on unsigned 64-bit integers (line 362). Deliberately
  untouched.

## Approach

One Track L cycle, five packages' worth of edits but one coherent contract
change: the new `IDbType<T>` + the nine type implementations + the serializer
contract, in `AsteropeDb.Core`/`AsteropeDb.Serialization`, then the test
rewrite, then the PRD status row. The encoding rules are transcribed verbatim
from PRD lines 332-353 - no design is invented here; the "design" work is the
API shape and the test strategy.

Key encoding is allocation-free by construction: every implementation writes
its fixed-length tag+payload into the caller's `Span<byte>` with
`BinaryPrimitives.TryWriteInt64BigEndian`/`TryWriteUInt64BigEndian`/
`TryWriteInt32BigEndian`; the two variable-length types (`String`, `Bytes`)
write UTF-8 bytes with `0x00` escaped as `0x00 0xFF` and a `0x00` terminator,
exactly as PRD line 341-342 specifies. Escape handling uses a preallocated
`SearchValues<byte>` set (`{ 0x00, 0xFF }`), a private `static readonly` field
on the singleton type - no allocation per call.

Generic math supplies every operator `DbTimestamp`/`DbNil` currently declare
by hand via `IEqualityOperators<,>` and `IComparisonOperators<,>`. The
operators must still be *declared* in the type (CS0535, verified) but become
six one-line expression-bodied wrappers - the readable form, and the one the
tests exercise.

The value types move to primary constructors with a guarded property
initializer. The guard stays a one-liner (`nanoseconds is >= 0 and <= 99
? nanoseconds : throw new ArgumentOutOfRangeException(...)`) so the class
level stays `readonly struct` without field initializers or initialization
order hazards - verified to compile and throw with the correct param name.

`DbTimestamp.ToString()` becomes `string.Create` over
`TryFormat(default, null)` so the UTF-16 path is allocation-light, with
`IUtf8SpanFormattable` giving the allocation-free UTF-8 path. Formatting
*delegates to `DateTime`* (`ToDateTime().TryFormat(...)`), so this cycle
inherits the BCL's behavior for calendar years outside the supported range -
the exotic year-9999 `TryFormat` edge is documented as a known limitation in
the plan, not engineered around. `DbTimestamp` keeps its custom
`ISpanParsable` implementation; `DbDate`/`DbLocalDateTime` implement
`ISpanFormattable`/`IUtf8SpanFormattable` only (no parsing - the PRD does not
specify parse syntax for them).

The `GetHashCode` members disappear from `IDbType<T>` and all nine
implementations (answer 5). This removes the trap where randomized string
hashing leaks through a public contract. Nothing in the tree calls them.

String comparison goes code-point-based: a dual rune enumeration
(`StringRuneEnumerator` values, public and returning structs - safe to hold)
compares code points in order, tie-breaks on code-point count. For equal
strings this is O(n); an ordinal fast-path is a separate cycle if M2
benchmarks ever demand one - it is not needed to be correct.

## Contracts

All signatures below are compiled and verified against SDK 10.0.101,
`LangVersion=latest`, C# 14, net10.0, in a sandbox probe on 2026-09-30.
`System.Numerics` must be imported wherever the operator interfaces appear.

### IDbType<T> (replaced, `src/AsteropeDb.Core/IDbType.cs`)

```csharp
public interface IDbType<in T>
{
    string TypeName { get; }

    int Compare(T left, T right);

    bool IsValid(T value);

    /// <summary>Upper bound on the encoded key length for this value. The
    /// destination span passed to <see cref="TryWriteIndexKey"/> must be at
    /// least this long for the call to succeed.</summary>
    int GetMaxEncodedLength(T value);

    /// <summary>Writes the self-delimiting, tag-prefixed index key for
    /// <paramref name="value"/> into <paramref name="destination"/>.
    /// Returns false (bytesWritten = 0) when the span is too small; throws
    /// for values that cannot be encoded (NaN floats, null strings, null
    /// bytes).</summary>
    bool TryWriteIndexKey(T value, Span<byte> destination, out int bytesWritten);
}
```

`GetIndexKey` and `GetHashCode` are deleted from the interface and from all
implementations.

### Key encodings (transcribed from PRD 332-343)

| Type | Tag | Payload | GetMaxEncodedLength |
|------|-----|---------|---------------------|
| Nil | 0x01 | none | 1 |
| Boolean | 0x02 | 0x00 false / 0x01 true | 2 |
| Integer | 0x03 | 8B BE `value ^ long.MinValue` | 9 |
| Float | 0x04 | 8B BE `bits ^ long.MinValue` when bits >= 0, else `~bits`; -0.0 normalized to +0.0; NaN rejected | 9 |
| Timestamp | 0x05 | 8B BE `ticks ^ long.MinValue`, 1B nanos | 10 |
| Date | 0x06 | 4B BE `dayNumber ^ int.MinValue` | 5 |
| LocalDateTime | 0x07 | 8B BE `ticks ^ long.MinValue` | 9 |
| String | 0x08 | UTF-8, 0x00 -> 0x00 0xFF, terminator 0x00 | 3n + 2 upper bound (n = UTF-16 code units; a BMP unit can encode to 3 UTF-8 bytes) |
| Bytes | 0x09 | same escaping/terminator as String | n + (escaped 0x00 count) + 2 |

Implementations for fixed-size keys use `BinaryPrimitives.TryWriteInt64BigEndian`
(verified) into `destination[1..]` after writing the tag into `destination[0]`.
Timestamp payload uses a single byte for nanoseconds after the flipped ticks.
Date uses `DateOnly.DayNumber` (verified) with `TryWriteInt32BigEndian`
(verified).

String/bytes escaping uses a `private static readonly SearchValues<byte>`
field (verified) and writes via `Encoding.UTF8.GetBytes(value.AsSpan(),
escaped)` for strings, with the terminator appended.

### DbTimestamp (replaced, `src/AsteropeDb.Core/DbTimestamp.cs`)

```csharp
public readonly struct DbTimestamp : IEquatable<DbTimestamp>,
    IComparable<DbTimestamp>,
    IEqualityOperators<DbTimestamp, DbTimestamp, bool>,
    IComparisonOperators<DbTimestamp, DbTimestamp, bool>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    ISpanParsable<DbTimestamp>
{
    private const long MaxNanoseconds = 99L;

    public DbTimestamp(long ticks, long nanoseconds = 0);  // primary ctor

    public long Ticks { get; }                 // primary ctor params
    public long Nanoseconds { get; }           // guarded: 0..99, throws
                                               // ArgumentOutOfRangeException
                                               // with nameof(nanoseconds)

    public static DbTimestamp FromDateTime(DateTime dateTime);
        // rejects DateTimeKind.Unspecified (ArgumentException);
        // Local converts to UTC; Utc used directly
    public static DbTimestamp FromDateTimeOffset(DateTimeOffset dateTimeOffset);
    public DateTime ToDateTime();              // Kind = Utc
    public DateTimeOffset ToDateTimeOffset();  // offset zero
    public int CompareTo(DbTimestamp other);   // ticks then nanos
    public bool Equals(DbTimestamp other);
    public override bool Equals(object? obj);
    public override int GetHashCode();         // HashCode.Combine
    public override string ToString();         // string.Create via TryFormat
    public string ToString(string? format, IFormatProvider? formatProvider);
    public static DbTimestamp Parse(string s, IFormatProvider? provider);
    public static DbTimestamp Parse(ReadOnlySpan<char> s,
        IFormatProvider? provider);
    public static bool TryParse(string? s, IFormatProvider? provider,
        out DbTimestamp result);
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider,
        out DbTimestamp result);
    public bool TryFormat(Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider);
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider);
        // delegates to ToDateTime().TryFormat - allocation-free
    // all six comparison operators, declared, one-line expression-bodied
    // (required by IComparisonOperators/IEqualityOperators - CS0535,
    // verified); default interface methods supply the rest
}
```

`DbTimestampType` (same file, unchanged except for the new `IDbType<T>`
members) implements the 0x05 encoding above.

### DbNil (replaced, `src/AsteropeDb.Core/DbNil.cs`)

```csharp
public readonly struct DbNil : IEquatable<DbNil>,
    IEqualityOperators<DbNil, DbNil, bool>
{
    public static DbNil Value { get; }        // primary-ctor-free struct
    public bool Equals(DbNil other) => true;
    public override bool Equals(object? obj) => obj is DbNil;
    public override int GetHashCode() => 0;
    public override string ToString() => "nil";
    public static bool operator ==(DbNil left, DbNil right) => true;
    public static bool operator !=(DbNil left, DbNil right) => false;
}
```

`IComparisonOperators` is deliberately NOT implemented by `DbNil`: the struct
defines no order (PRD says all nil values are equal); the type's `Compare`
returns 0 by contract. A comparison interface would advertise an order that
does not exist. `DbNilType` (same file) implements the 0x01 encoding.

### DbStringType (`src/AsteropeDb.Core/DbStringType.cs`)

- `Compare`: dual `StringRuneEnumerator` code-point comparison, count
  tie-break (PRD line 346).
- `GetMaxEncodedLength(string value)`: `(3 * value.Length) + 2` (safe upper
  bound: a BMP UTF-16 code unit can encode to 3 UTF-8 bytes, so 3n covers
  every input; the terminator and tag add 2).
- `TryWriteIndexKey`: `Encoding.UTF8.GetBytes(value.AsSpan(), escapedSpan)`,
  escaping the `0x00` bytes that `GetBytes` wrote (single pass over the
  produced bytes - no `Replace`, no second `GetBytes`).
- Preserves `ArgumentNullException` on null via `ArgumentNullException.ThrowIfNull`
  (verified); contract documented.
- `GetHashCode` deleted.

### DbIntegerType / DbFloatType / DbBooleanType

Same files, new contract. Integer flips the sign bit (`value ^ long.MinValue`).
Float normalizes `-0.0` to `+0.0` (PRD 337), rejects NaN (ArgumentException),
and writes the PRD 0x04 transformation - which fixes the current
negative-values-sort-among-positives bug. Boolean writes the 0x02 tag.

### DbBytesType (new, `src/AsteropeDb.Core/DbBytesType.cs`)

```csharp
public sealed class DbBytesType : IDbType<byte[]>
{
    public static DbBytesType Instance { get; }

    public string TypeName => "bytes";

    /// <summary>Ordering is lexicographic unsigned byte order.</summary>
    public int Compare(byte[] left, byte[] right);
        // ReadOnlySpan<byte>.SequenceCompareTo (verified)

    public bool IsValid(byte[] value);           // value != null
    public int GetMaxEncodedLength(byte[] value);
        // value.Length + count of 0x00 (escape pairs) + 2 (tag+terminator)
    public bool TryWriteIndexKey(byte[] value, Span<byte> destination,
        out int bytesWritten);
        // copies bytes, escaping 0x00 -> 0x00 0xFF, terminator 0x00
}
```

Byte-span overloads are NOT introduced: `IDbType<T>` is generic over `byte[]`
today, and `SearchValues<byte>` scan + span copy inside
`TryWriteIndexKey(byte[] ...)` is already allocation-free. A `ReadOnlyMemory`
generic variant is a separate interface decision, out of scope.

### DbDate (new, `src/AsteropeDb.Core/DbDate.cs`)

```csharp
public readonly struct DbDate : IEquatable<DbDate>,
    IComparable<DbDate>,
    IEqualityOperators<DbDate, DbDate, bool>,
    IComparisonOperators<DbDate, DbDate, bool>,
    ISpanFormattable,
    IUtf8SpanFormattable
{
    public DbDate(int dayNumber);               // throws
                                                // ArgumentOutOfRangeException
                                                // unless 0 <= dayNumber <=
                                                // DateOnly.MaxValue.DayNumber
    public int DayNumber { get; }               // days since 0001-01-01

    public static DbDate FromDateOnly(DateOnly dateOnly);
    public DateOnly ToDateOnly();               // DateOnly.FromDayNumber
    public int CompareTo(DbDate other);         // DayNumber.CompareTo
    public bool Equals(DbDate other);
    public override bool Equals(object? obj);
    public override int GetHashCode();          // DayNumber.GetHashCode()
    public override string ToString();          // ToDateOnly().ToString("O",
                                                // CultureInfo.InvariantCulture)
    public string ToString(string? format, IFormatProvider? formatProvider);
    public bool TryFormat(Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider);
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider);
    // six comparison operators, declared, expression-bodied
}

public sealed class DbDateType : IDbType<DbDate>
{
    public static DbDateType Instance { get; }

    public string TypeName => "date";
    public int Compare(DbDate left, DbDate right);      // CompareTo
    public bool IsValid(DbDate value);                  // true
    public int GetMaxEncodedLength(DbDate value);       // 5
    public bool TryWriteIndexKey(DbDate value, Span<byte> destination,
        out int bytesWritten);   // 0x06, 4B BE (dayNumber ^ int.MinValue)
}
```

`DateOnly.DayNumber`/`DateOnly.FromDayNumber` verified. The day-number range
guard (0..`DateOnly.MaxValue.DayNumber`) matches `DateOnly`'s own domain.
No `ISpanParsable` on `DbDate` - the PRD specifies no date parse syntax.

### DbLocalDateTime (new, `src/AsteropeDb.Core/DbLocalDateTime.cs`)

```csharp
public readonly struct DbLocalDateTime : IEquatable<DbLocalDateTime>,
    IComparable<DbLocalDateTime>,
    IEqualityOperators<DbLocalDateTime, DbLocalDateTime, bool>,
    IComparisonOperators<DbLocalDateTime, DbLocalDateTime, bool>,
    ISpanFormattable,
    IUtf8SpanFormattable
{
    public DbLocalDateTime(long ticks);         // Ticks property
    public long Ticks { get; }                  // primary ctor param

    public int CompareTo(DbLocalDateTime other);  // Ticks.CompareTo
    public bool Equals(DbLocalDateTime other);
    public override bool Equals(object? obj);
    public override int GetHashCode();            // Ticks.GetHashCode()
    public override string ToString();
        // "O" round-trip over a DateTime with Kind = Unspecified
    public string ToString(string? format, IFormatProvider? formatProvider);
    public bool TryFormat(Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider);
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider);
    // six comparison operators, declared, expression-bodied
}

public sealed class DbLocalDateTimeType : IDbType<DbLocalDateTime>
{
    public static DbLocalDateTimeType Instance { get; }

    public string TypeName => "localdatetime";
    public int Compare(DbLocalDateTime left, DbLocalDateTime right);
    public bool IsValid(DbLocalDateTime value);        // true
    public int GetMaxEncodedLength(DbLocalDateTime value);   // 9
    public bool TryWriteIndexKey(DbLocalDateTime value,
        Span<byte> destination, out int bytesWritten);
        // 0x07, 8B BE (ticks ^ long.MinValue)
}
```

### IDbSerializer (replaced, `src/AsteropeDb.Serialization/IDbSerializer.cs`)

```csharp
public interface IDbSerializer
{
    string Name { get; }

    /// <summary>Serializes a value into the writer's buffer without
    /// intermediate allocation.</summary>
    void Serialize<T>(IBufferWriter<byte> writer, T value);

    /// <summary>Deserializes a value from a buffer sequence.</summary>
    T Deserialize<T>(ReadOnlySequence<byte> data);

    bool CanSerialize<T>();
}
```

Verbatim from PRD line 367 (`using System.Buffers` added). `CanSerialize`
stays: the PRD neither removes nor replaces it, and deleting it is not this
cycle's decision.

## Storage implications

None exist yet (no on-disk format, no WAL, no mmap). This cycle is the last
window where the index-key encoding can change for free; after M2 the encoded
form is append-only and backwards-compatible. The encodings transcribed above
are therefore normative for M2: the type tags, the sign-bit flips, the string
escaping and the terminator are the format.

## Failure modes

- **A key encodes but sorts wrong** (a flipped transformation, a missed tag).
  Guard: an ordering property test per type (already present in the test
  suite) rewritten to assert the new order - including the regression pairs
  that fail on today's code (negative floats, pre-1970 timestamps). This is
  the bug class the tripwire names; it must have a test that fails before the
  rework and passes after.
- **The C# 14 compiler (Roslyn) crashes on an unprobed construct.** Verified
  in sandbox: the only crash in the probe set is the `extension` block
  (`extension(int x) { ... }`), which produces a Roslyn NRE. It is therefore
  banned from this cycle and noted as such. Everything else in the contracts
  compiles and runs on SDK 10.0.101.
- **`TryWriteIndexKey` on too-small spans.** Contract: returns false with
  `bytesWritten = 0`, never partial writes. A test per type asserts the false
  path and that the buffer is untouched. Consumers (future B+ tree) pre-size
  with `GetMaxEncodedLength`.
- **`DbTimestamp.TryFormat` delegates to `DateTime.TryFormat`**, which cannot
  format calendar years outside 1-9999 (it returns false with a small
  destination). `DbTimestamp`'s Ticks domain exceeds that. Documented known
  limitation; `ToString`/format tests use in-range values. Nothing silently
  corrupts - the failure is a false return.
- **Escape logic mis-counts bytes** in `DbBytesType.GetMaxEncodedLength`
  (0x00 count). Guard: exact-length tests with embedded nulls, plus the
  write-then-assert-exact-bytes pattern.
- **A `GetHashCode` call site breaks when the member is deleted.** Verified:
  none exist outside the tests (grep on 2026-09-30); the tests are rewritten
  in the same cycle.
- **xunit v2 + `CancellationToken` test parameters.** Verified: xunit 2.9.3
  produces xUnit1001 (an analyzer warning) on `[Fact]` methods with
  parameters - it does NOT silently pass or fail. New tests therefore carry
  `CancellationToken` only where the AGENTS.md wording permits (the repo's
  convention has so far not applied it to plain xUnit 2 tests; this cycle
  keeps that convention to stay warning-free).
- **StyleCop on C# 14 syntax.** The pinned StyleCop (1.2.0-beta.556) predates
  C# 14; probes show it analyzes the constructs fine (primary constructors,
  collection expressions, field properties compile clean under the repo
  ruleset). If a specific idiom trips an SA rule, the plan guards it by
  keeping the gate as the check and reporting any new warning as a failed
  step.

## Testing strategy

Plain xUnit only (repo rule), `tests/AsteropeDb.Core.Tests`, one test class
per production type. The rewrite covers the six existing files plus three new
ones (`DbBytesTypeTests`, `DbDateTests`, `DbLocalDateTimeTests`).

- Every type: `TryWriteIndexKey` happy path asserting exact tag+payload bytes;
  too-small-destination returns false with untouched buffer; ordering
  property (keys byte-compare in value order across a spread including
  boundary values - negative/positive floats, pre/post-1970 timestamps,
  astral strings); `GetMaxEncodedLength` >= actual for a sample of values.
- String/bytes: exact escaped bytes for values containing `0x00`/`0xFF`;
  round-trip-free compare tests for the code-point ordering (surrogate
  pairs); bytes type ordering via `SequenceCompareTo` semantics.
- The two regression pairs that fail on today's code:
  `DbFloatType` keys for `{ 0.0, 1.0, -1.0 }` must sort `-1.0, 0.0, 1.0`;
  `DbTimestampType` keys for pre/post-epoch timestamps must sort
  pre-epoch first.
- Value types: operator behavior via the generic-math interfaces (declared
  operators tested directly), `TryFormat`/`TryParse` round-trips
  (`ISpanParsable` on `DbTimestamp`), `FromDateTime` Unspecified-kind
  rejection.
- `DbTimestampTypeTests` stays the canonical test file for both the struct
  and its type (existing convention, one file per production type).
- Serialization: `IDbSerializer` has no implementation this cycle, so the
  contract is not unit-tested beyond compilation (nothing to assert; the
  placeholder suite keeps its single test). The next cycle that adds a
  MessagePack implementation brings its own tests.

## Rejected alternatives

- **Keep `GetIndexKey` alongside `TryWriteIndexKey`**: doubles the surface
  every future key type must implement; the PRD explicitly says the API "no
  longer returns an allocated `ReadOnlySpan<byte>`"; answer 3 confirmed
  deletion.
- **Code-point compare via `string.CompareOrdinal` + documentation**: wrong
  per PRD line 346; astral characters misorder in indexes; rejected.
- **`DbNil : IComparisonOperators<...>`**: the struct has no order; the
  interface would advertise one; rejected.
- **`DbBytesType : IDbType<ReadOnlyMemory<byte>>` or span overloads**: a
  different generic instantiation changes the registry's type-key story;
  out of scope; `byte[]` is the contract today.
- **`DbTimestamp.Parse` via `DateTime.Parse`**: culture- and
  format-dependent; the struct's own format is well-defined; rejected.
- **C# 14 `extension` blocks** (would be the most visible "bleeding edge"):
  Roslyn NRE on this SDK (verified in sandbox); banned this cycle; revisit
  when the SDK ships a fix.
- **Primary-constructor + field-based `Nanoseconds` validation via a
  method**: property-initializer throw is the verified pattern; a helper
  method adds a call site without adding clarity.
- **`IDbType<T>` keeps `GetHashCode` but documented as ephemeral**: the
  member invites persistence (PRD line 363 forbids it); answer 5 confirmed
  dropping it.
