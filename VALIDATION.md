# Desktop validation — 1.28.2

QuietStatusUITests verifies saved-cursor startup retains the observed commander without new rows, Shutdown produces quiet Idle, repeated stock refresh/poll cycles produce no text changes or duplicate log entries, a real different commander remains a stable visible warning, stock refresh cannot clear a tracking failure and a successful matching poll clears it. The full Windows accounting/profile/DPAPI/shared-client/Compact UI/Raven regression suite passed. Backend and database are unchanged.

---

# Desktop validation — 1.28.1

Full Windows build and existing accounting/profile/shared-client/Raven regression checks passed. CompactUITests verifies 510 × 440 layout, narrow numeric columns, expandable controls/details, visible errors while collapsed, independent hidden columns/totals, unchanged collection arithmetic, saved size/preferences and profile isolation. Bounds assertions prevent overlap among the table, totals and status strip. A compact preview was rendered and visually checked; native combo content is not fully represented by DrawToBitmap, so control selections are checked programmatically.

The Worker and database are unchanged; backend integration results below remain the earlier accounting validation. No production writes or deployment were performed for this desktop-only release.

---

# Validation — 1.28

Full Windows build and regression checks passed: application/profile isolation and DPAPI encryption, durable journal positions, market/direct transfer classification, carrier discovery/setup UI, legacy stock preservation, shared retries/lost acknowledgment, Raven contribution/depot/completion reporting and UI arithmetic/layout.

InboundTests covers opted-in ordinary-station purchases, personal/unknown-context/carrier exclusion, mixed Inbound/Outbound, partial deposit and delivery priorities, separate clearing, duplicate replay and persistence across restart. UI checks exercise collection intent save/reload, Either-carrier filtering and six-column To collect arithmetic. Prior Outbound tests cover the 16,260-tonne stocked/withdrawn/delivered example, snapshots, returns, losses and no second carrier debit on delivery.

Backend integration tests passed **99 HTTP assertions** plus direct balance/SQL checks using the real Worker and SQLite triggers. They cover member/admin/group isolation, two carriers, partial Inbound deposits with simultaneous Outbound, shared owner isolation, caps/losses, duplicates, delayed event arrival, old correction receipts, legacy lost acknowledgments and reconciliation cutoffs.

No real in-game station purchase, carrier sale/transfer or construction delivery was performed by automated tests. A small live end-to-end cycle remains a user/game validation step. Read-only production preservation/capability checks and the actual deployed version are recorded in DEPLOYMENT.md. No stock or cargo test mutations are written in production.
