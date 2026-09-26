# AGENTS.md

This is the single authority for how agents work in this repo and how code in
it is styled. `opencode` auto-loads it and Claude Code reads it natively, so
there is deliberately no second instruction file - one file, no drift.
`PRD.md` is the design authority (see Tripwires); this file governs process,
style, and repo facts. If this file and `PRD.md` disagree, `PRD.md` wins on
design questions and this file wins on process and style - and tell me about
the disagreement so one of them gets fixed.

## Code style

StyleCop runs in the build and violations surface as warnings; the tree is
kept near warning-free. StyleCop is the enforcement - this section is the
authority on intent.

- Use 4 spaces for indentation.
- PascalCase for classes/methods/constants/properties, camelCase for variables
  and fields.
- Do not prefix private fields with an underscore.
- Braces on new lines for classes/methods (Allman).
- File-scoped namespaces preferred.
- Implicit usings are disabled - explicit `using` in every file.
- Avoid `var`; specify the type explicitly where possible. For `new`, prefer
  target-typed: `MyClass c = new();`.
- Extensively comment all public, internal and protected classes and members.
  Use `<see cref>`, `<see langword>` and `<paramref>` liberally. Private
  members do not need comments unless the code is unclear or complicated.
  End all comment sentences with a period.
- Put the MIT license header at the top of every file.
- Import any type referenced in a comment (e.g. `using System;` for
  `DateTime`).
- Remove unnecessary using directives.
- Keep files LF even though `.editorconfig` says CRLF (see Repo gotchas).
- Plain xUnit only in tests - no Moq, no FluentAssertions.

## Project overview

AsteropeDb is a .NET-native database optimized for storing large binary files
(up to 2^63 bytes) with queryable metadata. Target domains: healthcare,
scientific research, media management, enterprise data storage.

- **Modular design**: `AsteropeDb.Core` (types, interfaces, abstractions),
  `AsteropeDb.Storage` (storage engine), `AsteropeDb.Serialization`
  (MessagePack integration), `AsteropeDb.Network` (placeholder; post-MVP it
  becomes `AsteropeDb.Network.Server`/`.Client`), plus the `AsteropeDb`
  metapackage and `AsteropeDb.Tools` (the `asterope` CLI) - distributed as
  separate NuGet packages.
- **Storage engine**: positional file I/O (`RandomAccess`) for binary data -
  memory-mapped files are explicitly not used for blobs; B+ tree indexing for
  metadata in a page store with a WAL; streaming I/O; a bounded,
  scan-resistant page cache; optional per-collection compression. The blob
  store layout is an open decision, settled at milestone M2.
- **Operation modes**: embedded (query AST with a LINQ front-end subset);
  networked post-MVP (plain HTTP semantics for blobs, gRPC for control - no
  custom HTTP/2 framing).
- **Transactions**: ACID with WAL; MVCC with a single writer - read
  transactions see a snapshot, write transactions are serializable; no
  cross-key joins.
- **Clustering**: a non-goal.
- **Stack**: .NET 10 (LTS), MessagePack for canonical stored metadata (other
  serializers are adapters at the API boundary), ASP.NET Core and gRPC for
  network services.
- **Design principles**: performance over features for large binary data;
  streaming to avoid memory pressure; simple APIs that scale from embedded to
  networked; minimal external dependencies.
- Design decisions and milestones live in `PRD.md`, not here - one source for
  design facts.

## Agents

Three, one per role - because the roles are expensive to merge and the phases
aren't (`opencode.json`):

- **`orchestrator`** (default agent, primary) - understands the request, writes
  the design spec and the plan, and delegates. Cannot edit production code -
  only `docs/superpowers/specs/**` and `docs/superpowers/plans/**` (plus
  `AGENTS.md`, `README.md`).
- **`coder`** (subagent) - implements one step at a time, strictly TDD, on
  instructions from Orchestrator or a direct `/tdd` / `/fix` / `/small` /
  `/gate`.
