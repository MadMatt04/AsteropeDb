Phase 3 - plan: $ARGUMENTS

Today is !`date +%F` - use it as the date prefix for the plan filename.

Design spec: @docs/superpowers/specs/$1-design.md
(if that path does not exist, list `docs/superpowers/specs/` and use the newest
file matching `$1`, or stop and ask which spec this plans)

Current baseline - the plan's preconditions must match this:
!`dotnet test AsteropeDb.slnx 2>&1 | tail -6`

Write `docs/superpowers/plans/<today>-$1.md`. Every signature real or marked
`VERIFY:`. Every step gated by an exact command. No step may leave the tree
red. New files must carry the MIT license header and satisfy AGENTS.md's style
rules - say so in the step that creates them.
