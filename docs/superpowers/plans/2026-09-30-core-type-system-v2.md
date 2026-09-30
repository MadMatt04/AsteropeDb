# Core Type System v2 - Implementation Plan

Design: docs/superpowers/specs/2026-09-30-core-type-system-v2-design.md
Target framework: net10.0 (C# 14, `LangVersion=latest`, SDK 10.0.101)
Test project: tests/AsteropeDb.Core.Tests (all four suites gate)

## Preconditions

```
dotnet build AsteropeDb.slnx
# expected: build succeeds, NO warnings (baseline: clean as of 2026-09-30)
dotnet test AsteropeDb.slnx
# expected: 129/129 green (126 Core + 1 Network + 1 Storage + 1 Serialization)
```

If the baseline drifts, stop and report - do not proceed on a dirty baseline.

## Verified API facts (sandbox-probed 2026-09-30, SDK 10.0.101)

Everything below compiles and runs on this exact machine. `@coder` must not
substitute or re-verify these:

- Generic-math operator interfaces are `System.Numerics.IEqualityOperators<TSelf,TOther,TResult>`
  and `IComparisonOperators<TSelf,TOther,TResult>`. They provide the operators
  via default interface methods, BUT the type must still *declare* the six
  (CS0535 otherwise) - the declarations are what the interface members bind
  to, and what tests call.
- `BinaryPrimitives.TryWriteInt64BigEndian(Span<byte>, long)` -> bool,
  `TryWriteUInt64BigEndian(Span<byte>, ulong)` -> bool,
  `TryWriteInt32BigEndian(Span<byte>, int)` -> bool. All exist.
- `Encoding.UTF8.GetBytes(ReadOnlySpan<char>, Span<byte>)` -> int (no
  allocation). `Encoding.UTF8.GetMaxByteCount(int)` -> int.
- `SearchValues<byte>` / `SearchValues.Create(ReadOnlySpan<byte>)` /
  `.Contains(byte)` exist (`using System.Buffers`).
- `string.EnumerateRunes()` returns `System.Text.StringRuneEnumerator`, a
  public struct with `MoveNext()`, `Current` (Rune), and implements
  `IEnumerable<Rune>`. `Rune` has `Value`, `CompareTo(Rune)`,
  `DecodeFromUtf16(ReadOnlySpan<char>, out Rune, out int)`,
  `EncodeToUtf8(Span<byte>)` -> int, `Utf8SequenceLength`.
- `DateOnly.DayNumber` (int, days since 0001-01-01), `DateOnly.FromDayNumber(int)`,
  `DateOnly.MaxValue.DayNumber` (= 3652058), `DateOnly.TryFormat(Span<char>,
  out int, "O", provider)` (writes "2023-01-01"), `DateOnly.ToString("O",
  provider)`. `DateTime` implements `IUtf8SpanFormattable`.
- `DbTimestamp : ISpanParsable<DbTimestamp>` etc. compile; the interfaces'
  members are `Parse(string, IFormatProvider?)`, `Parse(ReadOnlySpan<char>,
  IFormatProvider?)`, `TryParse(string?, IFormatProvider?, out T)`,
  `TryParse(ReadOnlySpan<char>, IFormatProvider?, out T)`.
- `ISpanFormattable.ToString(string?, IFormatProvider?)` and
  `TryFormat(Span<char>, out int, ReadOnlySpan<char>, IFormatProvider?)`;
  `IUtf8SpanFormattable.TryFormat(Span<byte>, out int, ReadOnlySpan<char>,
  IFormatProvider?)` (explicit, default implementation delegates to the
  char-based one).
- `ArgumentNullException.ThrowIfNull(object?, string?)` exists (net6+).
  `ArgumentOutOfRangeException.ThrowIfNotBetween` does NOT exist - write the
  range guard manually (probed: CS0117).
- Primary constructors + property-initializer `throw` in a readonly struct:
  `public long Nanoseconds { get; } = nanoseconds is >= 0 and <= 99 ? nanoseconds :
  throw new ArgumentOutOfRangeException(nameof(nanoseconds), nanoseconds, "...");`
  compiles and throws with the right param name. Primary-ctor params with
  default values work (`DbTimestamp(long ticks, long nanoseconds = 0)`).
- `DateTime.TryParseExact(string, string, IFormatProvider?, DateTimeStyles,
  out DateTime)` with `DateTimeStyles.AssumeUniversal | AdjustToUniversal`
  returns Kind Utc (probed).
- C# 14 `extension` blocks **crash Roslyn on this SDK** (NRE, verified).
  Banned this cycle; do not use. (`field` properties, `params` span
  collections, collection expressions all work, but this plan uses none.)
- xunit 2.9.3: `[Fact]` methods with a `CancellationToken` parameter produce
  analyzer xUnit1001. Do NOT add `CancellationToken` parameters to the tests
  in this cycle (repo convention; keeps the build warning-free).

## Style constants applied to every new file

- MIT license header (copy from any existing source file; keep the 2025
  copyright year as the repo does).
- File-scoped namespace `AsteropeDb.Core;` (tests: `AsteropeDb.Core.Tests;`).
- Explicit usings, alphabetized (System namespaces first). No `var`.
  Target-typed `new()` where the type is explicit. XML docs on every public
  and internal member, sentences end with periods. LF line endings.
- 4-space indent, Allman braces.

## Red-contract migration note

Steps 2-5 leave the tree red by design: the `IDbType<T>` contract change is
the red test for the rework, and the old test files reference the retired
members until Step 7. Each step's gate states exactly which redness is
expected. The tree returns to full green at Step 7's gate.

## Step 1 — Add the two ordering regression tests (red on assertion)

**Test first:** add exactly two tests to the existing files (both must FAIL
on their assertion against today's code - this is the tripwire regression
made visible):

- `tests/AsteropeDb.Core.Tests/DbFloatTypeTests.cs`:
  `GetIndexKey_ShouldSortNegativeValuesBeforePositiveValues` - keys for
  `{ -1.0, 0.0, 1.0 }` via the existing `GetIndexKey`, byte-compared in that
  order. FAILS today (the known float ordering bug).
- `tests/AsteropeDb.Core.Tests/DbTimestampTypeTests.cs`:
  `GetIndexKey_ShouldSortPreEpochBeforePostEpoch` - keys for
  `new DbTimestamp(-1_000_000L)` and `new DbTimestamp(1_000_000L)` via the
  existing `GetIndexKey`, assert pre-epoch byte-compares first. FAILS today
  (the known timestamp ordering bug).

**Gate:** `dotnet test tests/AsteropeDb.Core.Tests --filter
"FullyQualifiedName~GetIndexKey_ShouldSort"` - expected RED: exactly 2
failed, both on their assertions. Report the failure output verbatim.

**Done when:** the two tests are observed red for the documented reason
(wrong ordering), not for compile errors.

## Step 2 — Replace IDbType<T> with the PRD contract

**Test first:** none compiles against the new contract yet; the contract
change itself is the red test for Steps 3-5.

**Then implement:** rewrite `src/AsteropeDb.Core/IDbType.cs` to exactly:

```csharp
public interface IDbType<in T>
{
    string TypeName { get; }

    int Compare(T left, T right);

    bool IsValid(T value);

    int GetMaxEncodedLength(T value);

    bool TryWriteIndexKey(T value, Span<byte> destination, out int bytesWritten);
}
```

Keep the existing `<summary>` and `<typeparam>` docs; write full `<summary>`
plus `<param>`/`<returns>` XML docs for the two new members (wording in the
design spec's Contracts section). Keep the contravariant `in T` and
`using System;`. Do not touch `IDbTypeRegistry.cs`.

**Gate:** `dotnet build AsteropeDb.slnx` - expected RED: CS0535/CS0738 errors
in the six `Db*Type` classes (`DbBooleanType`, `DbIntegerType`,
`DbFloatType`, `DbStringType`, `DbNilType`, `DbTimestampType`) and the six
Core test files (references to `GetIndexKey`/`GetHashCode`). Report the error
list verbatim.

**Done when:** the build fails for exactly those reasons and no other
(no StyleCop warnings in the failure output, no errors outside Core).

## Step 3 — Rework the six existing Db*Type implementations

**Test first:** none (tree is red by contract; this step restores it).

**Then implement:** edit exactly these six files (all under
`src/AsteropeDb.Core/`), replacing the `GetIndexKey`/`GetHashCode` members
with `GetMaxEncodedLength`/`TryWriteIndexKey`, keeping `TypeName`, `Compare`,
`IsValid`, the singleton `Instance` and the private ctor unchanged:

- `DbBooleanType.cs` - tag 0x02: `GetMaxEncodedLength` -> 2;
  `TryWriteIndexKey`: guard `destination.Length >= 2` else return false with
  `bytesWritten = 0`; then `destination[0] = 0x02;
  destination[1] = value ? (byte)1 : (byte)0; bytesWritten = 2;`.
- `DbIntegerType.cs` - tag 0x03: length 9; guard `destination.Length >= 9`;
  `destination[0] = 0x03;` then
  `BinaryPrimitives.TryWriteInt64BigEndian(destination[1..], value ^ long.MinValue);`
  `bytesWritten = 9`. Keep the sign-flip comment.
- `DbFloatType.cs` - tag 0x04: length 9. NaN -> `ArgumentException` (as
  today). Normalize `-0.0` to `+0.0` FIRST (`value = value == 0.0 ? 0.0 :
  value` - this also normalizes the bits path). Then
  `long bits = BitConverter.DoubleToInt64Bits(value);` and
  `long transformed = bits >= 0 ? bits ^ long.MinValue : ~bits;` (NOTE the
  sign-bit flip on the positive path - this is the bug fix; today's code
  writes positive values raw). `TryWriteInt64BigEndian(destination[1..], transformed)`.
- `DbStringType.cs` - tag 0x08: `GetMaxEncodedLength(string value)` ->
  `(3 * value.Length) + 2`. `TryWriteIndexKey(string value, Span<byte>
  destination, out int bytesWritten)`: `ArgumentNullException.ThrowIfNull(value);`
  then if `destination.Length < GetMaxEncodedLength(value)` return false,
  `bytesWritten = 0`. Else `destination[0] = 0x08;`,
  `Encoding.UTF8.GetBytes(value.AsSpan(), destination[1..])` writing
  `utf8Bytes` into a scratch span, then a single forward pass over
  `scratch[..utf8Bytes]` escaping each `0x00` as `0x00 0xFF`: for index i
  and write index w, copy the byte, and when the byte is 0x00 write an extra
  0xFF after it (`if (scratch[i] == 0x00) { scratch[w++] = 0xFF; }` - one
  loop, copy+escape together). Terminate with `0x00`. `bytesWritten` =
  tag(1) + escapedBytes + terminator(1).
  `Compare` becomes the code-point comparison from the design spec (dual
  `StringRuneEnumerator`). Delete `GetHashCode`.
- `DbNilType.cs` - tag 0x01: length 1; guard `destination.Length >= 1`;
  write `0x01`, `bytesWritten = 1`. Delete `GetHashCode`.
- `DbTimestampType.cs` - tag 0x05: length 10; guard `>= 10`;
  `destination[0] = 0x05;`
  `BinaryPrimitives.TryWriteInt64BigEndian(destination[1..], value.Ticks ^ long.MinValue);`
  then `destination[9] = (byte)value.Nanoseconds;` (the sign-bit flip on
  Ticks is the second bug fix - today's code writes raw ticks). Delete
  `GetHashCode`.

The `DbTimestamp` struct and `DbNil` struct in those two files keep their
current hand-written operators/formatting for now - Step 4 replaces them.

**Gate:** `dotnet build AsteropeDb.slnx` - expected RED still, but the errors
must now be ONLY in the test files (references to `GetIndexKey`/`GetHashCode`
in `tests/AsteropeDb.Core.Tests/*.cs`). No errors in `src/`. Report the error
list.

**Done when:** `dotnet build src/AsteropeDb.Core/AsteropeDb.Core.csproj`
succeeds with zero warnings (build the Core project alone to prove the
production side is clean).

## Step 4 — Modernize DbTimestamp and DbNil (C# 14 idioms)

**Test first:** none for the new operators (declared + default-interface
methods - covered by existing operator tests in Step 7's rewrite). Existing
`DbTimestamp` behavior (ctors, conversions, operators, formatting) must not
regress - the current tests prove that after Step 7; before Step 7 the Core
tests do not compile, so this step's gate is the Core-project build.

**Then implement:** in `src/AsteropeDb.Core/DbTimestamp.cs`:

- Add `using System.Buffers;`, `using System.Globalization;`,
  `using System.Numerics;` (keep `System`, `System.Buffers.Binary`).
- Change the struct declaration to implement
  `IEqualityOperators<DbTimestamp, DbTimestamp, bool>` and
  `IComparisonOperators<DbTimestamp, DbTimestamp, bool>` (keep
  `IEquatable<DbTimestamp>`, `IComparable<DbTimestamp>`), plus
  `ISpanFormattable`, `IUtf8SpanFormattable`, `ISpanParsable<DbTimestamp>`.
- Convert to a primary constructor: `public readonly struct DbTimestamp(long
  ticks, long nanoseconds = 0) : ...interfaces`. Replace the two properties
  with primary-ctor params exposed as `public long Ticks { get; }` and
  `public long Nanoseconds { get; } = nanoseconds is >= 0 and <= 99 ?
  nanoseconds : throw new ArgumentOutOfRangeException(nameof(nanoseconds),
  nanoseconds, "Nanoseconds must be between 0 and 99.");` (delete the
  hand-written ctor and the `MaxNanoseconds` constant - inline the bound).
- Rewrite the six operators as one-line expression-bodied declarations
  (required by the interfaces; keep the existing semantics: `==`/`!=` via
  `Equals`, the four comparisons via `CompareTo`). Keep their XML docs.
- `FromDateTime`: reject `DateTimeKind.Unspecified` with
  `ArgumentException`; Local -> `ToUniversalTime()`; Utc direct (today's
  Unspecified-as-UTC behavior is the bug being fixed - see design spec).
- Add `ISpanFormattable`/`IUtf8SpanFormattable`/`ISpanParsable` members per
  the design spec's DbTimestamp contract. `TryFormat(char)` and
  `TryFormat(byte)` delegate to `ToDateTime()`'s `TryFormat` with the
  invariant "O" format (probed output shape:
  "2023-01-01T12:00:00.0000000Z"); append the two nanosecond digits before
  the `Z` when `Nanoseconds != 0` (mirror today's `ToString` shape).
  `Parse`/`TryParse` (string + span overloads) accept
  `yyyy-MM-ddTHH:mm:ss` with optional `.fffffff` and optional `Z`, via
  `DateTime.TryParseExact` with `DateTimeStyles.AssumeUniversal |
  AdjustToUniversal` and `CultureInfo.InvariantCulture` (probed: returns
  Kind Utc). `ToString()` -> `string.Create` over `TryFormat(default, null)`.
  `GetHashCode` -> `HashCode.Combine(Ticks, Nanoseconds)` (unchanged).
  See the design spec for the exact member list.
- In `DbTimestampType` (same file): no further changes beyond Step 3.

In `src/AsteropeDb.Core/DbNil.cs`: add `using System.Numerics;`, implement
`IEqualityOperators<DbNil, DbNil, bool>` (keep `IEquatable<DbNil>`), declare
`==`/`!=` as today (expression-bodied `=> true` / `=> false`), keep
`Value`/`Equals`/`GetHashCode`/`ToString`. Do NOT add comparison interfaces.

**Gate:** `dotnet build src/AsteropeDb.Core/AsteropeDb.Core.csproj` - zero
warnings. `dotnet build AsteropeDb.slnx` - RED still, errors only in the
tests. Report both.

**Done when:** Core project builds clean; slnx failure list contains only
test-side errors.

## Step 5 — Add DbBytesType, DbDate, DbLocalDateTime (production only)

**Test first:** none (new types; their tests are Step 7's job).

**Then implement:** three new files in `src/AsteropeDb.Core/` with the MIT
header and full style rules:

- `DbBytesType.cs`: `public sealed class DbBytesType : IDbType<byte[]>` with
  `public static DbBytesType Instance { get; }`, private ctor,
  `TypeName => "bytes"`, `Compare(byte[] left, byte[] right)` ->
  `((ReadOnlySpan<byte>)left).SequenceCompareTo(right)` with
  `ArgumentNullException.ThrowIfNull` on both, `IsValid` -> `value != null`,
  `GetMaxEncodedLength(byte[] value)` -> `value.Length + zeroCount + 2`
  (count the 0x00 bytes - a loop or `SearchValues<byte>` scan),
  `TryWriteIndexKey(byte[] value, Span<byte> destination, out int
  bytesWritten)`: throw on null, false on too small, else tag `0x09`,
  copy+escape `0x00` -> `0x00 0xFF` in one forward pass, terminator `0x00`.
  Use `using System;` and `using System.Buffers;`.
- `DbDate.cs`: `DbDate` struct + `DbDateType` class in one file, per the
  design spec's contracts (interfaces: `IEquatable<DbDate>`,
  `IComparable<DbDate>`, `IEqualityOperators`, `IComparisonOperators`,
  `ISpanFormattable`, `IUtf8SpanFormattable`). Struct with
  `public DbDate(int dayNumber)` and a manual range guard
  (`if (dayNumber is < 0 or > DateOnly.MaxValue.DayNumber) throw new
  ArgumentOutOfRangeException(nameof(dayNumber), dayNumber, "...");` -
  ThrowIfNotBetween does not exist, probed).
  `FromDateOnly(DateOnly)` / `ToDateOnly()` via `DayNumber`/`FromDayNumber`.
  Formatting delegates to `ToDateOnly().TryFormat(..., "O", ...)` (probed:
  writes "2023-01-01"). `DbDateType` encodes tag `0x06` +
  `BinaryPrimitives.TryWriteInt32BigEndian(destination[1..], value.DayNumber ^ int.MinValue)`,
  length 5.
- `DbLocalDateTime.cs`: `DbLocalDateTime` struct + `DbLocalDateTimeType`
  class in one file, per the design spec's contracts (same interfaces as
  `DbDate`). `public DbLocalDateTime(long ticks)` primary-ctor style with
  `public long Ticks { get; }`. Formatting via a `DateTime` with
  `DateTimeKind.Unspecified` (`new DateTime(Ticks, DateTimeKind.Unspecified)`)
  and `"O"`. `DbLocalDateTimeType` encodes tag `0x07` +
  `BinaryPrimitives.TryWriteInt64BigEndian(destination[1..], value.Ticks ^ long.MinValue)`,
  length 9.

**Gate:** `dotnet build src/AsteropeDb.Core/AsteropeDb.Core.csproj` - zero
warnings. `dotnet build AsteropeDb.slnx` - RED, test-side errors only.

**Done when:** Core builds clean with the three new files in the tree.

## Step 6 — Rewrite the six existing Core test files

**Test first:** the rewritten tests ARE the test; the regression pair from
Step 1 must now PASS against the reworked code (they were observed red in
Step 1).

**Then implement:** rewrite these six files under `tests/AsteropeDb.Core.Tests/`
for the new contract. Keep the surviving tests (Instance/TypeName/IsValid/
Compare/value-type behavior) migrated to `TryWriteIndexKey`; DELETE the
`GetHashCode` tests (member gone); ADD per type:

- `TryWriteIndexKey_ShouldReturnTrueAndWriteTaggedKey` - exact expected
  bytes: e.g. boolean false -> `{ 0x02, 0x00 }`, true -> `{ 0x02, 0x01 }`;
  integer `42L` -> `{ 0x03, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2A }`
  (tag + `(42 ^ long.MinValue)` big-endian); nil -> `{ 0x01 }`.
- `TryWriteIndexKey_WithTooSmallDestination_ShouldReturnFalse` - a span one
  byte short; assert `false`, `bytesWritten == 0`, and the buffer contents
  unchanged.
- `GetMaxEncodedLength_ShouldBeAtLeastWrittenBytes` - for a spread of
  values, `max >= written`.
- Migrate the Step 1 regression tests to the new API (rename the methods to
  use `TryWriteIndexKey`; they must now PASS).
- Keep/adjust the ordered-keys tests to the new encodings (they now include
  the tag byte first - order is still byte-lexicographic).

Specifically:

- `DbBooleanTypeTests.cs`: replace the two `GetIndexKey` tests with the
  tagged expectations above; too-small test; delete `GetHashCode` test.
- `DbIntegerTypeTests.cs`: reversible test reads `key[1..]` with
  `BinaryPrimitives.ReadInt64BigEndian` and un-flips; ordering test unchanged
  in spirit; delete `GetHashCode` test.
- `DbFloatTypeTests.cs`: keep the Step 1 regression (migrated); ADD
  `TryWriteIndexKey_WithNegativeZero_ShouldEqualPositiveZero` (the -0.0
  normalization); delete `GetHashCode` test.
- `DbStringTypeTests.cs`: UTF-8 tests now expect tag + escaped bytes +
  terminator; ADD `TryWriteIndexKey_WithEmbeddedNull_ShouldEscape` (exact
  bytes for `"a\0b"`); ADD `Compare_WithSurrogatePairs_ShouldCompareCodePoints`
  (`"😀" < "😁"`); delete `GetHashCode` tests.
- `DbNilTypeTests.cs`: `GetIndexKey` -> tagged `{ 0x01 }`; delete the
  type-level `GetHashCode` test (the struct's own `GetHashCode() == 0` test
  stays).
- `DbTimestampTypeTests.cs`: key tests expect 10 bytes with tag `0x05`;
  reversible test un-flips `key[1..9]`; keep the Step 1 regression (migrated);
  ADD `FromDateTime_WithUnspecifiedKind_ShouldThrowArgumentException`; ADD
  operator/format/parse tests covering Step 4's new members (parsers, span
  formatting); delete the `GetHashCode` type test.

All tests: plain xUnit 2 `[Fact]`/`[Theory]`, no `CancellationToken`
parameters (see verified facts), `Assert` from xunit 2.9.3 only.

**Gate:** `dotnet test tests/AsteropeDb.Core.Tests` - green.
`dotnet build AsteropeDb.slnx` - zero warnings.

**Done when:** Core tests green, build clean, and
`grep -rn "GetIndexKey\|GetHashCode" src/AsteropeDb.Core tests/AsteropeDb.Core.Tests`
returns only the value types' own `override int GetHashCode()` occurrences
(`DbTimestamp.GetHashCode`, `DbNil.GetHashCode`, `DbDate.GetHashCode`,
`DbLocalDateTime.GetHashCode`). No other matches.

## Step 7 — Add tests for the three new types

**Test first:** write the three new test files first; they compile against
the Step 5 production types (tree is green), so the "red" here is: write
each test's assertion, and where an expected byte encoding is asserted, the
tests are green immediately against the PRD transcription. The new types'
red phase was the Step 2 contract break; their encoding correctness is
proven by the exact-bytes assertions.

**Then implement:** three new test files under `tests/AsteropeDb.Core.Tests/`
mirroring the production files:

- `DbBytesTypeTests.cs`: Instance/TypeName/IsValid(null -> false)/Compare
  ordering via `SequenceCompareTo` semantics/TryWriteIndexKey exact bytes
  for `{ 0x01, 0x00, 0x02 }` -> tag `0x09` + `01 00 FF 02` + `00`
  terminator/too-small false path/`GetMaxEncodedLength` >= written.
- `DbDateTests.cs`: ctor range guard throws/`FromDateOnly`-`ToDateOnly`
  round-trip/`CompareTo`/operators (==, <, >, <=, >=, !=)/TryFormat
  round-trip/`DbDateType` key exact bytes for a known dayNumber/ordering/
  too-small path.
- `DbLocalDateTimeTests.cs`: ctor/`CompareTo`/operators/TryFormat/
  `DbLocalDateTimeType` key exact bytes (tag 0x07 + flipped ticks)/ordering/
  too-small path.

**Gate:** `dotnet test tests/AsteropeDb.Core.Tests` - green;
`dotnet build AsteropeDb.slnx` - zero warnings.

**Done when:** all three new suites green; build clean; full solution gate
green.

## Step 8 — Replace IDbSerializer with the buffer-based contract

**Test first:** none (contract-only; no implementation exists to test).

**Then implement:** rewrite `src/AsteropeDb.Serialization/IDbSerializer.cs`
to exactly the design spec's contract (add `using System.Buffers;`; keep
`using System.IO;` only if the docs reference `InvalidDataException` - if
the exception docs move, drop the using). XML-doc all three methods per the
design spec's wording. `CanSerialize<T>` stays.

**Gate:** `dotnet build AsteropeDb.slnx` - zero warnings; full
`dotnet test AsteropeDb.slnx` - green.

**Done when:** build clean and gate green.

## Step 9 — Update the PRD M1 status row

**Test first:** none (documentation; no test can observe it).

**Then implement:** `PRD.md`, the M1 milestone row (currently "Not started.
The existing types need rework.") -> state the delivered subset: new
`IDbType<T>` contract (`GetMaxEncodedLength`/`TryWriteIndexKey`), the nine
type implementations with tag-prefixed self-delimiting encodings, the float
and timestamp ordering fixes, string escaping + code-point comparison,
`DbBytesType`/`DbDate`/`DbLocalDateTime`, the buffer-based `IDbSerializer`,
and the `DbTimestamp` Unspecified-kind rejection. Leave the remaining M1
items (registry rework, bytes/date/localdatetime in every adapter) marked as
still open. Do not touch any other PRD line.

**Gate:** `dotnet build AsteropeDb.slnx && dotnet test AsteropeDb.slnx` -
both green (documentation-only step; run the gate anyway).

**Done when:** gate green and the PRD row reads accurately.

## Final gate (after step 9)

```
dotnet build AsteropeDb.slnx   # zero warnings
dotnet test AsteropeDb.slnx    # green (Core count now > 126: +3 new test files)
```

Report the final test count and the build-warning count.

## Risks

1. **The regression tests are migrated (Step 6) before being observed red
   (Step 1).** Guard: Step 1's gate explicitly requires the red run and its
   verbatim failure output; Step 6 renames the methods but keeps the
   assertions. If the Step 1 red run cannot be produced, stop and report.
2. **The C# 14 compiler crashes (Roslyn NRE) if @coder reaches for an
   unprobed construct** - specifically the `extension` block, which crashes
   this SDK. Guard: the verified-facts list bans it; if a build crashes with
   a NullReferenceException inside `Microsoft.CSharp.Core.targets`, stop and
   report - do not try variations.
3. **The string/bytes escape logic off-by-ones** (`GetMaxEncodedLength`
   undercounts; the escape loop overwrites unescaped bytes). Guard: the
   plan fixes the exact bound (3n+2 for strings, n+zeros+2 for bytes) and
   prescribes the single-pass copy+escape loop; the exact-bytes tests in
   Steps 6/7 assert against hand-computed encodings. If a length mismatch
   shows in the gate, the implementation (not the plan) is wrong.
4. **`TryFormat`/`TryParse` on DbTimestamp produce format mismatches**
   (the `"O"`-format delegation differs subtly from the old `ToString`).
   Guard: the existing ToString tests (Step 6's rewrite keeps them) pin the
   shape; the design explicitly delegates to `DateTime.TryFormat` so the BCL
   is the formatter. If a format test fails, report the actual vs expected
   strings before changing anything.
5. **`GetHashCode` removal leaves a dangling reference** somewhere outside
   Core (a grep the plan runs only over Core paths). Guard: Step 6's grep
   covers `src/AsteropeDb.Core` and `tests/AsteropeDb.Core.Tests`; the final
   gate's full-solution build catches any remaining reference in
   Storage/Network/Serialization (they reference Core). If the build is
   green, no references remain anywhere.