- **`reviewer`** (subagent) - adversarial review of a diff. Cannot edit code -
  only `docs/reviews/**`.

Talk to `orchestrator` for anything that isn't a one-liner you could type
yourself; it decides how much process the request needs (see Right-sizing
below) and hands the actual typing to `@coder`. The slash commands are the
fast path when you already know what you want: `/fix`, `/small`, `/tdd`,
`/gate` go straight to `coder`; `/review` straight to `reviewer`;
`/brainstorm`, `/design`, `/plan` to `orchestrator`.

**Bash is open to all three**, with two exceptions the tool itself enforces:
`git commit` and `git push` always ask. Nothing else below is tool-enforced -
it is what each agent's prompt tells it to do, and what you're expected to
notice and stop if it doesn't. The Reviewer's own bash is the one place a
narrower allowlist remained, because "cannot edit code" needs to mean it even
when the tool would let it shell around that.

## The quality gate

Two commands. They are the definition of "done" for any code change:

```sh
dotnet build AsteropeDb.slnx
dotnet test AsteropeDb.slnx
```

`/gate` runs both and reports only failures.

Why these exact forms - do not "simplify" them back:

- **The machine has only the .NET 10 SDK/runtime installed** and all projects
  target `net10.0` (pinned by `global.json`), so a bare `dotnet test` is the
  correct form - no `DOTNET_ROLL_FORWARD` prefix. That prefix existed only
  while the projects targeted `net9.0` with no .NET 9 runtime present; it is
  obsolete and must not be reintroduced.
- **Test filters are VSTest `--filter`, not MTP single-dash options** (unlike
  EnduranceCoach):

  ```sh
  dotnet test tests/AsteropeDb.Core.Tests --filter "FullyQualifiedName~DbStringTypeTests"
  ```
- **The build is the warning check.** There is no CI build/test workflow and
  `TreatWarningsAsErrors` is off, so the build output is the only thing that
  sees StyleCop violations. Keep it near warning-free; a step that introduces
  a new warning is a red step. One pre-existing warning is known and not yours
  to fix casually: SA1649 in `src/AsteropeDb.Core/IDbType.cs` (file name does
  not match first type name).
- **Baseline:** 129 tests across four test projects (126 Core, 1 each in the
  three placeholder suites) as of 2026-09-25. A plan's preconditions must show
  the current count so drift is visible.

## Right-sizing the cycle

The full cycle exists for changes where a bad design is expensive. Most
changes are not that. Writing a design spec and a plan file for a two-line fix
wastes money and, worse, trains you to skim the documents that matter.

There are three tracks. Orchestrator **states which track a request is on in
its first line of response, and why.** If it can't justify a track in one
sentence, it's on the track above.

### Track F - Fix

For: a defect with a known cause, a typo, a wrong constant, a missing null
check, a doc-comment fix, a test that was wrong.

Conditions - all must hold:

- one file of production code, and the change is under ~20 lines
- no new public member, no signature change
- no new dependency
- the root cause is statable in one sentence

Procedure: Orchestrator skips design and planning entirely and hands `@coder`
the root cause and the fix directly. Failing regression test that reproduces
the defect, fix it, gate, report. No design doc, no plan file, no `@reviewer`
unless the fix touches something in Tripwires.

The regression test is not optional on Track F. A bug that was worth fixing was
worth a test, and a fix with no test is how the same bug comes back in six
weeks.

### Track S - Small feature

For: a new `IDbType<T>` implementation that mirrors an existing one, a small
registry addition, a serializer implementation on the existing seam.

Conditions - all must hold:

- at most ~3 production files and ~80 net lines
- follows a pattern already present in this repo - Orchestrator can point at
  the file it's modelled on
- no new public contract consumed by another project
- no tripwire from the list below

