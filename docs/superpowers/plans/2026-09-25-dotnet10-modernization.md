# .NET 10 Modernization - Implementation Plan

Design: docs/superpowers/specs/2026-09-25-dotnet10-modernization-design.md
Target framework: net10.0 (was net9.0)
Test project: tests/AsteropeDb.Core.Tests (all four suites gate)

## Preconditions

```
dotnet build AsteropeDb.sln && DOTNET_ROLL_FORWARD=LatestMajor dotnet test AsteropeDb.sln
# 129/129 green, 1 known SA1649 warning (IDbType.cs) — no new warnings
# (current tree is still net9.0/.sln, so the roll-forward applies until step 1 lands)
```

Verified by sandbox probe on 2026-09-25: the full target state (net10.0, CPM,
latest packages, lock files, global.json, slnx) restores, builds and passes
129/129 on this machine with **bare** `dotnet test AsteropeDb.slnx` — no
roll-forward needed. Sandbox: `/var/folders/h_/26tk6wkn1tj6qt557c81dppc0000gn/T/opencode/repo-probe`.

Every package version below was resolved against nuget.org by the probe and is
real as of today. No invented APIs anywhere: the steps below name the exact
files, exact lines, and exact content.

## Step 1 — Retarget all projects to net10.0

**Test first:** none — build/gate is the test (documented exemption).

**Then implement:** in each of these 8 files, replace `net9.0` with `net10.0`:

- `src/AsteropeDb.Core/AsteropeDb.Core.csproj`
- `src/AsteropeDb.Network/AsteropeDb.Network.csproj`
- `src/AsteropeDb.Serialization/AsteropeDb.Serialization.csproj`
- `src/AsteropeDb.Storage/AsteropeDb.Storage.csproj`
- `tests/AsteropeDb.Core.Tests/AsteropeDb.Core.Tests.csproj`
- `tests/AsteropeDb.Network.Tests/AsteropeDb.Network.Tests.csproj`
- `tests/AsteropeDb.Serialization.Tests/AsteropeDb.Serialization.Tests.csproj`
- `tests/AsteropeDb.Storage.Tests/AsteropeDb.Storage.Tests.csproj`

The change is exactly one attribute value per file:
`<TargetFramework>net10.0</TargetFramework>`. Touch nothing else in the files.

**Gate:**
```
dotnet build AsteropeDb.sln && dotnet test AsteropeDb.sln
```
(Bare test is correct after this step: TFM is now net10.0 and only the
.NET 10 runtime is installed.)

**Done when:** build succeeds (1 known SA1649 warning only), 129/129 tests
green, `dotnet list AsteropeDb.sln package` shows `[net10.0]` under every
project.

## Step 2 — Create global.json pinning SDK 10.0.101

**Test first:** none — build/gate is the test.

**Then implement:** create `global.json` (repo root) with exactly:

```json
{
  "sdk": {
    "version": "10.0.101",
    "rollForward": "latestPatch"
  }
}
```

LF line endings. No other content.

**Gate:**
```
dotnet --version && dotnet build AsteropeDb.sln && dotnet test AsteropeDb.sln
```
`dotnet --version` must print `10.0.101`.

**Done when:** `dotnet --version` reports 10.0.101 and gate is green.

## Step 3 — Central Package Management + version bumps + lock files

**Test first:** none — build/gate is the test.

**Then implement:**

a. Create `Directory.Packages.props` (repo root) with exactly:

```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="coverlet.collector" Version="10.0.1" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
    <PackageVersion Include="StyleCop.Analyzers" Version="1.2.0-beta.556" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="4.0.0" />
  </ItemGroup>
</Project>
```

b. In `Directory.Build.props`, add inside the existing `<Project>` (e.g. after
   the CodeAnalysisRuleSet property):

```xml
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <NuGetLockFilePath>packages.lock.json</NuGetLockFilePath>
  </PropertyGroup>
```

c. Remove the ` Version="..."` attribute from the `StyleCop.Analyzers`
   `PackageReference` in `Directory.Build.props`.

d. In all 4 test csprojs, remove the ` Version="..."` attribute from these
   `PackageReference`s (do not touch the `<Using Include="Xunit" />` or
   `<ProjectReference>` elements):
   - `coverlet.collector` (was 6.0.2)
   - `Microsoft.NET.Test.Sdk` (was 17.12.0)
   - `xunit` (was 2.9.2)
   - `xunit.runner.visualstudio` (was 2.8.2)

e. Run `dotnet restore AsteropeDb.sln` (no extra flags needed — the
   `RestorePackagesWithLockFile` property drives it). This produces one
   `packages.lock.json` **per project** (8 files) with the new versions.

**Gate:**
```
dotnet build AsteropeDb.sln && dotnet test AsteropeDb.sln
dotnet list AsteropeDb.sln package
```

