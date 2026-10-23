# Compact view — 1.28.2

Choose **View → Compact view**. The default compact size is **510 × 440 pixels**, with a **480-pixel minimum width**. Resize it to suit your screen. **View → Full view** restores the wider layout. Each player remembers their own view, sizes, expanded panels, visible cargo columns and totals preference; new profiles start with Full view.

## Everyday controls

The project selector, carrier selector, collection switch, Stock editor, Refresh, Settings and View remain available in Compact view. **Controls ▸** expands the Player selector/Add player, Automatic/Manual tracking, Carriers / review, Inbound destination, Always on top and automatic-refresh options. Collapse Controls to reclaim table space. To change the destination for future station purchases, expand Controls; the destination is not silently changed by collapsing it.

The collection switch is shortened to **Collect to carrier** in Compact view. It is the same saved **Collecting for carriers** setting, with the same opt-in purchase rules. Changing view does not start or stop tracking, alter stock, switch players/carriers/projects, or change the collection destination.

## Narrow columns

Compact headings are **Commodity | Need | Stock | In | Out | Collect**. Hover a heading for its full meaning. Need means To deliver; In and Out mean Inbound and Outbound. Long commodity names may be shortened on screen; hover the row for the name and accounting explanation. Numeric columns grow for larger displayed amounts, rather than truncating quantities; unusually wide values can require horizontal scrolling.

Under View, toggle **Show Inbound**, **Show Outbound** and **Show totals** independently. Hiding a column only changes the display: its tracked quantities still count toward To collect. Keep both flows visible when investigating an unexpected collection total. Full view uses the longer headings and the same accounting.

## Collapsed status

The bottom status strip shows Local/Synced/Syncing/Attention, tracking mode, pending shared changes and review count. Review includes carrier movement review and uncertain Raven reports; pending Raven uploads are indicated separately. Click the strip to expand project refresh, cargo tracking, shared inventory and Raven reporting details. Hover it to read all four statuses without expanding.

Errors, genuinely paused tracking, stock discrepancies and Raven review warnings remain visible in a separate alert line while details are collapsed. Click the alert to expand details. A shared synchronization failure is marked as an error, rather than appearing Synced. Use the appropriate Settings/review control to resolve it; the compact strip is a display, not a new Sync action.

Expanding controls/details may increase the minimum height to leave a usable table. Collapse them to lower that minimum and resize the window smaller if desired. Preferences are stored in the current profile's view.json alongside existing settings. No key, inventory, queue or server migration is required for this desktop-only update.

## Quiet journal status and local diagnostics

Routine waiting for a game session is not an alert. When Automatic tracking has no active commander journal, the compact strip shows **Idle**; the expanded cargo status explains that no active journal is available. It does not repeatedly open/close the red warning line. A matching commander already observed in the latest journal remains recognized after restarting the application even if the game has not appended a new line yet.

A confirmed active commander different from the selected Player is still a visible tracking warning: choose the correct profile or game account. Actual journal read failures also remain visible until polling succeeds; a shared-stock refresh cannot briefly erase them.

Use **View → Tracking log…** to open a read-only snapshot of the current player's local diagnostics. The file is tracking-status.log beside that player's settings, normally under %LOCALAPPDATA%\ColonizationNeeds\profiles\<profile-id>. It records timestamped tracking state transitions in UTC, not every two-second poll or full journal contents, and is cleared/reused after approximately 128 KiB. It contains no configured API keys or access tokens and is not uploaded to the backend. Close and reopen the viewer to refresh its contents. Inbound/Outbound stock/cargo receipts and their shared synchronization are separate from this diagnostic log.

Version 1.28.4 guards FitViewColumns during native row replacement and recursive resizing. The same six cells and hidden-column preferences are retained. Switching between an individual project and All active projects requires no stock reset.
