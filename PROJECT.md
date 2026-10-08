# Continue developing ColonizationNeeds

Purpose: a lightweight always-on-top Windows window showing Elite Dangerous colonization commodity requirements from Raven Colonial.

Current stable version: **1.25**. Features: active/combined Raven projects; grouped commodities and totals; editable carrier stock; Manual/Collect/Colonize; fixed original Required with journal recovery; verified planned construction setup; delivery and completion reporting with saved queues/history; local or Cloudflare shared inventory, ledger and admin replacement; dark amber theme, saved opacity/location and preserved scroll/selection.

Project layout:

- src/ColonizationNeeds.cs: application source and self-tests.
- src/JournalCargoTracker.cs: complete new journal events, commander isolation and tracking tests.
- src/RavenDeliveryReporter.cs: optional confirmed-delivery and depot reporting with persistent state and uncertain-response review.
- src/RavenConstruction.cs: current dock reconstruction, planned-site creation/reuse, identity/type checks and verification tests.
- RAVEN-WORKFLOW.md: start, deliver, complete and recovery steps.
- RELEASE-NOTES.md: stable release contents and upgrading.
- tests/Test-Reporting.ps1: mocked reporting checks run by build.ps1.
- bin/ColonizationNeeds.exe: ready-to-run application.
- build.ps1: rebuild and test with Windows' built-in .NET Framework compiler.
- ColonizationNeeds.csproj: IDE/MSBuild project.
- README.md: usage, API behavior, validation and version history.
- AGENTS.md: instructions for future coding sessions.

Version 1.6 renames RavenNeeds to ColonizationNeeds. On first normal launch it copies legacy settings and inventory from LocalAppData/RavenNeeds to LocalAppData/ColonizationNeeds without overwriting existing destination files. The legacy folder remains intact.

Cross-computer files: source and executable are available in the public GitHub repository. Download or clone the repository on each computer, then open this folder as a local Codex project to continue work. No GitHub account or invitation is needed to download it. Public repository: https://github.com/discordant-T/ColonizationNeeds .

Runtime account settings and inventory remain local. The Windows-encrypted API key must be entered independently on each computer. Local inventory does not automatically synchronize; shared inventory uses the Cloudflare group ledger. Pending queues remain on the original computer.

Validation: build/self-tests and mocked reporting checks pass. The user confirmed construction setup live on Gernhardt Beacon and automatic completion across several constructions. Deployment-specific Cloudflare authorization and IDE/MSBuild builds require their usual separate checks. See RELEASE-NOTES.md.


