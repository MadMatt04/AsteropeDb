# Core Type System v2 - Design

## Problem

The repo targets net10.0 with `LangVersion=latest`, but the only real code
in it - the Core type system and the `IDbSerializer` seam - was written
against the pre-PRD-v2 shape. Its `IDbType<T>.GetIndexKey` returns an
allocated `ReadOnlySpan<byte>` from a fresh array on every call; its keys
have no type tags, no string terminator, and two of them (`DbFloatType`,
`DbTimestampType`) sort incorrectly (positive floats are written without a
sign-bit flip; pre-1970 timestamps sort after post-1970 ones - the exact
class of bug that corrupts B+ tree lookups while every test passes).
`DbStringType.Compare` uses `string.CompareOrdinal` (UTF-16 code units),
which orders astral characters against the PRD's code-point requirement -
for `"\uFFFF"` against `"\U0001F600"` the comparison disagrees with the
UTF-8 key order. `DbTimestamp.FromDateTime` silently treats
`DateTimeKind.Unspecified` as UTC. `IDbSerializer` round-trips whole
`byte[]` arrays where the PRD requires buffer-based streaming. `GetHashCode`
on `IDbType<T>` exposes randomized, non-persistable string hashes through a
public contract that the PRD forbids persisting.

The PRD has already specified the target for every one of these (lines
328-369): tag-prefixed self-delimiting keys, `TryWriteIndexKey` into caller
buffers, code-point string comparison, `IBufferWriter`/`ReadOnlySequence`
serialization, three new types, and no persisted hash codes. This cycle
implements that specification, lands the modern .NET idioms on the value
types, and retires `GetIndexKey` - while the only consumers are the 126 Core
tests (129 across the repo's four suites), so the interface break is nearly
free. Doing it later is not nearly free: after M2 every on-disk key and
every dependent package pins the old shape.

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
- Define `DbTimestamp`'s canonical text format (the existing `ToString`
  shape, pinned to the invariant culture) and add `ISpanFormattable`,
  `IUtf8SpanFormattable` and `ISpanParsable` against it. The current struct
  has none of these interfaces.
- Replace `IDbSerializer` with the PRD buffer-based contract (verbatim from
  PRD line 367).
- Modernize the value types with the verified modern .NET idioms:
  `DbTimestamp` gains `ISpanFormattable`, `IUtf8SpanFormattable`,
  `ISpanParsable`, generic-math operator interfaces and a primary
  constructor; `DbNil` gains `IEqualityOperators`; the new `DbDate` and
  `DbLocalDateTime` ship with `IEqualityOperators`,
  `IComparisonOperators`, `ISpanFormattable`, `IUtf8SpanFormattable` and
  primary constructors.
- Rewrite the six existing Core test files against the new contract
  (behavior-preserving tests migrate; behavior-changing tests are rewritten
  to assert the new ordering), and add the allocation-free key-encoding
  test that M1's exit criteria require.
- Update PRD.md M1 status row (done via this spec, as the tripwire
  requires; the one off-lists touch, approved with this spec).

Out of scope (aggressively):

- A registry implementation. None exists in the tree; the `IDbTypeRegistry`
  interface compiles unchanged against the new contract (it never calls the
  retired members), and the PRD does not specify an implementation.
- MessagePack itself, the canonical format, and the field-path reader. The
  new `IDbSerializer` is a contract; no implementation ships this cycle. No
  new dependency (MessagePack-CSharp) is added.
- AOT/trimming analyzer enablement (`IsAotCompatible`, PublishAot). Build
  shape change; PRD lists it as a goal but not as a type-system API.
- `IDbType<T>` `where T : notnull` tightening. PRD does not ask for it; the
  current implementations accept nulls deliberately (`IsValid` gates them).
- Null handling semantics in `TryWriteIndexKey` (PRD does not specify;
  current `GetIndexKey` throws on null strings - that behavior is preserved
  and documented). `Compare` throws `ArgumentNullException` on null for
  `DbStringType` and `DbBytesType` (today `string.CompareOrdinal` orders
  null first; pinned because `IsValid(null)` is false and the B+ tree never
  compares nulls).
- Docs for `DbTimestamp`'s new parse/format methods beyond the canonical
  format grammar (documented on `Parse`/`TryParse`) and the `<inheritdoc/>`
  that the style rules require.
