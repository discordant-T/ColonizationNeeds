# Solo player setup and use

Use ColonizationNeeds in **local inventory mode** when you are the only person tracking carrier stock. This works whether you use a personal fleet carrier or a squadron carrier. Owning or using a squadron carrier does not require shared inventory.

Local mode requires no Cloudflare account, database, owner secret or player token. Raven Colonial supplies project requirements, while ColonizationNeeds saves your tracked carrier stock on your computer.

## 1. Download and run

1. Open the public [ColonizationNeeds repository](https://github.com/discordant-T/ColonizationNeeds). No GitHub account or invitation is needed.
2. Choose **Code > Download ZIP**, extract the ZIP, and open the extracted project folder.
3. Double-click `bin\ColonizationNeeds.exe`. You can move the executable to a folder of your choice and run it there; it does not need the source or backend files beside it.

Requires Windows with .NET Framework 4.8, normally available on Windows 10/11. There is no installer or development-tool requirement. Settings and inventory are saved under `%LOCALAPPDATA%\ColonizationNeeds`, separately from the executable.

## 2. Connect your Raven Colonial account

1. Sign in to [Raven Colonial](https://www.ravencolonial.com/) and make sure your construction projects are active for your account.
2. In the application, open **Settings** and enter your commander name exactly as shown on Raven Colonial. Use the commander name rather than a numeric website user ID.
3. Enter your Raven Colonial API key if your account requires one. Public project reads may work without it; obtain any required key using Raven Colonial's account instructions.
4. Check the game journal folder. Its usual location is `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous`. Browse to your actual folder if different. The game commander must match the configured commander for automatic cargo tracking.
5. Under **Settings > Shared inventory**, leave **Use shared inventory** unchecked. Leave the server URL and player token empty on a new setup. Save settings.
6. Click **Refresh** and choose an individual project or **All active projects (combined)**.

If you previously used shared inventory, resolve any pending shared updates before disabling it. Switching back to local restores your separate local counts; it does not copy the shared balances into them.

## 3. Enter your existing carrier stock

### Optional: report deliveries without SrvSurvey

In **Settings**, enter your Raven API key and enable **Report deliveries to Raven Colonial**, then Save. First disable delivery reporting in SrvSurvey or any other reporting tool. Keep ColonizationNeeds running while delivering: confirmed contribution events report your commander credit and depot snapshots update the correct project's remaining requirements. This works in any cargo mode and requires no Cloudflare account. Carrier stock edits are not reported as deliveries. The feature does not import historical deliveries or create projects.

The Raven status line shows pending reports/errors. If it requests review after a timeout or interruption, open **Settings > Review delivery reports**, check Raven's contribution history, then mark the report recorded if present or retry only if absent. Further contribution reporting waits until you resolve uncertain reports, avoiding an automatic duplicate retry.

Start in **Manual** cargo mode. For each displayed commodity you already hold, double-click it or select it and click **Edit inventory**, choose **Set total**, enter the actual tonnes, and apply the change.

| Column | Meaning |
| --- | --- |
| Required | Outstanding quantity reported by Raven Colonial for the selected projects |
| Carrier stock | Your locally recorded stock for that commodity |
| Still needed | In Manual/Collect, Required minus carrier stock, with a minimum of zero. In Colonize, the outstanding quantity still to deliver, without subtracting stock |

Required reflects outstanding requirements, rather than the original construction cost including completed deliveries. The footer totals the displayed commodities. Inventory is shared between your project views: the combined view subtracts your stock once from combined requirements. Changing the project selection does not create another stock inventory.

Local inventory is saved for the configured commander. It is a tracked balance, not a live query of your carrier's storage. Compare it with the actual carrier occasionally and correct differences with **Set total**.

## 4. Choose manual or automatic tracking

Manual edits are available in every cargo mode:

- **Add:** increase stock by the quantity entered.
- **Remove:** decrease stock by the quantity entered.
- **Set total:** replace the recorded balance with an exact count.

Choose your cargo mode before loading cargo:

| Mode | Behavior | Example: starting with 100 t of Steel |
| --- | --- | --- |
| Manual | Only manual edits change stock | Loading cargo leaves the tracked balance at 100 t |
| Collect | New cargo loaded onto your ship adds to stock, assuming you will deposit it into the carrier | Loading 20 t records 120 t |
| Colonize | New cargo loaded onto your ship deducts from stock, assuming it came from the carrier | Loading 20 t records 80 t |

### Collecting supplies

Select **Collect** before acquiring supplies intended for the carrier. The application credits the balance when new cargo loads onto your ship. When you subsequently unload that cargo into the carrier, the application does not credit it again.

If you cancel the deposit, sell or lose that cargo instead, remove the credited quantity manually. Use Manual mode or make a correction for cargo acquired for another purpose.

### Taking supplies to a construction project

Select **Colonize** before loading supplies from the carrier onto your ship. The application deducts the loaded quantity immediately. Delivering it does not cause another inventory deduction. Raven Colonial's requirements update separately through your existing Raven reporting workflow and the application's refresh.

If the load came from a station or another source instead of the carrier, the same automatic deduction can still occur. Use Manual mode for those loads, or correct the balance. If you return an undelivered load to the carrier, add it back manually. A Colonize deduction that exceeds recorded stock leaves zero and reports a shortfall for correction.

In Colonize mode, **Still needed** stays bright cyan while deliveries are outstanding and becomes green at zero. The game's next construction-depot journal snapshot updates the matching project's remaining quantities; the combined view adds the projects together. It does not subtract carrier stock or deduct the delivery twice when Raven catches up. Keep the app running during delivery. Projects without a matching depot snapshot use Raven's reported outstanding quantities.

### Tracking limits

Keep the application running while loading cargo. It reads new game journal events; existing ship cargo at startup and historical events are not imported. Mode changes establish a fresh tracking baseline. Unloads and repeated cargo snapshots do not change the balance again. Run one tracker for your game session.

The application cannot verify that cargo reached the carrier or identify which carrier supplied a load. These rules apply equally to personal and squadron carriers. If other people change a squadron carrier's stock while you use local mode, record their changes manually or reconcile against the actual storage.

## 5. Everyday use

1. Open ColonizationNeeds before loading cargo.
2. Check the selected projects and recorded stock.
3. Choose Manual, Collect or Colonize for your next activity.
4. Correct any load or transfer that does not follow that mode's assumptions.
5. Periodically compare the recorded balances with the carrier and use Set total to reconcile.

**Refresh every minute** updates Raven project requirements; it does not read carrier inventory. You can also click **Refresh** yourself. Closing and reopening the app keeps your local inventory and remembered window location. Use **Always on top** and the opacity setting to suit your screen; borderless/windowed game mode helps keep the window visible.

## 6. Another computer or friends joining

You can download the executable on another computer, but local stock does not automatically synchronize between computers. Enter your Raven settings there separately; encrypted credentials are tied to the Windows user who saved them. Establish the correct inventory on the computer you will use before continuing tracking.

If friends later need to update the same ledger, follow the [squadron leader installation guide](INSTALLATION.md) to set up shared inventory. An admin can use **Sync local to shared** to initialize it from the saved local counts. Review the preview: this replaces shared totals, including setting shared-only commodities to zero, rather than adding the two inventories. Give each friend their own member token and the [member setup guide](MEMBER-SETUP.md).

You can continue using local mode indefinitely on a squadron carrier if you do not need other players' applications to synchronize stock.
