Track F fix: $ARGUMENTS

Working tree:
!`git status --short`

Confirm this really is Track F against AGENTS.md before you touch anything: one
production file, under ~20 lines, no signature change, no tripwire, and you can
state the root cause in one sentence. Say the root cause first. If any of that
does not hold, or it touches anything in Tripwires, stop and tell me which track
it actually is.

Then: failing regression test that reproduces the defect, minimum fix, gate,
one-line report. No design spec, no plan file.

Gate: `dotnet build AsteropeDb.slnx` then
`dotnet test AsteropeDb.slnx`.
