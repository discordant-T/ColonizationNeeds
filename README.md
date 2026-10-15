# ColonizationNeeds 1.28.3

Windows Forms application for Elite Dangerous colonization supplies, with local or shared tracking across a squadron carrier, a personal carrier and multiple commanders on one PC.

Version **1.28.3** adds a carrier icon to the executable, window and taskbar. Both build methods embed `assets/ColonizationNeeds.ico`; no separate icon file is needed when running the executable. Artwork source and preparation are documented in [assets/README.md](assets/README.md).

Run `bin/ColonizationNeeds.exe`. Existing settings, encrypted keys, inventory, carrier selections and reporting queues are reused. Close the earlier app first. Upgrading from 1.27.x does not require repeating carrier setup or moving Legacy stock again. The current published release is **v1.28.3**. Download its executable and source package from [GitHub Releases](https://github.com/discordant-T/ColonizationNeeds/releases/latest).

The table is **Commodity | To deliver | Stock | Inbound | Outbound | To collect**. To collect is `max(0, To deliver - Stock - Inbound - Outbound)` per commodity. Hover the headings for definitions.

Enable **Collecting for carriers** before station purchases and choose their destination. Inbound counts those new purchases until deposited, delivered or removed. The switch defaults off per profile; personal purchases are ignored while off. Automatic tracking records carrier buys/sells and confirmed owner transfers. Withdrawals become Outbound until delivery/return/loss. Manual mode queues carrier movements for review and does not create new Inbound purchases. Turning collection off does not erase existing Inbound.


**What the collection switch controls:** it marks new ordinary-station purchases as Inbound while Automatic tracking is active. Leave it off for personal purchases/trading; you do not need to turn it off to deliver. Deposits to a configured carrier always increase Stock, even with the switch off, and carrier withdrawals/deliveries still track normally. Turning it on later does not retroactively mark cargo already aboard. Turning it off does not erase existing loads. Any personal/trading goods deposited on a configured carrier also count as Stock.

All active projects and individual project selection are retained. To deliver reflects construction requirements, not acquisition. Start planned construction, optional Raven delivery/depot/completion reporting, per-player keys, inventory corrections and discrepancy review remain available.

## Compact display

Choose **View → Compact view** for a 510 × 440 starting window (480-pixel minimum width), short headings, collapsible Controls/status and optional Inbound/Outbound columns and totals. Errors and review counts remain visible. Layout preferences are saved per player. Accounting and the backend are unchanged. See [Compact view](COMPACT-VIEW.md).

Routine waiting for a game journal is quiet: Compact view shows Idle, and details are available in the expanded status section or **View → Tracking log**. This per-player diagnostic log stays on the PC and records state transitions rather than every poll. Genuine tracking failures, stock discrepancies and a confirmed active-commander mismatch remain visible. See [Compact view](COMPACT-VIEW.md).

## Guides

- [Carrier accounting and examples](CARRIER-TRACKING.md)
- [Leader installation and administration](INSTALLATION.md)
- [Member setup](MEMBER-SETUP.md) and [solo setup](SOLO-SETUP.md)
- [Multiple players and PCs](PLAYER-PROFILES.md)
- [Raven construction workflow](RAVEN-WORKFLOW.md)
- [All changes and upgrade notes](RELEASE-NOTES.md)
- [Backend setup](backend/README.md), [validation](VALIDATION.md), [deployment record](DEPLOYMENT.md)

## Build and test

Windows with .NET Framework 4.8 is required. `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1` uses the built-in Windows compiler and runs application/profile/DPAPI, journal, local/shared cargo, UI and Raven-reporting checks. No C# packages are required. Backend integration tests run the actual Worker routes and SQLite triggers locally: `node tests/backend-tests.mjs <python-executable>`. They do not contact production.

The dated `README-1.26-reference.md` is historical documentation, not current setup instructions. No runtime credentials or inventory belong in the source package.
