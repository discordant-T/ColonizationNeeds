# Deployment and upgrade notes

## v1.28.6 backend update

The owner confirmed that the shared-read optimization Worker and additive indexes were applied on October 23, 2026. The 1.29 and 1.29.1 desktop changes require no additional Worker deployment or database migration after the v1.28.6 backend update. For older deployments, see [Shared read optimization](READ-OPTIMIZATION.md).

The source-and-build package includes the desktop application, shared Worker source, additive schemas and setup documentation. Keep the package free of credentials, player settings, runtime inventory, journal files, diagnostic logs or private deployment recovery records.

## Desktop upgrade

Close the previous executable and run the [Windows build listed in the README](README.md). Saved player profiles, encrypted keys, stock, carrier configuration, journal cursors, outboxes and view preferences remain under Local AppData. Do not repeat Legacy stock migration after 1.27.x.

## Shared backend upgrade

For a fresh installation apply schema.sql, schema-v2.sql, schema-transit.sql and schema-read-optimization.sql, then deploy backend/worker.js with your own D1 database bound as DB and your own OWNER_TOKEN secret configured. Existing 1.27.2–1.28.5 databases need the additive schema-read-optimization.sql indexes and the current Worker; see READ-OPTIMIZATION.md. Keep existing users, token hashes, registry, stock and ledgers. Check health for schema 2, transit true and inbound true. See backend/README.md and INSTALLATION.md.

Record your current Worker version and an up-to-date database recovery bookmark privately before changing a production deployment. Never publish secrets or restore an old bookmark casually: restoration overwrites later activity. Publishing a desktop build does not deploy the Worker or alter the live database.

## Validation

For v1.28.6, the Windows regression suite and 109 local Worker HTTP assertions passed. Tests cover per-player isolation, partial/mixed Inbound/Outbound, ship loss, shared retries, legacy migration, compact UI, quiet status, and Raven reporting. No real in-game end-to-end trade/delivery cycle was performed by automated tests. Confirm a small cycle with the correct profile and carrier setup before general use.


See [Validation](VALIDATION.md) for desktop regression results.