**Done when:** gate green (129/129, 1 known warning), 8 `packages.lock.json`
files exist, `dotnet list package` shows requested/resolved:
coverlet.collector 10.0.1, Microsoft.NET.Test.Sdk 18.10.1, xunit 2.9.3,
xunit.runner.visualstudio 4.0.0, StyleCop.Analyzers 1.2.0-beta.556.

## Step 4 — Migrate solution to .slnx

**Test first:** none — build/gate is the test.

**Then implement:**

a. Run `dotnet sln AsteropeDb.sln migrate` (generates `AsteropeDb.slnx` in
   repo root; SDK 10.0.101, verified).
b. Delete `AsteropeDb.sln` (`rm AsteropeDb.sln`).
c. Delete `.idea/runConfigurations/Build_Solution.xml` — it points at the
   removed `.sln`.
d. Inspect `AsteropeDb.slnx`: it must contain 8 `<Project Path=...>` entries
   and 2 `<Folder>` entries (`/src/`, `/tests/`). Fix paths if they differ
   (migrate emits relative paths; sandbox output had 8 projects + 2 folders).

**Gate:**
```
dotnet sln AsteropeDb.slnx list && dotnet build AsteropeDb.slnx && dotnet test AsteropeDb.slnx
```

**Done when:** `dotnet sln AsteropeDb.slnx list` shows all 8 projects, build
succeeds, 129/129 green.

## Step 5 — Consolidate AGENTS.md, delete CLAUDE.md

**Test first:** none — config/documentation change.

**Then implement** (edits to `AGENTS.md`, deletion of `CLAUDE.md`):

a. Rewrite the preamble: remove "First read the instructions in @CLAUDE.md..."
   and the "Where this file and CLAUDE.md disagree, CLAUDE.md wins" paragraph.
   Replace with a sentence that AGENTS.md is now the single authority.
b. In "The quality gate", replace the two gate commands with:

   ```
   dotnet build AsteropeDb.slnx
   dotnet test AsteropeDb.slnx
   ```

   and replace the "Why these exact forms" bullets: remove the
   roll-forward bullets entirely; state that the machine has only the
   .NET 10 SDK/runtime installed and the TFM is now net10.0 so no
   roll-forward is needed; keep the VSTest `--filter` bullet; keep the
   SA1649 baseline bullet; update the baseline to "129 tests ... as of
   2026-09-25".
c. Add a "Code style" section (absorbed verbatim from CLAUDE.md, preserving
   its 4-space / PascalCase / camelCase / braces / file-scoped namespaces /
   no var / target-typed new / XML docs / MIT header / LF rules) and a
   "Project overview" section (absorbed from CLAUDE.md, with the
   ".NET 9" reference corrected to ".NET 10").
d. Update remaining "AsteropeDb.sln" occurrences to "AsteropeDb.slnx".
e. Update "Repo gotchas" if they reference CLAUDE.md or roll-forward.
f. Delete `CLAUDE.md` (`rm CLAUDE.md`).

**Gate:** `grep -r "CLAUDE" . --include="*.md"` returns nothing except
`AGENTS.md` self-references are gone too — the only remaining match must be
zero. Run `dotnet build AsteropeDb.slnx && dotnet test AsteropeDb.slnx` to
confirm nothing broke.

**Done when:** `CLAUDE.md` gone; `grep -rn CLAUDE --include="*.md" .` (repo
root) prints no matches; AGENTS.md contains the absorbed style + overview;
gate green.

## Step 6 — Update opencode.json and .opencode/prompts

**Test first:** none — config change.

**Then implement:**

a. `opencode.json`:
   - Remove `"instructions": ["CLAUDE.md"],` and its preceding comment lines
     (17-19). AGENTS.md is auto-loaded; nothing replaces it.
   - Remove `"CLAUDE.md": "allow",` from the orchestrator `permission.edit`
     block (keep the other five entries).
   - Reviewer bash allowlist: replace
     `"dotnet build AsteropeDb.sln*": "allow"` with
     `"dotnet build AsteropeDb.slnx*": "allow"`, and replace
     `"DOTNET_ROLL_FORWARD=LatestMajor dotnet test*": "allow"` with
     `"dotnet test*": "allow"`.
b. `.opencode/prompts/coder.md`:
   - Line 32: `dotnet build AsteropeDb.sln` -> `dotnet build AsteropeDb.slnx`
   - Line 33: `DOTNET_ROLL_FORWARD=LatestMajor dotnet test AsteropeDb.sln` ->
     `dotnet test AsteropeDb.slnx`
   - Lines 36-40: replace the roll-forward paragraph with: the machine has
     only the .NET 10 SDK/runtime and the TFM is net10.0, so no roll-forward
     is needed — use the bare form.
   - Line 79: `AsteropeDb.sln` -> `AsteropeDb.slnx`
