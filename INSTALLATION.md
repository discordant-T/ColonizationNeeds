# Squadron leader installation and administration

This guide sets up ColonizationNeeds with your own Raven Colonial account and a shared carrier inventory hosted by Cloudflare Workers and D1. Members use the Windows application and a player token you give them. Only the leader needs to administer Cloudflare or run the player creation script.

If you play alone, including on a squadron carrier, use the [solo player guide](SOLO-SETUP.md). Local inventory works without Cloudflare or shared storage.

If your backend already works, skip to **Create player tokens** and **Add members**. You do not need a new database for each member.

## 1. Download the application

1. Open the public [ColonizationNeeds repository](https://github.com/discordant-T/ColonizationNeeds). No GitHub account or invitation is needed to download it.
2. Choose **Code > Download ZIP** and extract the ZIP. Keep the whole extracted project folder for the leader's setup scripts. For example, put it at `C:\Games\ColonizationNeeds`. Your extracted folder may have a name such as `ColonizationNeeds-main`; use its actual path in commands.
3. Open `bin` and double-click `ColonizationNeeds.exe`.

Windows with .NET Framework 4.8 is required, normally available on Windows 10/11. No development tools or installer are required. The executable can run by itself; the leader's token creation script needs the `backend` folder. Settings and inventory are saved under `%LOCALAPPDATA%\ColonizationNeeds`, separately from the executable.

## 2. Connect Raven Colonial

1. Sign in to [Raven Colonial](https://www.ravencolonial.com/) and make sure your intended construction projects are active for your account.
2. In ColonizationNeeds, open **Settings**.
3. Enter your commander name exactly as shown on Raven Colonial. This is the account identifier used by the application, rather than a numeric website user ID.
4. If your account requires a Raven Colonial API key, enter the key supplied by Raven Colonial. Public project reads may work without one. Use Raven Colonial's own account instructions to obtain a key; a Cloudflare token will not work here.
5. Check the Elite Dangerous journal folder. The usual folder is `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous`. Browse to the correct folder if your installation uses another location.
6. Save settings, click **Refresh**, and choose a project or **All active projects (combined)**. Confirm the expected commodities appear.

The configured commander must also match the commander in your game journal for automatic cargo tracking. Each player configures their own Raven account. Shared inventory shares carrier stock; it does not automatically share the leader's Raven project list or Raven credentials.

To replace SrvSurvey's delivery reporting, enter your own Raven API key and enable **Report deliveries to Raven Colonial** in Settings. Disable delivery reporting in other tools first. This reports confirmed contributions and depot requirements while the application is running, in any cargo mode; it does not publish carrier balances or create projects. Each member enables reporting with their own Raven key. The Raven status line shows pending reports. If an outcome is uncertain, check contribution history and resolve it under **Settings > Delivery reports** before retrying.

You can use local inventory without Cloudflare. To record existing stock, use **Edit inventory > Set total** for each displayed commodity. These saved local counts can later initialize shared stock.

## 3. Understand the credentials

| Credential | Purpose | Who uses it? |
| --- | --- | --- |
| Raven Colonial API key, if needed | Read that player's Raven project requirements | That player, in the main Settings dialog |
| `OWNER_TOKEN` | Authorize creation of player accounts on your Worker | Leader only, at the script's hidden prompt |
| Player access token | Read and change one group's shared inventory | Its assigned player, in Shared inventory settings |

Your Cloudflare login and Cloudflare account API tokens are not entered into ColonizationNeeds. An admin player token is still different from `OWNER_TOKEN`: it can reconcile stock, but cannot create players. Keep the owner secret private and give each member a separate player token. Do not put any token in GitHub or this guide.

## 4. Create the Cloudflare Worker and D1 database

Use your own Cloudflare account for your squadron. A custom domain is unnecessary; the Worker gets a `workers.dev` address. Select the free Workers/D1 options to test; check current [Workers pricing](https://developers.cloudflare.com/workers/platform/pricing/) and [D1 pricing](https://developers.cloudflare.com/d1/platform/pricing/) before enabling a paid plan.

1. Sign in to the [Cloudflare dashboard](https://dash.cloudflare.com/).
2. Open **Workers & Pages**, create an application, choose the Hello World Worker option, name it (for example, `colonizationneeds-api`), and deploy it.
3. Record the actual Worker URL shown in your dashboard, such as `https://YOUR-WORKER.YOUR-SUBDOMAIN.workers.dev`. Use your own URL throughout this guide.
4. Under **Storage & databases > D1**, create a database, for example `colonizationneeds-db`.
5. Open the database's **Console**. Open the project's `backend\schema.sql` in a text editor, copy its complete contents into the console, and execute it. Resolve any SQL error before continuing. This creates the tables and ledger trigger without deleting existing data.
6. Open your Worker > **Bindings > Add binding**, select **D1 database**, use the exact variable name **DB**, and select your database. Save the binding.
7. Open the Worker's **Edit code** screen. Replace the starter JavaScript with the complete contents of `backend\worker.js`, then **Deploy**.

Cloudflare's [official D1 setup guide](https://developers.cloudflare.com/d1/get-started/) explains database creation and Worker bindings. Dashboard labels can change; the essential binding is a D1 database named `DB` on the Worker running the supplied code.

## 5. Set the owner secret and check the backend

1. Generate a long random secret with a password manager: use at least 32 random bytes, for example 64 random hexadecimal characters. Save it securely so you can enter the same value when creating players.
2. Open your Worker > **Settings > Variables and Secrets** and add **OWNER_TOKEN**, with type **Secret**, containing that exact value. The name is case-sensitive. Save/deploy the change when prompted.
3. Open `https://YOUR-WORKER.YOUR-SUBDOMAIN.workers.dev/health` in a browser. Expected response:

```json
{"ok":true,"service":"ColonizationNeeds","schema":1}
```

Opening the root URL may return `Endpoint not found`; use `/health` for the check. A successful health check confirms the schema is reachable; it does not test your owner secret or a player token.

See Cloudflare's [secret configuration instructions](https://developers.cloudflare.com/workers/configuration/secrets/). This secret is a value you create for this application, not your Cloudflare password or a Cloudflare API token.

## 6. Create player tokens

Do this on the leader's computer. Open **Windows PowerShell** and go to the extracted project folder. The path below is an example, not a required installation location:

```powershell
Set-Location "C:\Games\ColonizationNeeds"
```

Create your own admin player. Replace the URL and commander name; choose a group ID that you will also use for every member:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\backend\Create-Player.ps1" -ServerUrl "https://YOUR-WORKER.YOUR-SUBDOMAIN.workers.dev" -PlayerName "YOUR COMMANDER" -GroupId "squad-carrier" -Role admin
```

At **OWNER_TOKEN from Cloudflare (hidden input)**, paste the exact secret from step 5 and press Enter. The script creates a player and copies the newly generated **player token** to your clipboard. Save that token securely, then paste it into the application as described below. It is returned only at creation; the database retains a hash and cannot recover the original token.

Always supply `-ServerUrl` for your own deployment. The script's built-in default points to the original developer's backend. Use the root HTTPS URL, without `/health`, `/inventory`, or other paths. Group IDs accept letters, numbers, underscores and hyphens, with no spaces. Creating another player with the same name creates another account/token, rather than resetting the existing one.

## 7. Connect the leader's application

1. Open **Settings > Shared inventory**.
2. Enter your Worker root URL and your newly created **admin player token**.
3. Use **Test connection** and confirm the expected group and admin role.
4. Enable **Use shared inventory** and save.

The token determines the group and role; there is no separate group ID to enter in the application. Shared inventory polls approximately every five seconds. **Refresh every minute** separately refreshes Raven project requirements.

Choose how to initialize stock:

- **Starting empty:** leave the new group's stock at zero, then use admin **Set total** edits for any known existing stock.
- **Migrating saved local stock:** use **Sync local to shared**, review the preview carefully, and confirm **Replace totals**. This requires an admin player token. It replaces shared totals with the configured commander's saved local counts, including setting shared-only commodities to zero. It does not add the two inventories together. The saved local inventory remains available separately.

Initialize before members start making changes. Replacement is applied per commodity, so an interrupted or conflicting import can be partly applied. Resolve the reported issue and run the preview again to review remaining differences. Switching between local and shared does not itself transfer inventory.

## 8. Add members

For each member, run this on the leader's computer, using the same Worker URL and group ID as your admin account:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\backend\Create-Player.ps1" -ServerUrl "https://YOUR-WORKER.YOUR-SUBDOMAIN.workers.dev" -PlayerName "MEMBER COMMANDER" -GroupId "squad-carrier" -Role member
```

Enter your owner secret at the hidden prompt. Privately send that member:

1. The application download or a copy of `bin\ColonizationNeeds.exe`.
2. Your Worker root URL.
3. Their individual player token.
4. [MEMBER-SETUP.md](MEMBER-SETUP.md).

Members can download from the public repository without a GitHub account or invitation, or you can distribute the executable directly. Members do not need PowerShell, the backend scripts, a Cloudflare account, or your owner secret. A member token allows Add/Remove and automatic cargo updates; absolute **Set total** reconciliation requires admin access. Give admin access only to players who should reconcile the group's balances.

## 9. Verify shared updates and choose cargo modes

With two applications connected to the same group, choose **Manual** mode initially. Add 1 tonne to a commodity visible on both screens. Confirm the other screen's carrier stock updates in approximately five seconds, then remove the test tonne. This tests authenticated updates; a browser health check alone does not.

Choose a cargo mode before loading your ship:

| Mode | When new cargo is loaded onto the ship |
| --- | --- |
| Manual | No automatic stock changes; edit inventory yourself |
| Collect | Adds the new cargo quantity to tracked carrier stock |
| Colonize | Deducts the new cargo quantity from tracked carrier stock |

These modes estimate carrier stock at ship loading. Collect assumes that collected cargo will be deposited into the carrier; Colonize assumes the loaded cargo came from the carrier. They do not verify a completed deposit or identify the source carrier. Unloads and repeated cargo snapshots do not apply another stock change. The existing cargo at startup is not imported, and historical journal events are not replayed.

Agree that players transfer only between their ship and the carrier, and manually correct purchases, losses, cancelled deposits, or other loads that do not follow those assumptions. Keep the application running while loading cargo. Run one tracker for each game session to avoid multiple instances recording the same loads. There is no automatic authoritative squadron carrier inventory reconciliation; periodically compare against the actual carrier and have an admin correct totals. **View ledger** helps identify changes and who made them.

## 10. Remove or replace member access

In the D1 Console, list accounts without their credential hashes:

```sql
SELECT id, name, group_id, role, active FROM players ORDER BY name;
```

Identify the exact account, then revoke it:

```sql
UPDATE players SET active = 0 WHERE id = 'EXACT_PLAYER_ID';
```

If a token is lost, create a new player token and deactivate the old account. Changing `OWNER_TOKEN` does not revoke existing player tokens. Avoid editing stock directly in SQL; use the application's admin edits so corrections appear in the ledger.

## Troubleshooting and updates

| Symptom | What to check |
| --- | --- |
| `Owner authorization required` when creating a player | Correct Worker URL, exact deployed `OWNER_TOKEN` secret name and matching value. Paste the secret value, without added quotes. Do not use a player token or Cloudflare API token. |
| Script file not found | Use the actual extracted project folder containing `backend\Create-Player.ps1`. Members do not run this script. |
| Health check fails | D1 binding is named `DB`, database schema executed successfully, and supplied Worker code is deployed. |
| Player connection rejected | Use that player's generated access token, check the root URL, and confirm the account is active. |
| Different stock on different computers | Confirm Test connection reports the same group, both apps use shared inventory, and neither has a connection error or pending update. Project lists may differ by Raven account. |
| Raven projects missing | Check the exact Raven commander name, its active projects and any required Raven API key. Cloudflare cannot supply Raven project access. |
| Update remains pending/conflicted | Read the application's error. A removal may exceed stock, or an admin replacement may conflict with a newer change. Resolve or cancel the conflicted manual edit before making a corrected one. |
| Unable to switch connection/mode | Pending shared changes must be resolved before switching the shared account or disabling shared inventory. |

Queued shared transactions are saved locally and retried when the service becomes available. Let them finish before moving to another computer; those pending files do not synchronize automatically. Credentials are protected for the current Windows user. Enter tokens separately on another computer instead of copying encrypted settings files.

For an application update, close it, download the newer executable, and replace your copy. Saved settings are outside the project folder. Update Worker code or schema only when the release instructions require it; adding members does not require redeployment. Do not delete the D1 database to reinstall the application.

The health endpoint has been checked on the original deployment. This guide does not claim that your new deployment or its authenticated player operations have been tested: perform the two-player check above before relying on the shared ledger.
