# ColonizationNeeds

A small native Windows application for Elite Dangerous colonization commodity requirements.

Double-click **bin\ColonizationNeeds.exe**. On first launch, enter your **commander name as shown on Raven Colonial** and an optional API key in Settings, then click Save. This API identifies the account by commander name rather than a numeric website user ID. The app fetches your active builds automatically. Choose one project or **All active projects (combined)**. The app lists outstanding commodities in tonnes, refreshes once per minute, and stays above other windows when **Always on top** is checked. Resize or move it wherever you prefer. Use Elite Dangerous in borderless/windowed mode to keep it visible; exclusive fullscreen can cover desktop windows.

The table shows commodity names and three quantity columns: **Required**, **Carrier stock**, and **Still needed**. Required is the outstanding project quantity reported by Raven Colonial, rather than the original construction cost including previous deliveries. Double-click a commodity (or select it and click **Edit inventory**) to Add, Remove, or Set total tonnes in your tracked carrier stock. Still needed is `max(Required − Carrier stock, 0)` for each commodity. The fixed footer adds each column; excess stock of one commodity cannot offset a shortage of another. Fully covered commodities stay visible in green.

Carrier inventory is saved separately for each commander and survives app restarts and API refreshes. It is shared across project views: an individual view shows how far your stock would cover that project, while the combined view subtracts your stock once from the combined requirements. In Manual mode, reduce inventory after deliveries or sales; Raven Colonial's next refresh will account for delivered cargo in Required. Manual edits are local and do not modify the website. The footer's inventory total covers commodities in the selected project view. Unknown API requirements stay marked unknown.

The application makes read-only HTTPS requests to `/api/cmdr/{commander}/active` and `/api/project/{buildId}` on Raven Colonial's API service, `https://ravencolonial100-awcbdvabgze4c5cq.canadacentral-01.azurewebsites.net`, matching its web client. The commander name is sent as UTF-8 base64 in `rcc-cmdr0`; a configured key is sent using `rcc-key`. Public reads may work without a key. It uses each project's `commodities` map as remaining requirements, omits zero values, and displays negative placeholders as Unknown. Combined requirements with unknown quantities show the known quantity plus `?`. It does not subtract deliveries a second time. Live values depend on your existing Raven Colonial reporting client updating the project. Game journals are read locally for cargo tracking; no delivery uploads are included.

Settings are saved under `%LOCALAPPDATA%\ColonizationNeeds\settings.json`. The API key is encrypted with Windows DPAPI for your Windows user account; it is never saved as plain text. Use Settings to change or remove it. Network failures retain the last successful data with a stale warning.

Requires Windows with .NET Framework 4.8 (normally included on Windows 10/11). No SDK, Python, Electron, or installer needed. The executable is unsigned.

Source is included in `src\ColonizationNeeds.cs` and `src\JournalCargoTracker.cs`. Rebuild with `powershell -ExecutionPolicy Bypass -File .\build.ps1` from this folder. Run `bin\ColonizationNeeds.exe --self-test` to check parsing, inventory arithmetic, and journal tracking with synthetic events. Exit code 0 indicates success. `--test-key-protection` separately checks DPAPI on your Windows account.

Validation: executable compiled and parsing checks passed. Live account/API connectivity has not been verified. The restricted build environment cannot access the Windows user profile required for DPAPI testing; key protection must be checked during normal desktop use. The app refuses to save a key if encryption fails.

API route/schema reference: https://github.com/Fenris159/ravencolonial_edmc/blob/main/api/client.py . This is an independent application and is not affiliated with Raven Colonial or Frontier.

Version 1.1 fixes the API host (the website can return HTML instead of JSON), matches the website's account headers, and handles empty/malformed responses with readable errors. API host/header reference: https://github.com/njthomson/RavenColonialWeb/blob/main/src/api/api-util.ts .

Version 1.2 adds editable saved inventory, three quantity columns, and fixed totals. Checks cover shortages, exact coverage, surplus inventory, persistence serialization, negative quantity rejection, and the rendered table/footer totals.

Version 1.3: Double-click a commodity or use **Edit inventory**, then choose **Add**, **Remove**, or **Set total**. Enter the quantity and click **Apply**. Add and Remove change your existing count by that amount; Set total replaces the count. The dialog previews the resulting inventory before you apply it. Removing more than you hold is rejected. All changes are saved and immediately update remaining needs and totals.