- Conversions between `DbDate`/`DbLocalDateTime` and
  `DateTime`/`DateTimeOffset`. The PRD specifies no members for the new
  types; this spec introduces the minimal set (constructor, accessors,
  comparison, formatting), and extending them is a separate decision.
- PRD's open question on unsigned 64-bit integers (line 362). Deliberately
  untouched.

## Approach

One Track L cycle, three projects' worth of edits but one coherent contract
change: the new `IDbType<T>` + the nine type implementations + the serializer
contract, in `AsteropeDb.Core`/`AsteropeDb.Serialization`, the test rewrite
in `AsteropeDb.Core.Tests`, then the PRD status row. The encoding rules are
transcribed verbatim from PRD lines 332-353 - no design is invented here;
the "design" work is the API shape and the test strategy.

Key encoding is allocation-free by construction: every implementation
writes its fixed-length tag+payload into the caller's `Span<byte>` with
`BinaryPrimitives.TryWriteInt64BigEndian`/`TryWriteUInt64BigEndian`/
`TryWriteInt32BigEndian`; the two variable-length types (`String`, `Bytes`)
write UTF-8 bytes with `0x00` escaped as `0x00 0xFF` and a `0x00` terminator,
exactly as PRD line 341-342 specifies. Escape handling scans for `0x00`
only - the single byte the PRD escapes - via a preallocated
`private static readonly SearchValues<byte>` field on the singleton type;
no allocation per call. The two variable-length encoders differ in one
respect the spec pins down: `DbBytesType` copies from the caller's `byte[]`
into `destination`, so a single forward pass that copies and escapes
together is correct; `DbStringType` produces its UTF-8 with
`Encoding.UTF8.GetBytes(value.AsSpan(), destination[1..])`, where source and
destination are the same buffer, so the escapes are expanded right-to-left
in place (the write index never overtakes the read index) before the
terminator is appended. No scratch buffer and no second `GetBytes` on either
path. A forward in-place expansion is wrong - the escape write lands at or
past the read position and clobbers unread bytes; recorded under Rejected
alternatives.

Generic math supplies every operator `DbTimestamp`/`DbNil` currently
declare by hand via `IEqualityOperators<,>` and `IComparisonOperators<,>`;
the new `DbDate`/`DbLocalDateTime` structs declare the same operator set
through the same interfaces. The operators must still be *declared* in the
type (CS0535, verified) but become six one-line expression-bodied wrappers -
the readable form, and the one the tests exercise.

The value types move to primary constructors with a guarded property
initializer. The guard stays a one-liner (`nanoseconds is >= 0 and <= 99
? nanoseconds : throw new ArgumentOutOfRangeException(...)`) so the class
level stays `readonly struct` without field initializers or initialization
order hazards - verified to compile and throw with the correct param name.

`DbTimestamp` gains `ISpanFormattable`, `IUtf8SpanFormattable` and
`ISpanParsable` - the current struct has none of them. Its canonical text
format is the shape `ToString` already produces today, pinned to the
invariant culture: `yyyy-MM-ddTHH:mm:ss.fffffff` + `Z`, with two further
nanosecond digits inserted before the `Z` when `Nanoseconds != 0` (7 or 9
fractional digits; fixed length 28 or 30 characters). `TryFormat` with a
null or empty format emits exactly that: the `DateTime` part is formatted
with `ToDateTime().TryFormat(..., "yyyy-MM-ddTHH:mm:ss.fffffff",
CultureInfo.InvariantCulture)` (the "O" pattern without the trailing `Z`),
then the struct appends the two nanosecond digits when `Nanoseconds != 0`
and the `Z` - so sub-tick precision survives a round trip. A
caller-supplied format delegates to `DateTime.TryFormat` with the caller's
format and provider, documented as tick-precision. `ToString()` becomes
`string.Create` over that fixed-length `TryFormat`, so the UTF-16 path is
allocation-light, with `IUtf8SpanFormattable` giving the allocation-free
UTF-8 path. `Parse`/`TryParse` (string and span overloads) accept the
canonical grammar, documented on `Parse`. `DbDate`/`DbLocalDateTime`
implement `ISpanFormattable`/`IUtf8SpanFormattable` only (no parsing - the
PRD does not specify parse syntax for them), formatting invariant "O"
through `DateOnly`/`DateTime` respectively.

The `GetHashCode` members disappear from `IDbType<T>` and all nine
implementations. This removes the trap where randomized string hashing
leaks through a public contract. Nothing in the tree calls them.

