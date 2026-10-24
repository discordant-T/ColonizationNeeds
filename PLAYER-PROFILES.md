# Multiple commanders and PCs — 1.28

Version 1.28.5 keeps the window responsive during journal catch-up when changing players. A switching message appears while outgoing events finish saving; account controls are temporarily unavailable until the incoming profile is loaded. Existing account data and credentials do not need resetting. If an older version is stuck, close it with Task Manager and reopen with the updated executable; keep the saved profile folders intact.

Use Add player, enter the exact commander name and configure that player's own Raven key and journal folder. Names are unique ignoring case. New profiles do not inherit another player's keys, shared token or collection intent. They start with local inventory, Manual tracking, Raven reporting off and Collecting for carriers off. Cancelling setup leaves the profile available to finish later.

Choose the active commander with Player. Wait for current operations to finish; outgoing journal activity is processed before switching. Each profile keeps its own Raven settings, shared URL/token, local stock, Inbound/Outbound events, collection switch/destination, journal cursor, review/outbox/reporting queues and display preferences. The same Windows journal directory can contain multiple commanders; processing is filtered by commander. Only verified original project requirements for the exact Raven build ID may be reused; remaining observations and cargo are not copied between accounts.

Profiles in the same shared group use separate player tokens. Carrier configuration and stock/cargo totals are shared, while cargo lots remain owned by the reporting player. Their Raven project lists and API access remain separate. Other profiles may show combined shared supply without having access to every member's Raven projects.

On another PC, install 1.28, configure the same commander and their existing shared player token, then verify the service/group. Windows DPAPI-encrypted settings files cannot simply be decrypted under a different Windows identity. Let pending work finish before moving PCs. Keep original journal filenames when copying logs so event identities remain stable. A fresh installation establishes a baseline; it does not replay old stock or assume existing ship cargo. Never copy another commander's credentials or cargo state to make a profile appear initialized.

Original Required totals are now internal recovery facts; the visible To deliver column is remaining construction need. If it is Unknown, refresh Raven with the correct account or visit the matching depot and open Construction Services for a new observation. This does not require enabling reporting. See CARRIER-TRACKING.md for Unknown stock/cargo and synchronization issues.

Saved data remains under %LOCALAPPDATA%\ColonizationNeeds and commander profile folders, independently of the executable. Close all instances and back it up before manual recovery. Existing legacy files are retained during migration; do not delete/reset them to resolve a display issue.

For a smaller overlay choose **View → Compact view**. Use Controls to reveal player/tracking/destination settings and click the bottom status strip for details. Columns/totals can be hidden through View; accounting is unchanged. These preferences are saved independently per player. See [Compact view](COMPACT-VIEW.md).
