# ColonizationNeeds project

This is a standalone native Windows Forms application for Elite Dangerous colonization supplies. The canonical source is src/ColonizationNeeds.cs. Build with powershell -NoProfile -ExecutionPolicy Bypass -File ./build.ps1. The script builds bin/ColonizationNeeds.exe and runs the self-tests. No external packages or SDK installation are needed for the script. The csproj supports IDE editing with .NET Framework 4.8 development tools installed.

Preserve read-only Raven Colonial API behavior, saved commander-specific inventory, Windows DPAPI key encryption, inventory Add/Remove/Set total operations, grouped commodity categories, and the slate/sage palette. Never put credentials or runtime inventory into this repository. The executable is deliberately included because the user wants source and a ready-to-run app available across computers.

Check the README for behavior, API references, and multi-computer limitations. For API changes, verify the current RavenColonialWeb API host and headers instead of assuming the public website hosts its API. Keep changes in this project folder; older RavenNeeds files outside it are legacy snapshots.
