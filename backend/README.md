## v1.28.6 backend update

The owner confirmed the shared-read optimization Worker and additive indexes were applied on October 23, 2026. This GitHub publication does not redeploy or migrate production. Existing installations that have not upgraded should follow READ-OPTIMIZATION.md (in the repository root); preserve all existing stock, ledgers, users, bindings and secrets.

# Shared backend — 1.28.6

Cloudflare Worker plus D1, with the database binding named DB and OWNER_TOKEN stored as a Cloudflare secret. Never embed owner/player/Cloudflare credentials in source or ship them in the Windows executable.

For an independent new service, create your own Cloudflare Worker and D1 database, bind the database as DB, and configure your own OWNER_TOKEN secret. When creating players, run Create-Player.cmd -ServerUrl https://YOUR-WORKER.workers.dev -GroupId YOUR-GROUP (or Create-Player.ps1 with those arguments). Supply your own URL: the script defaults to the original project service. Members only need the executable, your service URL and their own player token; solo local use needs no backend.

## Install or upgrade

1. For a fresh database apply schema.sql, schema-v2.sql, schema-transit.sql and schema-read-optimization.sql in that order. They use additive IF NOT EXISTS definitions. Existing v1 services need the missing carrier/transit migrations. Existing 1.27.2–1.28.5 databases also need schema-read-optimization.sql before deploying the current worker.js.
2. Keep the existing DB binding, OWNER_TOKEN, players/token hashes, original inventory/ledger, carrier registry and cargo events. Do not create a new database or reset user stock for an upgrade. Record the active version/recovery information before changing production.
3. Deploy worker.js to the existing service. Verify GET /health: ok true, schema 2, transit true, inbound true. Check authenticated inventory/carrier reads using existing player identities. Test mutations locally, not against production stock.
4. Create player tokens with Create-Player.cmd or Create-Player.ps1. The script prompts for the owner secret with hidden input. Use the same GroupId for collaborating commanders, unique player identities/tokens, and member or admin role as appropriate. A member never needs the owner secret or Cloudflare sign-in.
5. Members configure HTTPS Worker root URL and their player access token in Settings → Shared inventory. The app uses Bearer authentication and DPAPI-encrypted local settings. Admins save the shared Squadron/Personal carrier registry; members adopt it automatically. Upgrade hauling clients to 1.28.6.

## API and storage

| Route | Behavior |
|---|---|
| GET /health | Public capability/schema check. Root / is not a health endpoint. |
| POST /admin/players | Owner-secret authorization only; create a player token. |
| GET/POST /v2/carriers | Group registry; POST admin-only, expectedVersion/requestId CAS, two unique carrier IDs/roles maximum. |
| GET /v2/inventory | One consistent snapshot of carrier/Legacy stock, Outbound in transit array, Inbound in inbound array. |
| POST /v2/transactions | Stable stock receipt: carrierId, commodity, operation, amount, source, observedAt for automatic events. Set/migrate admin-only with expectedVersion. |
| GET /v2/ledger?after=0 | First 200 subsequent stock receipts. |
| POST /v2/transit | Stable per-player cargo receipt: inbound, consume, cap, snapshot or reset. |

The original players/inventory/ledger tables remain intact. Carrier inventory and receipts are additive. The stock-ledger trigger atomically records carrier withdrawals/returns in transit_events. Newly opted-in station purchases use the same event ledger: protocol inbound is stored as kind withdraw with carrier_id inbound:<destination> (numeric configured MarketID or any). No extra migration or destructive CHECK constraint rewrite is needed. Snapshot responses split these tagged lots from Outbound so older clients do not mislabel Inbound as Outbound.

The chronological fold is per group/player, with observed time then sequence ordering. Inbound receipts require a configured destination or any. Deposits prioritize destination/Either Inbound, then origin Outbound, then other lots; deliveries/removal prefer Outbound then Inbound. Snapshot caps apply to total ship cargo and cannot credit personal cargo. Caps tagged inbound clear only that flow; old caps without a flow remain compatible with Outbound-only correction, including previously acknowledged empty-carrier receipts.

Automatic Set total cutoffs exclude older stock events from affecting corrected balances or generating new ship lots. Stable request IDs and immutable receipt comparisons handle retries/collisions. Original v1 lost-acknowledgment receipts are recognized before old queues affect Legacy stock again. Groups that adopted carrier tracking reject old v1 stock mutation APIs; unmigrated groups retain legacy behavior. Registry version checks and stock/history protections remain.

Player journals are trusted client observations, not an authoritative live query of all carrier storage. Outsider activity and absent logs require reconciliation. No historical purchase replay/backfill occurs. See ../CARRIER-TRACKING.md for application semantics and limitations, ../INSTALLATION.md for leader setup and ../DEPLOYMENT.md for actual deployed versions.

## Local validation

node tests/backend-tests.mjs <python-executable> executes real Worker routes and SQL through a SQLite D1 adapter. It uses isolated synthetic players and inventory, never the live service. Windows build.ps1 covers the desktop/outbox/profile/DPAPI/UI/reporting paths. Keep all tests free of real credentials.


Client 1.29 project selection is a local per-commander preference, not a server group setting. It requires no Worker/database deployment and does not modify shared stock or reporting. See [Project selection](../PROJECT-SELECTION.md).