Procedure: no design spec. Orchestrator writes a short plan - 3-6 numbered
steps with real signatures and a gate command each - inline in the
conversation rather than in `docs/superpowers/plans/`, gets a nod from you,
then delegates it to `@coder` step by step. `@reviewer` is a judgement call:
run it if the change touches the public API surface of a package, skip it for
a mechanical refactor with green tests.

### Track L - Full cycle

Everything else, and anything with a tripwire. Orchestrator understands the
request, writes the design spec, writes the plan file, then delegates it to
`@coder` one step at a time - waiting for each step's report before sending
the next - and brings in `@reviewer` at the end (or sooner, for anything that
touches the public API surface or a tripwire).

## Tripwires - force Track L regardless of size

A one-line change to any of these is a Track L change. Size is not the
criterion here; blast radius is.

- **`IDbType<T>` or any `Db*Type` comparison contract.** Equal values must
  produce equal `GetIndexKey` bytes and equal hash codes, and `Compare` must
  be a total order - a violation corrupts B+ tree lookups in ways no test in
  the current suite would catch.
- **`src/AsteropeDb.Core` public surface.** It is the base package everything
  else references; a change here ripples into all three dependents.
- **`PRD.md`.** It is the design source of truth. Changing the design goes
  through a spec in `docs/superpowers/specs/`, not a direct edit of the PRD
  mid-task.
- **Solution/project/build shape**: `AsteropeDb.slnx`, any `.csproj`,
  `Directory.Build.props`, `Directory.Packages.props`, `global.json`,
  `packages.lock.json`, `code-analysis.ruleset`, `stylecop.json`, or a
  NuGet dependency. Adding a package needs approval.
- **Anything that reads or writes whole payloads into memory** where the
  design calls for streaming - the entire point of this database is 2^63-byte
  files, and an accidental `ToArray()` on a stream is invisible in small-file
  tests.
- **WAL or storage format, once it exists** - the on-disk format is append-
  only and backwards-compatible; anything touching it is Track L with a spec.
- **Deleting or rewriting an existing test.**
- **You do not understand why the current code does what it does.**

## Escalation is one-way

Tracks move up, never down. If a Track F fix turns out to need a second file,
it is Track S; if a Track S feature reveals a tripwire, Orchestrator stops and
says so before delegating anything else. Do not finish the change on the
original track and mention it afterwards.

**Two failed gates on the same step is an escalation, not a third attempt.** If
`@coder` reports the gate failing twice on the same step, Orchestrator stops
and reports what was learned rather than sending a third variation. Repeated
failure is evidence the plan or the design is wrong, and Coder is the
worst-placed agent to notice that.

## What no track ever skips

- the failing test first, and it must fail on its assertion
- both gate commands, green, before reporting done
- the off-limits list below
- the API-exactness rule below
- no weakening or deleting an existing test to get to green

The only changes that may ship without a test are ones where no test could
observe the difference: comments, `.md` files, doc-comment wording. Say which
applies; "hard to test" is not on the list.

## TDD is not optional

Failing test first, minimum code to green, then refactor. A test that fails to
compile does not count as red - it must fail on its assertion. The tree is
green at the end of every step.

This survives every track. What the tracks change is how much planning happens
before the first test gets written, never whether it gets written. On Track L
that means one plan step at a time; on Track F it means one regression test and
one fix.

Testing rules that are easy to get wrong here:

- Plain xUnit only - no Moq, no FluentAssertions, no frameworks beyond what the
  test csproj already references. The placeholder suites in Storage, Network
  and Serialization each have exactly one `Assert.True(true)` test.
- `AsteropeDb.Core.Tests` mirrors `src/AsteropeDb.Core` file-for-file - one
  test class per production type (`DbStringType` -> `DbStringTypeTests`).
- Tests live in `tests/AsteropeDb.<Module>.Tests`, never under `src/`.

## API-exactness rule

`coder` is fast and cheap and *will* invent plausible .NET API surface if
allowed to. Therefore:

