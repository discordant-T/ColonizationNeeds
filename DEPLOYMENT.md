## v1.28.6 backend update

The owner confirmed the shared-read optimization Worker and additive indexes were applied on October 23, 2026. This GitHub publication does not redeploy or migrate production. Existing installations that have not upgraded should follow READ-OPTIMIZATION.md (in the repository root); preserve all existing stock, ledgers, users, bindings and secrets.

# Deployment and upgrade notes — v1.28.6

This release includes the desktop application, shared Worker source, additive schemas and setup documentation. It does not contain credentials, player settings, runtime inventory, journal files, diagnostic logs or private deployment recovery records.

## Desktop upgrade

Close the previous executable and run v1.28.6. Saved player profiles, encrypted keys, stock, carrier configuration, journal cursors, outboxes and view preferences remain under Local AppData. Do not repeat Legacy stock migration after 1.27.x. The compact/status changes in 1.28.1/1.28.2 require no backend migration.

## Shared backend upgrade

For a fresh installation apply schema.sql, schema-v2.sql, schema-transit.sql and schema-read-optimization.sql, then deploy backend/worker.js with your own D1 database bound as DB and your own OWNER_TOKEN secret configured. Existing 1.27.2–1.28.5 databases need the additive schema-read-optimization.sql indexes and the current Worker; see READ-OPTIMIZATION.md. Keep existing users, token hashes, registry, stock and ledgers. Check health for schema 2, transit true and inbound true. See backend/README.md and INSTALLATION.md.

Record your current Worker version and an up-to-date database recovery bookmark privately before changing a production deployment. Never publish secrets or restore an old bookmark casually: restoration overwrites later activity. Publishing this GitHub release does not redeploy or alter an existing live database.

## Validation

The Windows regression suite and 109 local Worker HTTP assertions passed. Tests cover per-player isolation, partial/mixed Inbound/Outbound, ship loss, shared retries, legacy migration, compact UI, quiet status, and Raven reporting. No real in-game end-to-end trade/delivery cycle was performed by automated tests. Confirm a small cycle with the correct profile and carrier setup before general use.
