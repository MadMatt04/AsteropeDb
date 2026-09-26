Phase 4 (Track L) - implement the next unchecked step of:
@docs/superpowers/plans/$1.md
(if that path does not exist, list `docs/superpowers/plans/` and use the newest
file matching `$1`)

Working tree state:
!`git status --short`

Execute exactly ONE step - the first one not marked done. Red, green, gate,
refactor, report, stop. If the step's signatures do not compile as written,
report it as a plan defect instead of substituting your own API.

Gate is `dotnet build AsteropeDb.slnx` then
`dotnet test AsteropeDb.slnx`. While iterating, filter:
`dotnet test tests/<module>.Tests --filter "FullyQualifiedName~<ClassName>"`.

When the step passes, mark it done in the plan file and stop for review.
