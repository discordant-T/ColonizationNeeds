# 1.28.6: shared inventory read optimization

Released as v1.28.6. The owner confirmed the backend changes were completed on October 23, 2026. This publication uses local regression tests and does not redeploy the Worker or modify production data. Production read savings have not been independently measured.

The five-second sync cadence is retained. The Worker checks indexed high-water marks for stock, legacy stock, transit events and carrier configuration. A matching per-player revision returns a small unchanged response without loading inventory or replaying transit history. Full snapshots now include carrier configuration, removing the separate registry request in updated clients. Authentication still occurs on every request.

Unchanged polling cost no longer grows with transit history. Changed snapshots still replay the complete group history to preserve chronological, per-player lot accounting, including backdated events. Sustained activity may therefore require a later materialized-balance optimization. This release does not claim a measured production read reduction.

## Upgrade instructions for other installations

1. Confirm the actual deployed Worker and schema against this package before making changes. Preserve all existing routes, secrets and DB bindings.
2. Apply `backend/schema-read-optimization.sql` once to the intended database. It adds three indexes; it does not alter balances or delete history. Index creation itself consumes production operations, so schedule after reset.
3. Deploy `backend/worker.js`, preserving configuration and secrets. Old clients continue receiving full snapshots.
4. Close the previous application and launch the 1.28.6 executable. It uses the existing saved profile configuration. Do not run two copies for the same profile.
5. Perform a small production check: initial snapshot, unchanged poll, one normal movement, and refresh from a second client. Compare D1 Row Metrics before and after. These checks consume production operations.

Rollback: restore the prior Worker and/or app. The indexes can remain. Updated clients fall back to fetching carrier configuration separately if the old Worker omits it. No runtime inventory migration is required.

## Local validation

`tests/backend-tests.mjs` runs the actual Worker SQL against local SQLite. It verifies unchanged-poll query exclusion, change invalidation (including a backdated transit event), group/player isolation, registry updates and covering-index query plans, alongside accounting, retries and reconciliation.

`build.ps1` compiles the executable and runs Windows regression suites. Shared-client tests cover the old response format, combined registry snapshots, unchanged balances, durable outbox recovery and lost acknowledgments. All HTTP transports in these shared-client checks are mocked.
