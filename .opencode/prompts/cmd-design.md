Phase 2 - design: $ARGUMENTS

Today is !`date +%F` - use it as the date prefix for the spec filename.

Write `docs/superpowers/specs/<today>-$1-design.md`, following your document
structure. This is the first spec in the directory, so you are establishing
the shape future specs will copy. If a brainstorm transcript or notes exist
for this feature, reconcile with them; where you depart from the brainstorm's
chosen direction, say so explicitly and why.

Existing specs, for naming and house style:
!`ls docs/superpowers/specs/ 2>/dev/null`

Package versions - verify, do not assume:
!`dotnet list AsteropeDb.slnx package 2>/dev/null | head -40`

Stop and ask before writing if any `OPEN:` question would change the contracts
section.
