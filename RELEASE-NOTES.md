# 1.29 - Active project selection 

- Project selection was developed from the verified 1.28.6 package, then combined with current GitHub main to preserve newer shared-sync changes and fresh-install guide corrections.
- Added Projects beside the project selector in Compact and Full view. Save a per-commander fixed subset or include all current/future Raven-active projects automatically. Empty selection stays empty. Individual excluded projects remain viewable.
- Combined quantities and To collect use only the chosen projects. Stock, Inbound, Outbound, ledger events and Raven reporting are unchanged. No backend deployment or inventory migration is needed.
- Stable build-ID choices survive renames, refreshes, restart and commander changes. New projects are excluded in fixed mode. Cancel preserves existing choices; unreadable files report an error rather than silently including all.
- Build and all carrier, transit, inbound, shared, reporter, status and Compact UI suites pass. New mocked-API UI tests cover the picker, subset/all/empty totals, new/renamed projects, individual inspection, cancellation, per-profile isolation, persistence and unchanged carrier/transit data. No live account data was changed by these tests.
- Full instructions: [Project selection](PROJECT-SELECTION.md). Desktop executable is not automatically replaced. Version 1.29 is available on the repository main branch.

## 1.28.6 — Shared read optimization

Built on existing 1.28.5, retaining responsive player switching. Unchanged syncs skip history replay; full snapshots include carriers. Includes additive indexes and the matching Worker update for savings. The owner confirmed these backend changes were completed on October 23, 2026; publishing this release does not repeat deployment. See READ-OPTIMIZATION.md.

# Release history and upgrade coverage

## 1.28.5 — Responsive player switching

- Drain the outgoing commander's journal in bounded, durable batches so the window can repaint before changing accounts.
- Initialize the incoming journal reader off the window thread. Inventory/reporting callbacks stay on the window thread; account controls remain disabled throughout the switch.
- Bound routine timer polling to 128 complete events per tick. Explicit accounting-mode and collection-intent changes retain their complete pre-change flush.
- Preserve profile data, encrypted credentials, stock, outboxes, event identities and the carrier icon. No backend deployment or migration is required.
- Added a regression check for a 300-event backlog, UI message processing during catch-up and the final durable journal offset.

## 1.28.4 — Project-switch column resizing fix

Fixes the reported NullReferenceException in FitViewColumns during ListView SizeChanged by deferring fitting during row clear/replacement, skipping transient or incomplete rows, and preventing recursive fitting while column widths change. Numeric text is measured before applying native column changes. This package is based on the archived 1.28.3 source whose executable SHA256 exactly matches the user's desktop 1.28.3 executable. Six columns, carrier stock, Inbound, Outbound, transit, profiles and reporting behavior are preserved; no backend or runtime data migration is required.

Validation: complete build/self-test, carrier/transit/inbound/shared/reporter and UI suites pass. Compact UI tests now include 30 alternating row-set rebuilds and resizes, nested column-fit callbacks, partial-row deferral and unchanged saved carrier/transit data. Desktop executable was not replaced automatically.

## 1.28.3 — Carrier icon

- Added a multi-resolution Windows icon based on the supplied white Javelin-Class Carrier image, embedded by both build.ps1 and the IDE project.
- Applied the embedded icon to the main window and taskbar. The executable stays standalone, with no external asset needed at runtime.
- Documented the image source and preparation in assets/README.md. Inventory, credentials, tracking and backend behavior are unchanged.

## 1.28.2 — Quiet and stable tracking status

- Routine no-active-journal waiting now shows neutral Idle and expanded details, without a flashing red alert. Confirmed mismatches and real tracking failures remain visible.
- Centralized cargo status so shared-stock refresh and journal polling cannot alternate normal/waiting messages or erase a paused error. Restored the latest observed journal identity after durable cursor reload without replaying stock/history.
- Added a per-profile, transition-only local tracking-status.log and View → Tracking log reader. No diagnostic backend uploads or new server deployment.
- Added regression coverage for restart with no new journal lines, repeated poll/refresh status stability, quiet idle, diagnostic deduplication and recovery from real errors.

## 1.28.1 — Compact desktop layout

- Added View → Compact/Full, 510 × 440 compact defaults and a 480-pixel minimum width. Kept project/carrier selection, collection switch and essential actions visible.
- Added collapsible Controls for player/tracking/destination/carrier configuration and display options; collapsed detailed statuses into a summary. Errors, pending counts, movement/Raven review and warnings remain accessible while collapsed.
- Shortened compact headings to Need/Stock/In/Out/Collect, with hover descriptions, truncated commodity names, protected numeric widths and independent Inbound/Outbound/totals visibility. Hidden columns still participate in accounting.
- Saved both view sizes, expansion state and visibility per profile in view.json; preserved existing profile, key, inventory and tracking settings. New profiles retain independent default preferences.
- Added narrow-layout, error visibility, overlap, persistence and accounting UI checks. No backend/schema/data changes or redeployment are required. See COMPACT-VIEW.md.

