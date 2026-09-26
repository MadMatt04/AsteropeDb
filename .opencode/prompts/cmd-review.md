Phase 5 - review the current diff.

Plan under review: @docs/superpowers/plans/$1.md
(if that path does not exist, list `docs/superpowers/plans/` and use the newest
file matching `$1`; on Track F or S there may be no plan at all, which is fine -
review against the stated intent instead)

Diff stat:
!`git diff --stat HEAD`

Full diff:
!`git diff HEAD`

Gate status (expected to pass already - confirm, do not re-litigate):
!`dotnet test AsteropeDb.slnx 2>&1 | tail -6`

Review against the plan step that was just implemented. Findings need
path:line and a concrete failure scenario. Weak tests get their own section.
Check AGENTS.md's Tripwires: a change that trips one but ran on a light track is
your first finding and it is a blocker.