String comparison goes code-point-based: a dual rune enumeration
(`StringRuneEnumerator` values, public and returning structs - safe to hold)
compares code points in order, tie-breaks on code-point count, and throws
`ArgumentNullException` on null. For equal strings this is O(n); an ordinal
fast-path is a separate cycle if M2 benchmarks ever demand one - it is not
needed to be correct.

## Contracts

All signatures below are compiled and verified against SDK 10.0.101,
`LangVersion=latest`, C# 14, net10.0, in a sandbox probe on 2026-09-30.
`System.Numerics` must be imported wherever the operator interfaces appear.
Additions from this revision were verified the same way:
`DateTime.TryFormat` with an empty format emits the culture-dependent "G"
pattern at seconds precision (`01/01/2023 12:00:00` invariant,
`01.01.2023 12:00:00` de-DE); `DateTime.TryParseExact` with a `.fffffff`
pattern rejects a 9-digit fraction; `new DateTime(ticks, kind)` throws
`ArgumentOutOfRangeException` outside years 1-9999;
`string.CompareOrdinal("\uFFFF", "\U0001F600")` returns a positive value
while the UTF-8 encodings order the other way.

### IDbType<T> (replaced, `src/AsteropeDb.Core/IDbType.cs`)

```csharp
public interface IDbType<in T>
{
    string TypeName { get; }

    int Compare(T left, T right);

    bool IsValid(T value);

    /// <summary>Gets the upper bound on the encoded key length for this
    /// value. The destination span passed to <see cref="TryWriteIndexKey"/>
    /// must be at least this long for the call to succeed.</summary>
    /// <param name="value">The value to measure.</param>
    /// <returns>The maximum number of bytes the encoded index key for
    /// <paramref name="value"/> can occupy, including the type tag.</returns>
    int GetMaxEncodedLength(T value);

    /// <summary>Writes the self-delimiting, tag-prefixed index key for
    /// <paramref name="value"/> into <paramref name="destination"/>.
    /// Returns false (bytesWritten = 0) when the span is too small; throws
    /// for values that cannot be encoded (NaN floats, null strings, null
    /// bytes).</summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="destination">The buffer to write into.</param>
    /// <param name="bytesWritten">When this method returns, contains the
    /// number of bytes written, or zero when the method returned
    /// <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the key was written;
    /// <see langword="false"/> when <paramref name="destination"/> is too
    /// small. Never writes partially.</returns>
    /// <exception cref="ArgumentNullException">Thrown when
    /// <paramref name="value"/> is null (string and bytes types).</exception>
    /// <exception cref="ArgumentException">Thrown when the value cannot be
    /// encoded (NaN floats).</exception>
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

    public long Ticks { get; }                 // 100ns ticks since the Unix
                                               // epoch (1970-01-01T00:00:00Z),
                                               // unchanged from today
    public long Nanoseconds { get; }           // guarded: 0..99, throws
                                               // ArgumentOutOfRangeException
                                               // with nameof(nanoseconds)

    public static DbTimestamp FromDateTime(DateTime dateTime);
        // rejects DateTimeKind.Unspecified (ArgumentException);
        // Local converts to UTC; Utc used directly
    public static DbTimestamp FromDateTimeOffset(DateTimeOffset dateTimeOffset);
    public DateTime ToDateTime();              // Kind = Utc; drops sub-tick
                                               // nanoseconds (DateTime is
                                               // 100ns); throws
                                               // ArgumentOutOfRangeException
                                               // outside years 1-9999
    public DateTimeOffset ToDateTimeOffset();  // offset zero
    public int CompareTo(DbTimestamp other);   // ticks then nanos
    public bool Equals(DbTimestamp other);
    public override bool Equals(object? obj);
    public override int GetHashCode();         // HashCode.Combine
    public override string ToString();         // string.Create over the
                                               // fixed-length canonical
                                               // TryFormat: 28 chars, or
                                               // 30 with nanoseconds
    public string ToString(string? format, IFormatProvider? formatProvider);
    public static DbTimestamp Parse(string s, IFormatProvider? provider);
        // canonical grammar, invariant; see the format note below
    public static DbTimestamp Parse(ReadOnlySpan<char> s,
        IFormatProvider? provider);
    public static bool TryParse(string? s, IFormatProvider? provider,
        out DbTimestamp result);
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider,
        out DbTimestamp result);
    public bool TryFormat(Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider);
        // null/empty format -> the canonical format (invariant
        // "yyyy-MM-ddTHH:mm:ss.fffffff" via ToDateTime().TryFormat, two
        // nanosecond digits appended when Nanoseconds != 0, then "Z");
        // a caller format -> DateTime.TryFormat with the caller's format
        // and provider (tick precision)
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider);
    // all six comparison operators, declared, one-line expression-bodied
    // (required by IComparisonOperators/IEqualityOperators - CS0535,
    // verified); default interface methods supply the rest
}
```