Version 1.4 groups commodities under category headings such as Chemicals, Foods, Industrial Materials, Machinery, Metals, Medicines, Technology, Textiles, Weapons, and others. Categories and names are alphabetical. The bundled catalog covers Raven Colonial's construction commodities, with older symbol aliases supported; unrecognized commodities appear under Other. Category and display-name facts follow EDCD's game-data catalog: https://github.com/EDCD/FDevIDs/blob/master/commodity.csv . For example, Water and Tritium are Chemicals, while Steel is Metals. Inventory editing and totals work across all displayed groups.

Version 1.5 uses a soft slate and sage palette: a pale gray-green table, graphite text, muted teal buttons, a shaded totals bar, and darker green for covered inventory. Settings and inventory dialogs use the same colors. Category headings have readable contrast against the light table background.


Version 1.6 renames the application and executable to ColonizationNeeds and organizes source under src/, with the executable under bin/. Legacy RavenNeeds settings and inventory are copied into the new Local AppData folder on first launch. See PROJECT.md for project layout and multi-computer use.

For other computers, download or clone the private repository and run bin\ColonizationNeeds.exe. Enter your API key on each computer; Windows-encrypted credentials and manual inventory remain local. To continue coding, open the downloaded project folder as a local Codex project.

Version 1.7 adds **Cargo mode**:

- **Manual**: automatic inventory changes are off. Add, Remove, and Set total remain available.
- **Collect**: newly acquired cargo loaded onto your active ship adds the same quantity to tracked carrier stock.
- **Colonize**: newly acquired cargo loaded onto your active ship removes the same quantity from tracked carrier stock. A shortfall leaves stock at zero and displays a warning for manual correction.

Choose the mode before loading cargo. For example, loading 20 t of Steel changes tracked stock from 100 to 120 in Collect, or from 100 to 80 in Colonize. The change occurs on ship loading, including purchases, scooping, refining, mission rewards and transfers to the ship. It does not wait for delivery to a carrier, and it does not identify the source carrier. Unloading, sales and subsequent cargo snapshots do not apply the change again. Manual adjustments remain available in all modes.

The app checks new Elite Dangerous journal entries every two seconds while running, and only applies entries for your configured commander. Settings lets you select the journal folder; the default is `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous`. The selected mode and folder are saved locally in `tracking.json`. Existing ship cargo and historical entries are skipped when the app starts or tracking is reset, including a mode or account change. Loads while the app is closed are not backfilled; correct stock manually when needed. Cargo snapshots are never treated as acquisitions, and SRV acquisitions are ignored. Carrier stock is a local estimate, not a direct reading of the carrier hold or a change to Raven Colonial.

Validation includes historical-event skipping, duplicate polling, snapshots, incomplete lines, ship/SRV transfers, commander isolation, journal rollover, restart baselines, failed-save retry, and both mode calculations. Live in-game loading still needs verification during normal gameplay.

Version 1.8 tightens the table spacing and starts at 480 pixels wide (previously 620), with a 460-pixel minimum width. The commodity column follows window resizing; hover over a shortened name to see its full text. All quantities are still in tonnes, shown by Totals (t). The mode description and footer are shorter to fit the narrower window.

Version 1.9 uses a near-black background with amber text and dark bronze controls, inspired by the in-game project overlay. Covered stock stays green. Settings includes a Window opacity slider from 30% to 100%; dragging previews the main window immediately, Save remembers it across restarts, and Cancel restores the previous setting. Settings stays fully opaque for readability.

Version 1.10 preserves the exact scroll position and selection during quantity refreshes by updating rows in place. When commodities are added or removed, it keeps the same visible commodity as an anchor, or the nearest surviving row if that commodity disappears. Applies to API refreshes, automatic cargo tracking and manual inventory edits.

Version 1.11 fixes text turning black during refresh. Uncovered and unknown commodities explicitly retain amber text; fully covered commodities remain green, including when stock changes between covered and uncovered.

Version 1.12 replaces the native white opacity slider with an amber and bronze slider supporting mouse dragging, arrow keys, Home and End. The main window and dialogs request dark title bars with amber title text using Windows DWM attributes. Custom title-bar colors require Windows 11; older Windows versions retain their supported system frame colors.
