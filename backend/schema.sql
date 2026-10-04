CREATE TABLE IF NOT EXISTS players (
  id TEXT PRIMARY KEY,
  group_id TEXT NOT NULL,
  name TEXT NOT NULL,
  token_hash TEXT NOT NULL UNIQUE,
  role TEXT NOT NULL CHECK(role IN ('member','admin')),
  active INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE IF NOT EXISTS inventory (
  group_id TEXT NOT NULL,
  commodity TEXT NOT NULL,
  quantity INTEGER NOT NULL DEFAULT 0 CHECK(quantity >= 0 AND quantity <= 1000000000),
  version INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY(group_id, commodity)
);
CREATE TABLE IF NOT EXISTS ledger (
  sequence INTEGER PRIMARY KEY AUTOINCREMENT,
  group_id TEXT NOT NULL,
  request_id TEXT NOT NULL,
  player_id TEXT NOT NULL REFERENCES players(id),
  commodity TEXT NOT NULL,
  operation TEXT NOT NULL CHECK(operation IN ('add','remove','set','colonize')),
  amount INTEGER NOT NULL CHECK(amount >= 0 AND amount <= 1000000000),
  source TEXT NOT NULL,
  before_balance INTEGER NOT NULL,
  after_balance INTEGER NOT NULL CHECK(after_balance >= 0 AND after_balance <= 1000000000),
  created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  UNIQUE(group_id, request_id)
);
CREATE INDEX IF NOT EXISTS ledger_group_sequence ON ledger(group_id, sequence);
CREATE TRIGGER IF NOT EXISTS ledger_update_inventory AFTER INSERT ON ledger
BEGIN
  UPDATE inventory SET quantity = NEW.after_balance, version = version + 1
  WHERE group_id = NEW.group_id AND commodity = NEW.commodity;
END;
