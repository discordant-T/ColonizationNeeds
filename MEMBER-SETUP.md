# ColonizationNeeds member setup

Your squadron leader supplies the application, the shared server URL, and your individual player access token. You do not need Cloudflare, PowerShell, or the leader's owner token.

## Install

1. Download the application from the leader or open the [public GitHub repository](https://github.com/discordant-T/ColonizationNeeds), choose **Code > Download ZIP**, and extract it. No GitHub account or invitation is needed.
2. Run `bin\ColonizationNeeds.exe` from the extracted project. If the leader sent only the executable, run that file directly from your chosen folder.
3. Windows with .NET Framework 4.8 is required, normally available on Windows 10/11.

## Configure your projects and shared stock

1. Open **Settings**. Enter your own commander name exactly as shown on Raven Colonial and your Raven API key if required. Make sure the intended projects are active/available for your Raven account. Shared stock does not automatically share the leader's project list.
2. Check the journal folder, normally `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous`. Your game commander must match the configured commander for cargo tracking.
3. Open **Shared inventory**. Enter the leader's root server URL and **your player token**. Do not append `/health` to the URL.
4. Click **Test connection** and confirm the group matches the leader's group. Enable **Use shared inventory**, then save settings.
5. Click **Refresh** to load your Raven projects. Shared carrier stock updates approximately every five seconds; the optional minute refresh updates Raven requirements separately.

Keep your token private. Your own Raven key, your player token, and the leader's owner token are different credentials. Enter your credentials again on another computer; encrypted settings files are tied to the Windows user who saved them.

## Use the inventory

- **Manual:** only your manual edits change stock.
- **Collect:** new cargo loaded onto your ship adds to tracked stock, assuming you will deposit it into the carrier.
- **Colonize:** new cargo loaded onto your ship deducts from stock, assuming it came from the carrier.

Choose the mode before loading cargo and keep the application running. Existing cargo at startup is not counted. These modes do not verify an actual carrier deposit, so correct exceptions manually and follow the squadron's agreed rules. Run one tracker for your game session.

Double-click a commodity or click **Edit inventory** to **Add** or **Remove** tonnes. Admins can also **Set total**. Ask an admin to reconcile the actual carrier stock when needed. Do not use **Sync local to shared** to combine your personal stock: that admin action replaces the group's totals.

Required comes from your selected Raven projects. Carrier stock comes from the shared group. In Manual/Collect, Still needed is the remaining shortage after that stock is subtracted. In Colonize, it shows what still needs delivering, yellow while outstanding and green at zero. The next construction-depot journal snapshot updates your matching project, while Raven refreshes provide updates from other players. Deliveries do not deduct carrier stock a second time. Local mode is a separate inventory and does not update the group.

If a shared update remains pending, read the status/error and let it finish before switching accounts or computers. If connection fails, check your server URL and token with the leader. You do not need to set up another backend.
