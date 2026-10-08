# Stable release 1.25

Released October 7, 2026. Source and `bin/ColonizationNeeds.exe` are available on the repository's main branch. Executable file version: **1.25.0.0**. The filename stays **ColonizationNeeds.exe**.

## New since the previous GitHub stable version 1.21

- **Settings > Start planned construction** in any cargo mode. Select the planned Raven site while docked with Construction Services open. Setup creates or reuses the project, preserves its exact type, links your commander, updates the plan to Building and verifies the result.
- **Fixed Colonize Required totals.** Original RequiredAmount is separate from outstanding delivery quantities, persists across refresh/restart, and can be recovered read-only from existing journals. Still needed alone reduces as deliveries arrive.
- **Automatic Raven completion.** New game-confirmed completion events call and verify the explicit completion endpoint. Zero cargo remaining is not assumed complete.
- **Completion history and durable recovery.** Completion records appear alongside deliveries; saved requests resume after interruption with a server check. Final delivery credits are processed first.

The earlier stable inventory, manual adjustments, commodity groups, dark amber theme, opacity, saved window position, scroll/selection preservation, local/shared modes, ledger and admin local-to-shared replacement remain available.

## Upgrading

1. Close ColonizationNeeds.
2. Download the current executable from `bin/ColonizationNeeds.exe` in the public repository.
3. Replace your existing executable, wherever you keep it, then restart. Source/backend files are not required beside it.
4. Confirm Settings and the correct journal folder. Enable reporting with your own Raven key if you want deliveries and completion uploaded.
5. Follow [the Raven workflow](RAVEN-WORKFLOW.md) to start a new planned site.

Saved settings, stock, queues, window state and original totals remain under `%LOCALAPPDATA%\ColonizationNeeds`. No backend/schema migration or Cloudflare redeployment is required for this release. Enter encrypted credentials separately on another Windows account/computer. Do not run the old and new executables together for one game session.

## Verification

The project builds with the Windows .NET Framework compiler and runs self-tests and mocked reporting tests. Construction tests cover uncertain variants, body conflicts, existing-project reuse, lost creation acknowledgments and commander verification. Completion tests cover final credit ordering, game completion with an empty resource list, zero requirements without completion, saved history and restart recovery without duplicate completion calls.

Live construction start was verified on Gernhardt Beacon in Synuefe HF-P c22-15, with the correct Military Outpost (nemesis) type and linked plan/commander. The user confirmed automatic completion across several constructions. Deployment-specific Cloudflare access still needs the leader's usual two-player connection check.

## Notes

Reporting is opt-in and requires the app running for new game events. It does not backfill deliveries or completion events. Historical journal lookup recovers original requirements only. Disable overlapping delivery/colonization reporting in other tools to avoid duplicate credit. Inventory remains an estimate; no authoritative squadron carrier storage reconciliation is added.

The prior v1.21-only local stable executable and v1.24 experimental builds are superseded by this combined stable release.
