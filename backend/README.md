# Install the shared inventory backend

This backend is prepared for https://colonizationneeds-api.macytr.workers.dev with a D1 binding named DB. It has not been deployed or tested on that account. The Windows application still uses local inventory until shared-client support is added.

1. In Cloudflare, open Storage & databases > D1 > colonizationneeds-db > Console. Paste schema.sql and execute it. The script creates tables without removing existing data.
2. Open Workers & Pages > colonizationneeds-api > Edit code. Replace the starter Worker source with worker.js, then Deploy. Keep the DB binding.
3. Open https://colonizationneeds-api.macytr.workers.dev/health. Successful setup returns JSON containing ok: true and schema: 1. The root URL returns Endpoint not found; this is expected.
4. Before creating player credentials, add a Worker secret called OWNER_TOKEN containing a long randomly generated value (at least 32 random bytes). Keep it private. This is an administration secret for this service, not a Cloudflare account API token. It must never be distributed to players or committed to GitHub.

API: POST /admin/players accepts owner Bearer authorization and {groupId,name,role} with role member or admin. It returns a generated accessToken once. The database stores only a SHA-256 hash of each player token. Revoke access by setting that player's active field to zero in the dashboard.

Player APIs require Authorization: Bearer <player token>. GET /inventory returns balances, per-commodity versions and a ledger cursor. GET /ledger?after=0 returns up to 200 transactions in sequence order; continue from the last sequence for further pages. POST /transactions accepts {requestId,commodity,operation,amount,source,expectedVersion}. Commodity uses the game's lowercase symbol. Operation is add, remove, set or colonize. Source is Manual, Collect or Colonize. Reuse the same requestId and payload when retrying a failed request. Set requires admin access and the latest commodity version, avoiding overwriting another player's concurrent change. Remove rejects insufficient stock; colonize clamps stock to zero and reports a shortfall. Quantities are limited to one billion tonnes.

Inventory and ledger are updated atomically through a database trigger. No Raven Colonial requests are made by this backend. Stock is an estimate following Collect/Colonize rules; reconciliation is manual. Browser CORS access is not enabled; the intended client is the Windows app. No public endpoint creates accounts or exposes inventory without a token.

Validation: Worker JavaScript syntax checked; SQLite tests cover ledger-trigger updates, duplicate retries, removal underflow, stale reconciliation versions and Colonize shortfalls. Live D1 deployment and end-to-end authorization remain unverified.
