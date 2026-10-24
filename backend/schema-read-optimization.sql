-- Additive indexes only. Run once before deploying the optimized Worker.
CREATE INDEX IF NOT EXISTS carrier_ledger_group_sequence ON carrier_ledger(group_id,sequence);
CREATE INDEX IF NOT EXISTS ledger_group_sequence ON ledger(group_id,sequence);
CREATE INDEX IF NOT EXISTS transit_group_sequence ON transit_events(group_id,sequence);
