# Continue developing ColonizationNeeds

Purpose: a lightweight always-on-top Windows window showing Elite Dangerous colonization commodity requirements from Raven Colonial.

Current features: saved commander name and optional API key; active-project selection and combined view; per-commodity Required, Carrier stock, Still needed columns; fixed column totals; local inventory Add, Remove and Set total with resulting-count preview; commodity type groups; slate/sage theme; minute refresh; stale-data errors; saved Manual, Collect and Colonize modes for newly loaded ship cargo.

Project layout:

- src/ColonizationNeeds.cs: application source and self-tests.
- src/JournalCargoTracker.cs: complete new journal events, commander isolation and tracking tests.
- bin/ColonizationNeeds.exe: ready-to-run application.
- build.ps1: rebuild and test with Windows' built-in .NET Framework compiler.
- ColonizationNeeds.csproj: IDE/MSBuild project.
- README.md: usage, API behavior, validation and version history.
- AGENTS.md: instructions for future coding sessions.

Version 1.6 renames RavenNeeds to ColonizationNeeds. On first normal launch it copies legacy settings and inventory from LocalAppData/RavenNeeds to LocalAppData/ColonizationNeeds without overwriting existing destination files. The legacy folder remains intact.

Cross-computer files: source and executable can be stored in a private GitHub repository. Download or clone the repository on each computer, then open this folder as a local Codex project to continue work. Private repository: https://github.com/discordant-T/ColonizationNeeds .

Runtime account settings and inventory remain local. The Windows-encrypted API key must be entered independently on each computer. Inventory does not automatically synchronize between computers.

Validation: compiled application and embedded self-tests pass. Live account access and DPAPI encryption require testing in a normal signed-in desktop session. IDE/MSBuild project configuration has not been built with a Framework 4.8 developer pack in this environment.


