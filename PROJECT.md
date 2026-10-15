# Project map — 1.28.1

Standalone .NET Framework Windows Forms app. build.ps1 compiles bin/ColonizationNeeds.exe and runs regression checks. Canonical current behavior is documented in README.md and CARRIER-TRACKING.md; RELEASE-NOTES.md accounts for all changes from the earlier acquisition tracker through Inbound/Outbound.

| Component | Responsibility |
|---|---|
| src/ColonizationNeeds.cs | UI, project requirements, original-total recovery, profile integration, column arithmetic and settings. |
| src/CompactUI.cs | Saved Compact/Full layout, optional columns, collapsible controls and always-visible status alerts. |
| src/CarrierUI.cs | Carrier configuration/review, stock/cargo corrections, collection intent/destination and shared/local views. |
| src/CarrierInventory.cs | Durable local stock ledger, legacy migration, signed discrepancies and cutoffs. |
| src/TransitLedger.cs | Opted-in station purchases, Outbound withdrawals, mixed-lot consumption, loss/snapshot caps. |
| src/CarrierDiscovery.cs | Read-only commander-specific carrier identification. |
| src/JournalCargoTracker.cs | Dock/commander context, complete journal lines, durable positions and callback ordering. |
| src/SharedInventoryClient.cs | DPAPI credentials, durable shared outbox, stable receipts, stock/cargo snapshots and registry CAS. |
| src/PlayerProfiles.cs | Independent commander folders and migration. |
| src/RavenConstruction.cs | Validated planned-site setup, reuse/link/verify. |
| src/RavenDeliveryReporter.cs | Durable optional contributions/depot/completion reporting and uncertain review. |
| backend/worker.js | Authenticated player/group APIs and per-player supply fold. |
| backend/schema*.sql | Original stock, additive carrier registry/stock ledger, additive ship-cargo events/trigger. |
| tests/ | Real SQLite Worker routes, local/shared replay/lost acknowledgment, UI arithmetic/configuration and Raven checks. |

Keep source and runnable executable together, but never package runtime inventory, player tokens or API keys. Production deployments must preserve the existing database/registry/secrets and record validation/recovery information in DEPLOYMENT.md. Do not deploy merely to run tests.
