# ColonizationNeeds project

This is a standalone native Windows Forms application for Elite Dangerous colonization supplies. The canonical sources are src/ColonizationNeeds.cs src/JournalCargoTracker.cs, and src/SharedInventoryClient.cs. Build with powershell -NoProfile -ExecutionPolicy Bypass -File ./build.ps1. The script builds bin/ColonizationNeeds.exe and runs the self-tests. No external packages or SDK installation are needed for the script. The csproj supports IDE editing with .NET Framework 4.8 development tools installed.

Preserve read-only Raven Colonial API behavior, saved commander-specific inventory, Windows DPAPI key encryption, inventory Add/Remove/Set total operations, grouped commodity categories, and the dark amber palette and saved opacity. Never put credentials or runtime inventory into this repository. The executable is deliberately included because the user wants source and a ready-to-run app available across computers.

Preserve Manual, Collect and Colonize modes. Collect credits newly loaded ship cargo; Colonize debits it from local carrier stock. Do not replay existing cargo, historical journals, snapshots, or unloading events. Ignore other commanders and SRV acquisitions. Manual editing remains available in every mode. Commit inventory before advancing the journal cursor, and keep rendering outside that transaction. Automatic tracking only covers events while the app is running; startup and mode changes establish a fresh baseline.

Check the README for behavior, API references, and multi-computer limitations. For API changes, verify the current RavenColonialWeb API host and headers instead of assuming the public website hosts its API. Keep changes in this project folder; older RavenNeeds files outside it are legacy snapshots.


Shared mode uses backend/worker.js and backend/schema.sql on Cloudflare. Preserve durable outbox IDs, server-side ledger/balance transactions, admin-only version-checked Set total, and separation from local inventory. Do not embed owner or Cloudflare credentials in the app. Player tokens are saved with DPAPI. Test lost acknowledgments and retry behavior; live shared access requires player credentials.