c. `.opencode/prompts/reviewer.md`:
   - Line 20-21: `dotnet build AsteropeDb.sln` -> `...slnx`, and
     `DOTNET_ROLL_FORWARD=LatestMajor dotnet test AsteropeDb.sln` ->
     `dotnet test AsteropeDb.slnx`
   - Line 23-24: drop "The roll-forward prefix is mandatory..." sentence.
d. `.opencode/prompts/orchestrator.md`:
   - Line 9: remove `CLAUDE.md` from the edit-permission list.
   - Line 115: `net9.0` -> `net10.0`.
   - Line 143: "CLAUDE.md" -> "AGENTS.md".
e. `.opencode/prompts/cmd-gate.md`:
   - Line 4: `AsteropeDb.sln` -> `AsteropeDb.slnx`
   - Line 5: `DOTNET_ROLL_FORWARD=LatestMajor dotnet test AsteropeDb.sln` ->
     `dotnet test AsteropeDb.slnx`
f. `.opencode/prompts/cmd-plan.md`:
   - Line 10: roll-forward prefix removed -> `dotnet test AsteropeDb.slnx`
   - Line 14-15: "CLAUDE.md" -> "AGENTS.md"
g. `.opencode/prompts/cmd-fix.md`:
   - Lines 15-16: gate -> `dotnet build AsteropeDb.slnx` then
     `dotnet test AsteropeDb.slnx`; delete the roll-forward sentence.
h. `.opencode/prompts/cmd-small.md`:
   - Lines 15-16: gate -> `dotnet build AsteropeDb.slnx` then
     `dotnet test AsteropeDb.slnx`; delete the "(the prefix is mandatory..."
     parenthetical.
i. `.opencode/prompts/cmd-tdd.md`:
   - Lines 13-15: gate -> `dotnet build AsteropeDb.slnx` then
     `dotnet test AsteropeDb.slnx`; delete "The roll-forward prefix is
     mandatory on this machine.".
j. `.opencode/prompts/cmd-review.md`:
   - Line 15: roll-forward prefix removed -> `dotnet test AsteropeDb.slnx`.
k. `.opencode/prompts/cmd-design.md`:
   - Line 15: `AsteropeDb.sln` -> `AsteropeDb.slnx`.

**Gate:** `grep -rn "ROLL_FORWARD\|CLAUDE\|AsteropeDb\.sln\b" opencode.json .opencode/` prints
no matches. Then `dotnet build AsteropeDb.slnx && dotnet test AsteropeDb.slnx`
green.

**Done when:** the grep above is silent, gate green, and
`dotnet list AsteropeDb.slnx package` still works.

## Step 7 — Update PRD.md

> **SUPERSEDED 2026-09-25**: the project owner rewrote PRD.md as v2.0 while
> step 6 was running. The rewrite already removed every CLAUDE.md and
> ".NET 9" reference and states .NET 10 (LTS), the slnx solution structure
> and central package management. The only session facts the rewrite was
> missing - lock files and the `global.json` SDK pin - were added to the M0
> milestone row (with the owner's authorization). Nothing below this note
> needed executing; it is retained for the record.

**Test first:** none — documentation (design-fact update, done via this spec
as the tripwire requires).

**Then implement** (PRD.md, exact lines):
- Line 244: `CLAUDE.md with development guidelines` ->
  `AGENTS.md with development guidelines`
- Line 101: `.NET 9` -> `.NET 10`
- Line 171: `.NET 9` -> `.NET 10`
- Line 190: `.NET 9` -> `.NET 10`
- Line 218: `.NET 9` -> `.NET 10`
- Line 223: `.NET 9` -> `.NET 10`
- Line 234: `.NET 9` -> `.NET 10`
- Line 371: `.NET 9` -> `.NET 10`

**Gate:** `grep -n "CLAUDE\|\.NET 9\|net9" PRD.md` prints nothing; build/test
still green (untouched by this step, run them anyway).

**Done when:** grep silent and gate green.

## Risks

1. **@coder edits the wrong `.csproj` elements** (e.g. touches
   `<Using Include="Xunit" />` or project refs while stripping versions).
   Guard: every step lists exactly which attributes change; @coder must
   `git diff` after each step and report a one-line summary of changed files.
2. **The version-stripping in step 3 leaves a malformed csproj** (empty
   attribute, broken self-closing tag). Guard: gate runs immediately after;
   restore errors surface the exact file; the plan pre-warns that a
   restore/build error here is a syntax problem, not a design problem.
3. **Roll-forward text removal is incomplete** (some prompt keeps the old
   command and future coder runs fail confusingly on a bare `dotnet test`).
   Guard: the final grep in step 6 (ROLL_FORWARD/CLAUDE/`AsteropeDb.sln` word
   boundary) must be silent before the step is done — and @coder reports the
   grep output verbatim.