- Any framework member not already used somewhere in this repo counts as
  unverified. Confirm it before calling it: grep for prior usage, check the
  package version with `dotnet list package`, or read the type.
- Build after writing a file, not after writing five.
- `VERIFY:` markers in a plan must be resolved before the code they guard is
  written, and how they were resolved must be stated.
- If a signature from the plan does not compile, that is a plan defect. Report
  it to `@orchestrator`. Do not silently substitute a different API.

## Off-limits - by convention, not by the tool

`opencode.json`'s bash permission is `"*": "allow"` plus `ask` on
`git commit`/`git push` - simple on purpose. Everything below is something
every agent is instructed not to do without checking with you in chat first,
and something worth actually noticing if an agent does it anyway, since the
tool will no longer stop it:

- Editing `AsteropeDb.slnx`, any `.csproj`, `Directory.Build.props`,
  `Directory.Packages.props`, `global.json`, `packages.lock.json`,
  `code-analysis.ruleset`, or `stylecop.json`
- `dotnet add package` / any NuGet change
- Editing `PRD.md` directly (specs are the path for design changes)
- Anything in Tripwires, beyond reading it
- Touching files outside the current step
- Adding `// TODO`, empty catch blocks, or `#pragma warning disable`

## Docs layout

Date-prefixed names, matching the EnduranceCoach convention:

```
docs/superpowers/specs/<YYYY-MM-DD>-<slug>-design.md    written by `orchestrator`
docs/superpowers/plans/<YYYY-MM-DD>-<slug>.md           written by `orchestrator`,
                                                        ticked off by `coder`
docs/reviews/<YYYY-MM-DD>-<slug>.md                     written by `reviewer`
```

`reviewer` can write `docs/reviews/**` and nothing else. `orchestrator` can
write the two paths above plus `AGENTS.md` and `README.md`.
Neither can write production code: that boundary is tool-enforced (the `edit`
permission, unlike `bash`, stayed narrow), and it is what stops Orchestrator
from "just fixing" the thing it was supposed to describe, and Reviewer from
fixing what it finds.

## Repo gotchas

- README quick-start and API snippets are aspirational - `AsteropeDatabase`,
  `AsteropeClient`, `StoreAsync`, etc. do not exist yet. `PRD.md` is the
  authority on intended design; the real code is the authority on what exists.
  `PRD.md.bak` is a stale tracked copy of the pre-2.0 PRD - never edit or
  reference it.
- `src/AsteropeDb.Storage` and `.Network` contain `Class1.cs` placeholders;
  `.Serialization` holds the real `IDbSerializer` seam.
- Dependencies are pinned three ways: `global.json` (SDK 10.0.101),
  `Directory.Packages.props` (central package versions), and one
  `packages.lock.json` per project (generated by `dotnet restore`). Version
  bumps therefore go through a plan, not a casual `dotnet add package`.
- `nuget.config` sets `globalPackagesFolder` to `~/.nuget/packages`, but NuGet
  does not expand `~`, so a stray `~/` folder appears in the repo root on
  restore. It is gitignored - ignore it; do not add files there.
- Working tree is LF despite `.editorconfig` specifying CRLF (git
  `core.autocrlf=input`) - keep new files LF.
- `dotnet format` exists but is not the gate; the `.editorconfig` CRLF setting
  makes `--verify-no-changes` noisy. StyleCop via the build is the style
  check.
- Style rules agents miss most: `ImplicitUsings` is off (explicit `using` in
  every file), MIT license header at the top of every new `.cs` file, XML docs
  on all public/internal/protected members, no `var`, camelCase private fields
  without underscore.

## Git workflow

Feature branches for anything beyond a one-line fix or a docs touch; `master`
is the main branch. `git commit` and `git push` are the two commands
`opencode.json` gates with `ask`, regardless of track or agent.

Commit messages carry no co-author trailers - write plain commit messages only.
