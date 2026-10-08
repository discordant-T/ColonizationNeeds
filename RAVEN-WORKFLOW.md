# Raven construction workflow

Applies to ColonizationNeeds 1.25 stable. Raven project access is separate from Cloudflare stock access. Construction setup and reporting use your Raven API key; a shared player token or owner token cannot substitute for it.

## Start a planned construction

1. Prepare the site in Raven's system plan and start the corresponding construction in Elite Dangerous.
2. Save your commander name, Raven API key and correct game journal folder in ColonizationNeeds Settings.
3. Dock at the in-game construction and open **Construction Services**.
4. Open **Settings > Start planned construction**. Choose the matching planned site by its planned name, body and construction type. Raven's generated planned name may differ from the game's construction name.
5. If its type ends in `?`, resolve the exact variant in Raven and save the plan first. Do not guess the variant solely from an uncertain label.
6. Click **Start / verify**. The app uses the journal's actual system and MarketID, original depot requirements and selected plan. It creates or reuses a project, links your commander, renames/links the plan to the construction and sets its status to Building.
7. Wait for **Verified**, close Settings and **Refresh** the main window. Check the project and Raven system plan.

Any cargo mode works. **Report deliveries to Raven Colonial** is not required for this explicit setup operation. Automatic construction detection may show a Settings hint, but it never creates a project without selecting the plan and clicking Start / verify.

The live test on Gernhardt Beacon verified a Military Outpost (nemesis), commander linkage, and Building status on C 3. Exact system, body, project and type checks prevent silently linking a conflicting existing project. Automatic fleet carrier linking is not part of setup; link carriers or other commanders in Raven as needed.

## Deliver and track progress

1. Enable **Settings > Report deliveries to Raven Colonial** with your own Raven key and save.
2. Disable overlapping delivery/colonization reporting in other tools, including SrvSurvey and BGS-Tally, to prevent duplicate credits or competing setup.
3. Keep ColonizationNeeds running during delivery. Reporting works in Manual, Collect and Colonize, with local or shared stock.
4. Confirmed contribution events submit commander credit. Depot snapshots update absolute outstanding requirements.
5. In Colonize, Required stays at the original cost and Still needed decreases with deliveries. Known zero is green. Carrier stock is debited on loading, not again at delivery.

Missing original Required totals are recovered read-only from game journals for the same commander and MarketID. They are saved locally. This recovery does not upload old deliveries or change inventory. If no original depot observation is available, open Construction Services at that site to obtain one; do not replace Required with the partially delivered balance.

## Complete a construction

Leave reporting enabled and the app running for the final delivery and game completion event. A new `ConstructionComplete: true` depot event queues an explicit completion call. The app finishes final contribution credits first, checks Raven before sending, and verifies completion afterward.

**Settings > Delivery reports** shows Construction completion and its status. The project should leave the active list on a Raven refresh. The user confirmed this reporting workflow across several completed constructions.

Zero remaining quantities alone are insufficient: the game must confirm completion. If the app was closed or reporting disabled at completion, the old event is not automatically replayed. Check Raven and use its normal project controls or an appropriate reporter for that already-completed site.

## Interrupted or uncertain operations

| Situation | Recovery |
| --- | --- |
| Setup timed out or says Not verified | Inspect Raven before retrying. Reopen setup at the same construction; it looks up and reuses the existing project rather than blindly creating another. No automatic setup retry occurs. |
| Existing project has conflicting body/type or another plan link | Stop and inspect the actual construction and Raven plan/project. Resolve the mismatch in Raven; do not force a duplicate project. |
| Plan variant ends in `?` | Save the exact variant in the system plan, then reopen setup. |
| Construction not detected | Verify the commander/journal folder, remain docked, and open Construction Services. |
| Contribution Needs review | Check Raven's contribution history. Mark recorded if present; retry only if absent. Further credits wait for review. |
| Completion remains pending | Resolve uncertain/pending final delivery credits and check network/key access. Saved completion resumes with a Raven status check. |
| Required shows Unknown | The original depot totals are absent from Raven, saved data and available journals. Visit that construction and open Construction Services. |
| Project list differs between members | Each has their own Raven commander account. Shared stock does not grant project membership. |

Automatic retry of a lost completion acknowledgment first checks whether Raven already reports complete. It does not blindly resend contribution credits. Saved queues and encrypted credentials belong to the current Windows account; let pending work finish before moving to another computer.

See [solo setup](SOLO-SETUP.md), [leader setup](INSTALLATION.md) and [member setup](MEMBER-SETUP.md) for inventory configuration.
