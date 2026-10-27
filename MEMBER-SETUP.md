# Member setup — 1.28.6

In **version 1.29**, use **Projects...** beside the project dropdown to choose which projects feed the combined rollup. Choices are separate per commander/PC and leave shared stock, Inbound/Outbound and Raven status unchanged. Individual projects remain viewable. See [Project selection](PROJECT-SELECTION.md). No backend update is required for this feature.

1. Close the old app and run the supplied 1.28.6 executable. Your existing settings and keys are retained; do not move Legacy stock again after upgrading.
2. In Settings enter your exact Raven commander, your own Raven key when needed, and your game's journal folder. Refresh and select your individual project or All active projects.
3. Open Settings → Shared inventory. Use the leader's HTTPS service URL and your own player token; enable sharing, test and Save. The leader's admin configures carriers under Carriers / review; you receive that registry automatically. Each player needs their own identity/token.
4. Select Automatic. Before buying carrier supplies at a station, enable Collecting for carriers and choose a carrier or Either carrier. Leave it off for personal purchases. The choice persists per player. Changing it affects future purchases, not existing loads.
5. Watch Stock, Inbound and Outbound. Inbound becomes Stock when deposited; Stock becomes Outbound when withdrawn. A delivery clears your cargo and updates To deliver through construction observations/Raven. To collect subtracts all three supplies. Either-carrier Inbound appears only in All carriers.

Members can Add/Remove stock and clear their own Inbound or Outbound in Edit inventory. Shared Set total, Legacy migration and carrier configuration require an admin. A rejected pending stock edit can be cancelled under Settings → Shared inventory → Cancel conflicted edit. Synchronization is automatic; no separate Sync button is required.

Run the correct Player profile for the commander currently playing. Configure Raven reporting only if you want this app to upload confirmed contributions/depot/completion; avoid another uploader for that commander. Start planned construction remains separate. See CARRIER-TRACKING.md for mixed cargo, losses, missing events and corrections, and RAVEN-WORKFLOW.md for construction setup/reporting.

**What the collection switch controls:** it marks new ordinary-station purchases as Inbound while Automatic tracking is active. Leave it off for personal purchases/trading; you do not need to turn it off to deliver. Deposits to a configured carrier always increase Stock, even with the switch off, and carrier withdrawals/deliveries still track normally. Turning it on later does not retroactively mark cargo already aboard. Turning it off does not erase existing loads. Any personal/trading goods deposited on a configured carrier also count as Stock.

For a smaller overlay choose **View → Compact view**. Use Controls to reveal player/tracking/destination settings and click the bottom status strip for details. Columns/totals can be hidden through View; accounting is unchanged. These preferences are saved independently per player. See [Compact view](COMPACT-VIEW.md).