## 1.28 — Inbound and Outbound

Documentation clarification: added a switch-on/off behavior table and a buy-for-trade-then-deposit example to the accounting guide, with matching README/leader/member/solo/Raven guidance. The switch affects new station purchases only; carrier deposits/withdrawals/deliveries continue, existing loads remain tracked, and enabling later does not relabel existing cargo. All observed deposits to configured carriers count as Stock, including personal/trading goods. This clarification changes documentation only; the executable and deployed backend are unchanged.

- Added per-profile Collecting for carriers switch, default off, with named or Either destination. Confirmed new station purchases while Automatic and opted in create Inbound; personal/unknown-context/carrier purchases do not.
- Renamed In transit to Outbound and Carrier stock to Stock. Added Inbound, heading help and a wider layout. To collect subtracts Stock + Inbound + Outbound, bounded at zero. Either-carrier cargo counts only in All carriers.
- Deposits, withdrawals, deliveries, partial/mixed loads, station sales, jettison, loss, snapshots, corrections and shared owner isolation account for both flows without double credit. Separate Clear my Inbound/Outbound corrections preserve stock and other players' cargo.
- Persisted collection intent/destination and cargo across restarts/profile changes. Intent changes process old unread events first; turning off does not erase loads. No historical purchase replay or arbitrary credit of existing ship cargo.
- Extended existing additive transit ledger protocol with inbound receipts; no extra schema migration over 1.27.2. Retained old receipt/correction compatibility, credentials, stock and registry.
- Rewrote current leader/member/solo/profile/Raven/accounting guides, refreshed backend/project/validation/deployment records and documented the full change history.

## 1.27.2 — outbound cargo and clear columns

- Replaced duplicate Required/Still needed display with To deliver and retained original verified totals internally.
- Added shared In transit (now Outbound) for carrier withdrawals, consumed by player-specific delivery, return, sale, jettison, death and available ship snapshots. Added manual transit clearing and durable/retry-safe chronological events.
- Added schema-transit.sql and atomic stock movement trigger. Preserved original tables, stock, users, keys, registry and Raven behavior. No historical stock replay.
- Clarified the blocked pending-edit popup with the actual Settings → Shared inventory cancellation path.

## 1.27.1 — carrier discovery and shared configuration

- Read-only commander-specific journal discovery of carrier MarketID/name/callsign/last seen; manual confirmation fallback.
- Admin-managed Squadron/Personal registry, version-checked saves and member adoption. Protected carrier IDs with stock/history.
- Deployed additive schema 2 to the existing service, preserving player tokens, stock and original ledger. No automatic assignment of Legacy stock to each carrier.

## 1.27 — carrier-based accounting

- Automatic/Manual replaced acquisition-based Collect/Colonize modes. MarketSell and confirmed owner tocarrier transfers add stock; MarketBuy and confirmed toship transfers remove it. Known SRV movements are excluded, ambiguous events reviewed.
- Added two carrier balances, All carriers/individual/Legacy views, manual Add/Remove/Set total and preserving Move from legacy.
- Added stable journal IDs, durable commander/profile cursors, outbox lost-acknowledgment retries, signed discrepancies, reconciliation cutoffs and local process locks. Deliveries never debit stock twice.
- Preserved project/all-project dropdown, Start planned construction, optional Raven contribution/depot/completion reporting, encrypted keys and existing stock.

## 1.26.1 — player profiles

- Multiple commanders on one PC, separate credentials, mode, local/shared stock, reporting queues, journal positions and display settings. New profiles do not inherit credentials. Verified original totals may be reused only for an exact project ID; remaining observations are isolated.

## Earlier retained behavior

- Planned construction validates location/type, creates or reuses a plan/project, links the commander and verifies Building status independently of cargo mode/reporting.
- Raven uploads confirmed new contributions/depot snapshots and game-confirmed completion; durable state, stale-observation checks and uncertain-outcome review prevent accidental replay.
- Dark amber UI, categories, opacity/location, scroll/selection retention, automatic refresh and Windows DPAPI protection remain.

## Upgrade

Close the previous app and run the supplied build on each hauling PC. Existing data stays in Local AppData. Do not repeat Legacy migration or carrier setup after 1.27.x. From an old backend, apply missing additive schema-v2.sql and schema-transit.sql before the current Worker. Existing 1.27.2 databases need only the new Worker. Keep DB bindings/secrets/player tokens unchanged. Fresh clients baseline history and do not assume existing ship cargo. Check a small real station → carrier → construction cycle after upgrading.

The features above are published together in **v1.28.2**. Use [GitHub Releases](https://github.com/discordant-T/ColonizationNeeds/releases/latest) for the executable and source package. README-1.26-reference.md is historical.