Canonical text format (normative; the grammar `Parse`/`TryParse` accept):
`yyyy-MM-ddTHH:mm:ss.fffffffZ` when `Nanoseconds == 0`, otherwise
`yyyy-MM-ddTHH:mm:ss.fffffff` + two nanosecond digits + `Z` (9 fractional
digits) - today's `ToString` shape, pinned to `CultureInfo.InvariantCulture`.
The layout is fixed-length (28 or 30 characters), which is what makes
`string.Create` in `ToString` exact. Parsing is invariant, accepts an
optional trailing `Z` (absent means UTC, via
`DateTimeStyles.AssumeUniversal | AdjustToUniversal`), and accepts either
fractional form - a single `TryParseExact` pattern with `.fffffff` rejects
the 9-digit form (verified), so the parser tries both patterns and takes
the last two of nine fractional digits as nanoseconds. The format is
limited to the `DateTime` domain (years 1-9999): ticks outside it throw
`ArgumentOutOfRangeException` from `ToDateTime`, and `ToDateTime` drops
sub-tick nanoseconds. `DbTimestamp`'s tick domain is the full `Int64`;
comparison and key encoding cover all of it.

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
  tie-break (PRD line 346). Throws `ArgumentNullException` on null (today
  `string.CompareOrdinal` orders null first; `IsValid(null)` is false, so
  the B+ tree never compares nulls - the contract is pinned for direct
  callers). The discriminating case for the code-point requirement is a
  high BMP character against an astral one: `"\uFFFF"` sorts before
  `"\U0001F600"` by code point and by UTF-8 key order, but after it by
  UTF-16 code units.
- `GetMaxEncodedLength(string value)`: `(3 * value.Length) + 2` (safe upper
  bound: a BMP UTF-16 code unit can encode to 3 UTF-8 bytes, so 3n covers
  every input; the terminator and tag add 2).
- `TryWriteIndexKey`: `ArgumentNullException.ThrowIfNull(value)` (verified);
  returns false with `bytesWritten = 0` when the destination is shorter
  than `GetMaxEncodedLength(value)`. Otherwise writes the `0x08` tag,
  produces the UTF-8 bytes with `Encoding.UTF8.GetBytes(value.AsSpan(),
  destination[1..])` - source and destination are the same buffer - then
  expands the `0x00` escapes right-to-left in place (the write index never
  overtakes the read index) and appends the `0x00` terminator. No scratch
  buffer, no second `GetBytes`, no allocation.
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

    /// <summary>Ordering is lexicographic unsigned byte order. Throws
    /// <see cref="ArgumentNullException"/> when either value is null.</summary>
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

`Compare` throws `ArgumentNullException` on null (pinned; `IsValid(null)`
is false). `TryWriteIndexKey` copies from the caller's array into
`destination` in a single forward pass that escapes each `0x00` as it
copies - source and destination are different buffers, so the forward pass
is correct, unlike `DbStringType`'s in-place expansion.

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
        // delegates to ToDateOnly().TryFormat(..., "O",
        // CultureInfo.InvariantCulture)
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider);
        // same, UTF-8 path
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
    public DbLocalDateTime(long ticks);         // 100ns ticks since
                                                // 0001-01-01 (DateTime.Ticks
                                                // semantics, Kind =
                                                // Unspecified) - deliberately
                                                // not the Unix epoch of
                                                // DbTimestamp
    public long Ticks { get; }                  // primary ctor param

    public int CompareTo(DbLocalDateTime other);  // Ticks.CompareTo
    public bool Equals(DbLocalDateTime other);
    public override bool Equals(object? obj);
    public override int GetHashCode();            // Ticks.GetHashCode()
    public override string ToString();
        // invariant "O" over new DateTime(Ticks, DateTimeKind.Unspecified)
    public string ToString(string? format, IFormatProvider? formatProvider);
    public bool TryFormat(Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider);
        // delegates to that DateTime, invariant "O"
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider);
        // same, UTF-8 path
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

