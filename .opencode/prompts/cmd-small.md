Track S change: $ARGUMENTS

Working tree:
!`git status --short`

Confirm Track S against AGENTS.md: ~3 files, ~80 net lines, mirrors a pattern
already in this repo (name the file), no new cross-project contract, no
tripwire.

Write the plan inline - 3-6 numbered steps, real signatures, a gate command per
step - and stop for a nod. Do not write the first test until I answer.

Step gates are filtered test runs:
`dotnet test tests/<module>.Tests --filter "FullyQualifiedName~<ClassName>"`.
Finish each step on the unfiltered `dotnet test AsteropeDb.slnx`.
