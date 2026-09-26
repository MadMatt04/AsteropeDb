Run the full quality gate and report only what fails, with the exact error
text. No commentary, no fixes unless I ask.

!`dotnet build AsteropeDb.slnx 2>&1 | grep -E "error|warning|Build succeeded" | head -30`
!`dotnet test AsteropeDb.slnx 2>&1 | tail -15`

If everything passed, reply with one line: the test count and "gate green".