`ToString()` and both `TryFormat` overloads are pinned to
`CultureInfo.InvariantCulture`. Formatting is limited to the `DateTime`
domain (years 1-9999) - the `DateTime` constructor throws outside it -
while key encoding covers the full `Int64` tick domain; same
known-limitation class as `DbTimestamp`.

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

Verbatim from PRD line 367 (`using System.Buffers` added). `Name` and
`CanSerialize` stay: the PRD replaces only the two `byte[]` methods and is
silent on both, and deleting them is not this cycle's decision.

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
  that fail on today's code (negative floats, pre-1970 timestamps) and, for
  strings, the astral pair `"\uFFFF"` vs `"\U0001F600"`, where today's
  `CompareOrdinal` disagrees with the UTF-8 key order. This is the bug class
  the tripwire names; it must have a test that fails before the rework and
  passes after.
- **The C# 14 compiler (Roslyn) crashes on an unprobed construct.** Verified
  in sandbox: the only crash in the probe set is the `extension` block
  (`extension(int x) { ... }`), which produces a Roslyn NRE. It is therefore
  banned from this cycle and noted as such. Everything else in the contracts
  compiles and runs on SDK 10.0.101.
- **`TryWriteIndexKey` on too-small spans.** Contract: returns false with
  `bytesWritten = 0`, never partial writes. A test per type asserts the false
  path and that the buffer is untouched. Consumers (future B+ tree) pre-size
  with `GetMaxEncodedLength`.
- **Ticks outside the `DateTime` domain.** `ToDateTime()` constructs
  `new DateTime(DateTime.UnixEpoch.Ticks + Ticks, DateTimeKind.Utc)`, which
  throws `ArgumentOutOfRangeException` outside years 1-9999, and drops
  sub-tick nanoseconds (`DateTime` is 100 ns). `DbTimestamp`'s tick domain
  is the full `Int64`; `Compare` and key encoding cover all of it, so
  `ToString`/`TryFormat` throw rather than emit a truncated string.
  `DbLocalDateTime` has the same limitation via `new DateTime(Ticks,
  DateTimeKind.Unspecified)`. Documented known limitation; format tests use
  in-range values.
- **The canonical format drifts from the parser.** `ToString` and `Parse`
  are pinned to the same grammar (invariant; 7 or 9 fractional digits;
  optional `Z`). Guard: round-trip tests over values with and without
  nanoseconds. Verified trap: a single `TryParseExact` pattern with
  `.fffffff` rejects the 9-digit form, so the parser tries both patterns.
- **Escape logic mis-counts bytes or expands in the wrong direction** in
  `DbStringType`. Guard: exact-length tests with embedded nulls, the
  write-then-assert-exact-bytes pattern, and the pinned algorithm - UTF-8
  into `destination[1..]`, escapes expanded right-to-left in place,
  terminator last. A forward in-place expansion clobbers unread bytes.
- **A `GetHashCode` call site breaks when the member is deleted.** Verified:
  none exist outside the tests (grep on 2026-09-30); the tests are rewritten
  in the same cycle.
- **xunit v2 + `CancellationToken` test parameters.** Verified: xunit 2.9.3's
  analyzer warns on `[Fact]` methods with parameters - it does NOT silently
  pass or fail. New tests therefore carry no `CancellationToken` parameters
  (the repo's convention for plain xUnit 2 tests; keeps the build
  warning-free).
- **StyleCop on C# 14 syntax.** The pinned StyleCop (1.2.0-beta.556) predates
  C# 14; probes show it analyzes the constructs fine (primary constructors,
  collection expressions, field properties compile clean under the repo
  ruleset). If a specific idiom trips an SA rule, the plan guards it by
  keeping the gate as the check and reporting any new warning as a failed
  step.

## Testing strategy

Plain xUnit only (repo rule), `tests/AsteropeDb.Core.Tests`, one test class
per production type. Baseline at spec time: 129 tests green (126 Core + 1
each in the Storage/Network/Serialization placeholder suites); the plan's
preconditions restate it. The rewrite covers the six existing files plus
three new ones (`DbBytesTypeTests`, `DbDateTests`, `DbLocalDateTimeTests`).

