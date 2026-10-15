-- Additive migration: do not remove or rewrite the original inventory/ledger/players tables.
CREATE TABLE IF NOT EXISTS carrier_groups (group_id TEXT PRIMARY KEY);
CREATE TABLE IF NOT EXISTS carrier_registry_versions (
 group_id TEXT PRIMARY KEY,
 version INTEGER NOT NULL DEFAULT 0,
 request_id TEXT NOT NULL DEFAULT '',
 payload_hash TEXT NOT NULL DEFAULT ''
);
CREATE TABLE IF NOT EXISTS carrier_registry (
 group_id TEXT NOT NULL,
 carrier_id TEXT NOT NULL,
 name TEXT NOT NULL,
 role TEXT NOT NULL CHECK(role IN ('Squadron','Personal')),
 callsign TEXT NOT NULL DEFAULT '',
 last_seen TEXT NOT NULL DEFAULT '',
 verified INTEGER NOT NULL DEFAULT 0,
 PRIMARY KEY(group_id,carrier_id),
 UNIQUE(group_id,role)
);
CREATE TABLE IF NOT EXISTS carrier_inventory (
 group_id TEXT NOT NULL,
 carrier_id TEXT NOT NULL,
 commodity TEXT NOT NULL,
 quantity INTEGER NOT NULL DEFAULT 0 CHECK(quantity BETWEEN -1000000000 AND 1000000000),
 version INTEGER NOT NULL DEFAULT 0,
 reconciled_at TEXT NOT NULL DEFAULT '',
 PRIMARY KEY(group_id,carrier_id,commodity)
);
CREATE TABLE IF NOT EXISTS carrier_ledger (
 sequence INTEGER PRIMARY KEY AUTOINCREMENT,
 group_id TEXT NOT NULL,
 request_id TEXT NOT NULL,
 player_id TEXT NOT NULL REFERENCES players(id),
 carrier_id TEXT NOT NULL,
 commodity TEXT NOT NULL,
 operation TEXT NOT NULL,
 amount INTEGER NOT NULL CHECK(amount BETWEEN 0 AND 1000000000),
 source TEXT NOT NULL,
 observed_at TEXT NOT NULL DEFAULT '',
 before_balance INTEGER NOT NULL,
 after_balance INTEGER NOT NULL CHECK(after_balance BETWEEN -1000000000 AND 1000000000),
 reconciled_at TEXT NOT NULL DEFAULT '',
 created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
 UNIQUE(group_id,request_id)
);
CREATE TRIGGER IF NOT EXISTS carrier_ledger_update AFTER INSERT ON carrier_ledger
BEGIN
 UPDATE carrier_inventory SET quantity=NEW.after_balance,version=version+1,
 reconciled_at=CASE WHEN NEW.operation='set' THEN NEW.reconciled_at ELSE reconciled_at END
 WHERE group_id=NEW.group_id AND carrier_id=NEW.carrier_id AND commodity=NEW.commodity;
END;
