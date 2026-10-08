# ColonizationNeeds

**Current stable version: 1.25.** A compact Windows application for Elite Dangerous colonization requirements and tracked carrier inventory. Source and a ready-to-run executable are included in this public repository.

Download [ColonizationNeeds.exe](https://github.com/discordant-T/ColonizationNeeds/raw/refs/heads/main/bin/ColonizationNeeds.exe), or use **Code > Download ZIP** and run `bin\ColonizationNeeds.exe`. The executable can run from any folder by itself. Requires Windows with .NET Framework 4.8, normally included in Windows 10/11; no installer, SDK, Python or Electron is needed. The executable is unsigned.

## Guides

- [Solo setup](SOLO-SETUP.md): local inventory on personal or squadron carriers, without Cloudflare.
- [Squadron leader setup](INSTALLATION.md): Raven account, Cloudflare Workers/D1, owner secret, player tokens and administration.
- [Member setup](MEMBER-SETUP.md): download, Raven account and the leader's shared inventory connection.
- [Raven construction workflow](RAVEN-WORKFLOW.md): start planned sites, report deliveries, complete constructions and troubleshoot.
- [Release notes and upgrading](RELEASE-NOTES.md).
- [Development project](PROJECT.md) and [shared backend reference](backend/README.md).

## Account and project selection

In **Settings**, save your commander name exactly as shown on Raven Colonial. This is the account identifier, not a numeric website user ID. Public project reads may work without a key; your own Raven API key is required for construction setup and delivery/completion reporting. A Cloudflare token is not a Raven key.

Set the Elite Dangerous journal folder, normally `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous`. The game commander must match the configured commander. Select an individual active Raven project or **All active projects (combined)**. Refresh manually or enable **Refresh every minute** to update project data; this does not query actual carrier storage. Shared stock polls separately, approximately every five seconds.

## Commodity columns and inventory

Commodities are grouped by type, with totals in tonnes at the bottom. Double-click a commodity or use **Edit inventory** to **Add**, **Remove**, or **Set total**, with a preview before applying. Shared Set total requires the group's admin role.

| Column | Manual / Collect | Colonize |
| --- | --- | --- |
| Required | Raven's outstanding commodity requirement | Original construction RequiredAmount, fixed despite deliveries |
| Carrier stock | Tracked local or shared carrier balance | Same tracked balance |
| Still needed | Outstanding requirement minus stock, minimum zero | Quantity still to deliver; carrier stock is not subtracted |

In Colonize, original totals are recovered from Raven depot data, new local depot observations, saved totals, or existing journals for that commander and MarketID. Recovery reads original requirements only: it never replays deliveries, cargo loads or inventory transactions. If none of those sources contains the original totals, Required displays **Unknown** until a depot observation supplies them. Totals remain saved across refreshes and restarts. Combined views add each selected project's requirements; one commodity's surplus never offsets another's shortage.

Positive Still needed is bright cyan in Colonize and red in Manual/Collect; known zero stays green. Depot remaining is RequiredAmount minus ProvidedAmount. Generic Raven metadata timestamps do not override a confirmed newer game observation; without newer depot evidence, the smaller known remaining balance is used.

## Cargo modes

Choose a mode before loading cargo and keep the app running.

| Mode | Newly loaded ship cargo |
| --- | --- |
| Manual | Does not change tracked stock automatically |
| Collect | Adds the loaded amount, assuming it will be deposited into the carrier |
| Colonize | Deducts the loaded amount, assuming it came from the carrier |

Loading 20 t of Steel changes a 100 t balance to 120 t in Collect or 80 t in Colonize. Shortfalls clamp automatic Colonize stock to zero and require manual correction. Unloads, repeated snapshots and construction deliveries do not apply another carrier-stock change. Existing cargo at startup, historical acquisitions and SRV acquisitions are not imported. Startup and mode/account changes establish a fresh baseline.

These modes estimate carrier inventory at ship loading. They cannot verify a completed carrier deposit or identify the source carrier. Manually correct station purchases, losses, cancelled deposits or returned loads that violate the assumptions. Reconcile periodically with actual carrier storage; squadron carrier stock has no authoritative automatic reconciliation here. Run one tracker per game session.

## Start, deliver and complete constructions

**Settings > Start planned construction** works in every cargo mode. Save your Raven key, dock at the site, open Construction Services, select the matching planned Raven site and click **Start / verify**. The planned type must be exact; resolve variants ending in `?` on Raven first. The app creates or reuses the project, preserves the type, links your commander, marks the plan Building and verifies all three. Refresh the main window afterward. Setup is an explicit action and does not require the reporting checkbox. See the [workflow guide](RAVEN-WORKFLOW.md) for recovery and identity conflicts.

Enable **Settings > Report deliveries to Raven Colonial** with your own key to report new confirmed contribution events and authoritative depot requirements in every mode, with local or shared inventory. Disable overlapping reporting in SrvSurvey/BGS-Tally/other tools to avoid duplicate credit. The app does not publish manual carrier edits or ship loading as project deliveries.

When a new depot event reports `ConstructionComplete: true`, the app calls Raven's completion endpoint and verifies the result. Zero outstanding commodities alone do not trigger completion. Final credits are processed first; an uncertain contribution must be reviewed before completion proceeds. Completed projects leave Raven's active list on refresh.

**Settings > Delivery reports** lists contributions and completion records; **Include depot requirement updates** also shows snapshots. An uncertain contribution is never retried automatically: check Raven history, then mark it recorded if present or retry only if absent. Pending work is saved per commander and resumes after reconnection/restart. Historical deliveries and completion events are not uploaded automatically.

## Local and shared inventory

Local mode works entirely without Cloudflare, including solo use of a squadron carrier. Counts are saved separately per commander and used across project views.

Shared mode uses a Cloudflare Worker and D1 database with individual player tokens and an auditable ledger. Both players must connect to the same group; each still uses their own Raven account/project list. **View ledger** displays shared changes. The leader can use **Sync local to shared** to preview and replace shared balances with local counts; it does not add the inventories, and shared-only commodities become zero. Concurrent conflicts require review; replacement is per commodity, so an interrupted import can be partly applied. Switching modes does not itself copy either inventory.

Keep the owner token with the leader; distribute individual player tokens only. No Cloudflare setup is needed for local mode. See the leader/member guides for installation and token management.

## Window and saved data

The compact window uses a near-black/amber palette, themed controls and readable selection colors. Refresh preserves selection and scroll position. **Always on top**, saved window location and an opacity slider from 30% to 100% support overlay use. Settings stays opaque; Cancel restores the previous opacity. Use borderless/windowed game mode if exclusive fullscreen covers desktop windows.

Settings, inventories, reporting queues, requirements and window state are stored under `%LOCALAPPDATA%\ColonizationNeeds`, independently of the executable. Credentials are encrypted with Windows DPAPI for the current Windows user. Enter credentials separately on another computer; copying the exe does not synchronize local inventory or pending queues. Shared inventory provides multi-computer stock synchronization.

## Upgrade and validation

Close the running app, replace only the executable, then restart. Existing saved data is retained. Version 1.25 needs no Cloudflare schema migration or backend redeployment. Windows file properties identify version 1.25.0.0; the filename remains ColonizationNeeds.exe.

The construction-start workflow was verified live on Gernhardt Beacon (Military Outpost, nemesis), including commander linkage and the plan's Building status. The user confirmed completion reporting working across several constructions. Automated checks cover journal isolation/replay prevention, original-total recovery, cargo arithmetic, construction identity/type conflicts, duplicate creation recovery, delivery reporting, completion verification and lost acknowledgments. These observations do not certify every player's Cloudflare deployment; test your group's connection independently.

Build from this folder with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`. It compiles the executable and runs self-tests plus mocked reporting checks. `--test-key-protection` separately checks DPAPI in a normal signed-in Windows session. IDE builds require the .NET Framework developer tools.

API references: [Raven web client](https://github.com/njthomson/RavenColonialWeb/tree/main/src/api), [SrvSurvey construction creation](https://github.com/njthomson/SrvSurvey/blob/main/SrvSurvey/forms/FormNewProject.cs), [completion reporting](https://github.com/njthomson/SrvSurvey/blob/main/SrvSurvey/game/ColonyData.cs). This independent application is not affiliated with Raven Colonial or Frontier.
