# .NET 10 Modernization - Review

Diff under review: working tree vs `HEAD` (786d2b8) on `net10-modernization`.
Intent: retarget to net10.0, pin dependencies (CPM + lock files + global.json),
migrate `.sln` -> `.slnx`, delete `CLAUDE.md` into `AGENTS.md`.

## Verdict

APPROVE WITH FINDINGS - the migration itself is complete and the gate is green,
but `AGENTS.md` ships two false repo facts and the orchestrator prompt names an
xunit v3 API the pinned xunit 2.9.3 does not have.

## Gate (run by reviewer)

- `dotnet build AsteropeDb.slnx` - Build succeeded, 0 errors, 1 warning
  (the known pre-existing SA1649 in `src/AsteropeDb.Core/IDbType.cs`).
- `DOTNET_ROLL_FORWARD=LatestMajor dotnet test AsteropeDb.slnx` - 129/129 pass
  (126 Core + 1 each in Storage, Network, Serialization).

Verified against review target list: all 8 projects list `[net10.0]`; the 5
central versions resolve to 10.0.1 / 18.10.1 / 1.2.0-beta.556 / 2.9.3 / 4.0.0
in every `packages.lock.json`; no csproj or `Directory.Build.props` carries a
hard-coded `Version=`; `AsteropeDb.slnx` lists 8 projects under 2 folders with
correct relative paths; `AsteropeDb.sln`, `CLAUDE.md` and
`.idea/runConfigurations/Build_Solution.xml` are the only deletions and all are
intentional. `CLAUDE.md`, `DOTNET_ROLL_FORWARD`, `AsteropeDb.sln` (word
boundary) and `net9` appear only in the specs/plans narration and in the
historical "why" bullet in `AGENTS.md` - no active instruction follows a
deleted file or a removed command.

## Findings

1. [major] AGENTS.md:317 - states `PRD.md.bak` is an untracked local copy; it is
   tracked in `HEAD` (`git show HEAD:PRD.md.bak` succeeds; `git log -- PRD.md.bak`
   shows it from the initial commit).
   Failure: an agent reads "untracked local copy", treats the file as outside
   version control, and either re-stages/ships the stale pre-2.0 PRD in the
   final commit or assumes it is safe to edit - the opposite of the protection
   the bullet is trying to provide.
   Fix direction: correct the bullet to say it is a tracked stale copy pending
   removal in its own cycle, then state the real constraint (never reference or
   edit it).

2. [major] .opencode/prompts/orchestrator.md:145 - tells the planner that new
   tests take `TestContext.Current.CancellationToken`; `TestContext` is an xunit
   v3 API and the repo pins xunit 2.9.3 (v3 was explicitly deferred by the spec).
   Failure: any future Track L plan with an async test adopts this line; the
   coder writes the test, it fails to compile - which AGENTS.md says is not a
   valid red - and the step escalates as a plan defect instead of progressing.
   Fix direction: drop the line or replace it with a v2-compatible pattern
   (thread an explicit `CancellationToken` parameter into the test) and confirm
   the API exists in xunit 2.9.3 before naming it.

3. [minor] AGENTS.md:319-321 - lists `src/AsteropeDb.Serialization` among the
   packages that contain a `Class1.cs` placeholder; that project has no
   `Class1.cs`, only `IDbSerializer.cs`.
   Failure: an agent following the gotcha models a new file on, or greps for, a
   Serialization `Class1.cs` that does not exist, and mis-reports the package
   state; the "additionally has the real `IDbSerializer` seam" half is correct
   but the placeholder half is not.
   Fix direction: remove `.Serialization` from the `Class1.cs` sentence.

4. [minor] AGENTS.md:43-47 - the Project Overview module list omits
   `AsteropeDb.Tools` and the `AsteropeDb` metapackage that PRD v2.0's Package
   Layout defines, so it is an incomplete sync with the design authority the
   same file names as authoritative.
   Failure: a Tools/CLI-scoped request is planned against a module list that
   does not mention the CLI package; the planner has to discover it from the
   PRD, and the overview silently understates the shipping surface.
   Fix direction: add `AsteropeDb.Tools` (and the `AsteropeDb` metapackage) to
   the modular-design bullet, matching PRD v2.0.

## Weak tests

N/A - this is a build/config/docs-only diff. No test can observe the difference
between `.sln` and `.slnx`, central package versions, a deleted markdown file or
`global.json`; the AGENTS.md "no test could observe the difference" exemption
applies, and the gate (build + 129 green) is the verification. No placeholder
test was weakened or deleted.

## Not reviewed

- `.github/workflows/claude.yml` and `claude-code-review.yml`: untouched by the
  diff and outside the stated intent. They still configure the
  `anthropics/claude-code-action`; whether those should now point at a
  different instruction file is a separate decision, not a dangling `CLAUDE.md`
  reference.
- `dotnet --version` / the `global.json` SDK pin could not be confirmed: the
  reviewer bash allowlist has no `dotnet --version`, so the 10.0.101 pin is
  taken from the file content and the green build, not from the resolver.
- `PRD.md` v2.0 prose: reviewed only for consistency with `AGENTS.md`, not for
  design merit or the correctness of the new design decisions.
- `AsteropeDb.sln.DotSettings` (tracked) still carries the deleted solution's
  name. Its content holds no path reference, so nothing a reader follows breaks;
  noted as leftover hygiene, not a finding.
- README.md still describes memory-mapped files and custom HTTP/2, which PRD
  v2.0 rejects; `AGENTS.md` explicitly declares README snippets aspirational
  and out of scope, so this is not counted against the diff.

## Untracked files that must be staged in the final commit

From `git status --short`:

- `AGENTS.md`, `opencode.json`, `global.json`, `Directory.Packages.props`,
  `AsteropeDb.slnx`
- `.opencode/` (its own `.gitignore` keeps `package.json`,
  `package-lock.json`, `node_modules` and `.gitignore` out, so only
  `prompts/**` lands - intended)
- `docs/` (specs, plans, this review)
- 8 `packages.lock.json` (one per project, all 8 present)

The deletions (`AsteropeDb.sln`, `CLAUDE.md`,
`.idea/runConfigurations/Build_Solution.xml`) must be staged as deletions. No
deleted file outside that set was found.