- Every type: `TryWriteIndexKey` happy path asserting exact tag+payload
  bytes; too-small-destination returns false with untouched buffer; ordering
  property (keys byte-compare in value order across a spread including
  boundary values - negative/positive floats, pre/post-1970 timestamps,
  astral strings); `GetMaxEncodedLength` >= actual for a sample of values.
- Allocation-free encoding (M1 exit criterion, PRD line 515): for every
  type, the `GC.GetAllocatedBytesForCurrentThread()` delta across a
  `TryWriteIndexKey` call into a `GetMaxEncodedLength`-sized destination is
  zero, after one warm-up call - string and bytes paths included, since
  neither uses a scratch buffer.
- Cross-type and composite-key properties (PRD lines 328, 344): one test
  asserts the tag order nil < boolean < integer < float < timestamp < date
  < localdatetime < string < bytes; one asserts composite keys are
  self-delimiting - `key("ab") || key("c")` differs from
  `key("a") || key("bc")` - so concatenated keys never collide across
  value splits.
- String/bytes: exact escaped bytes for values containing `0x00`/`0xFF`,
  including that a literal `0xFF` passes through unescaped; compare tests
  for the code-point ordering pinned to the discriminating pair `"\uFFFF"`
  vs `"\U0001F600"` (fails on today's `CompareOrdinal`, which disagrees
  with the UTF-8 key order), plus a `Compare`-agrees-with-key-order
  assertion for it; bytes type ordering via `SequenceCompareTo` semantics.
- The two regression pairs that fail on today's code:
  `DbFloatType` keys for `{ 0.0, 1.0, -1.0 }` must sort `-1.0, 0.0, 1.0`;
  `DbTimestampType` keys for pre/post-epoch timestamps must sort
  pre-epoch first.
- Value types: operator behavior via the generic-math interfaces (declared
  operators tested directly), `TryFormat`/`TryParse` round-trips against
  the canonical grammar (`DbTimestamp`, with and without nanoseconds;
  7- and 9-digit fractions), `FromDateTime` Unspecified-kind rejection.
- `DbTimestampTypeTests` stays the canonical test file for both the struct
  and its type (existing convention, one file per production type).
- Serialization: `IDbSerializer` has no implementation this cycle, so the
  contract is not unit-tested beyond compilation (nothing to assert; the
  placeholder suite keeps its single test). The next cycle that adds a
  MessagePack implementation brings its own tests.

## Rejected alternatives

- **Keep `GetIndexKey` alongside `TryWriteIndexKey`**: doubles the surface
  every future key type must implement; the PRD explicitly says the API "no
  longer returns an allocated `ReadOnlySpan<byte>`"; deletion confirmed.
- **Code-point compare via `string.CompareOrdinal` + documentation**: wrong
  per PRD line 346; astral characters misorder in indexes; rejected.
- **`DbNil : IComparisonOperators<...>`**: the struct has no order; the
  interface would advertise one; rejected.
- **`DbBytesType : IDbType<ReadOnlyMemory<byte>>` or span overloads**: a
  different generic instantiation changes the registry's type-key story;
  out of scope; `byte[]` is the contract today.
- **`DbTimestamp.Parse` via `DateTime.Parse`**: culture- and
  format-dependent; the struct's canonical format is well-defined;
  rejected.
- **Delegate `DbTimestamp.TryFormat` to `DateTime.TryFormat` with the
  default (null) format**: verified to produce the culture-dependent "G"
  pattern at seconds precision (`01/01/2023 12:00:00` invariant,
  `01.01.2023 12:00:00` de-DE), dropping sub-second ticks and nanoseconds,
  and not round-trippable; rejected in favour of the pinned canonical
  format.
- **Forward in-place escape expansion for `DbStringType`**: the escape
  write lands at or past the read position and clobbers unread bytes
  (traced on `"a\0b"`); the in-place expansion runs right-to-left;
  rejected.
- **C# 14 `extension` blocks** (would be the most visible "bleeding edge"):
  Roslyn NRE on this SDK (verified in sandbox); banned this cycle; revisit
  when the SDK ships a fix.
- **Primary-constructor + field-based `Nanoseconds` validation via a
  method**: property-initializer throw is the verified pattern; a helper
  method adds a call site without adding clarity.
- **`IDbType<T>` keeps `GetHashCode` but documented as ephemeral**: the
  member invites persistence (PRD line 363 forbids it); dropping it
  confirmed.
