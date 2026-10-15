-- Additive, no stock/history replay. Confirmed movements after this migration start transit.
CREATE TABLE IF NOT EXISTS transit_events (
 sequence INTEGER PRIMARY KEY AUTOINCREMENT,
 group_id TEXT NOT NULL,
 request_id TEXT NOT NULL,
 player_id TEXT NOT NULL REFERENCES players(id),
 kind TEXT NOT NULL CHECK(kind IN ('withdraw','consume','cap','snapshot','reset')),
 carrier_id TEXT NOT NULL DEFAULT '',
 commodity TEXT NOT NULL DEFAULT '',
 amount INTEGER NOT NULL DEFAULT 0 CHECK(amount BETWEEN 0 AND 1000000000),
 cargo TEXT NOT NULL DEFAULT '{}',
 observed_at TEXT NOT NULL,
 UNIQUE(group_id,request_id)
);
CREATE INDEX IF NOT EXISTS transit_group_order ON transit_events(group_id,observed_at,sequence);
CREATE TRIGGER IF NOT EXISTS carrier_transit_capture AFTER INSERT ON carrier_ledger
WHEN NEW.source IN ('Market','Transfer') AND NEW.after_balance<>NEW.before_balance
BEGIN
 INSERT INTO transit_events(group_id,request_id,player_id,kind,carrier_id,commodity,amount,observed_at)
 VALUES(NEW.group_id,NEW.request_id,NEW.player_id,CASE WHEN NEW.operation='remove' THEN 'withdraw' ELSE 'consume' END,NEW.carrier_id,NEW.commodity,NEW.amount,NEW.observed_at);
END;
