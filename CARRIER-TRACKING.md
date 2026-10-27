# Carrier accounting — 1.28.1

In **version 1.29**, use **Projects...** beside the project dropdown to choose which projects feed the combined rollup. Choices are separate per commander/PC and leave shared stock, Inbound/Outbound and Raven status unchanged. Individual projects remain viewable. See [Project selection](PROJECT-SELECTION.md). No backend update is required for this feature.

Compact/Full display is available through View; see [Compact view](COMPACT-VIEW.md). Need/In/Out/Collect are shorter labels for the same quantities. Hiding a cargo column does not remove it from the collection calculation.

## Columns and collection need

| Column | Meaning |
|---|---|
| To deliver | Outstanding construction requirement for the selected project(s). |
| Stock | Recorded stock aboard the selected carrier(s). |
| Inbound | Opted-in station purchases still aboard players' ships, intended for carriers. |
| Outbound | Confirmed carrier withdrawals still aboard players' ships. Previously called In transit. |
| To collect | `max(0, To deliver - Stock - Inbound - Outbound)` for each commodity. |

Required and Still needed were replaced with To deliver. Original verified requirements remain cached internally for recovery; they are not an extra table column. Unknown data remains Unknown rather than becoming zero. Positive delivery need is cyan, collection shortages red and satisfied rows green.

## Example: station → carrier → construction

| Stage | To deliver | Stock | Inbound | Outbound | To collect |
|---|---:|---:|---:|---:|---:|
| Need 100 steel | 100 | 0 | 0 | 0 | 100 |
| Buy 100 at a station with collection enabled | 100 | 0 | 100 | 0 | 0 |
| Deposit 40 | 100 | 40 | 60 | 0 | 0 |
| Deposit remaining 60 | 100 | 100 | 0 | 0 | 0 |
| Withdraw 100 | 100 | 0 | 0 | 100 | 0 |
| Deliver 40 | 60 | 0 | 0 | 60 | 0 |
| Deliver remaining 60 | 0 | 0 | 0 | 0 | 0 |

## Collection intent and destination

Use Automatic tracking. Enable **Collecting for carriers** before buying supplies at an ordinary station and choose a carrier or **Either carrier**. New purchases are recorded; cargo already aboard is not reconstructed or uploaded from historical journals. Mining, rewards and personal cargo acquired while off are not automatically reserved. A known station docking context is required; unknown contexts do not become Inbound.

The switch and destination are saved per player profile. Changes first process unread journal lines using the previous intent. Switching off stops future purchase tracking; it does not remove existing loads. Changing destination affects future purchases only. Check the switch before buying personal cargo, especially after restarting the app. Manual mode suspends new Inbound purchase capture even if the remembered switch is on. Backlogged purchases are attributed using the saved intent when the app next processes them; keep that profile's journal and intent settings together.

All carriers includes every configured carrier, retained Legacy stock and all tracked Inbound/Outbound. An individual carrier view includes its stock, Inbound explicitly assigned to it and Outbound withdrawn from it. **Either-carrier Inbound counts only in All carriers**, so it is never allocated to both individual carriers. Project selection changes outstanding requirements; cargo is not reserved to a project until actual delivery identifies the destination. Unneeded excess cargo can remain visible, but To collect never falls below zero.

## Buying for trade and delivering with the switch off

**Collecting for carriers controls new station purchases becoming Inbound. It does not turn carrier accounting or delivery tracking on and off.** The rules below assume Automatic tracking, the correct player profile and confirmed journal events.

| Action | Switch off | Switch on |
|---|---|---|
| Buy commodities at an ordinary station | No new Inbound; use this for personal cargo or trading. | New purchase becomes Inbound for the chosen destination. |
| Sell or directly transfer goods to a configured carrier | Stock increases. | Stock increases. |
| Buy or withdraw goods from a configured carrier | Stock decreases and Outbound increases. | Stock decreases and Outbound increases. |
| Deliver tracked goods to construction | Tracked ship cargo clears; construction observations/Raven update To deliver. | Same behavior. |

**You do not need to disable the switch when delivering.** Turn it off before buying goods at stations for other reasons. Turning it on later affects future purchases only; it does not retroactively label cargo already aboard your ship as Inbound. Turning it off does not erase or freeze existing tracked cargo: deposits, deliveries, sales and losses still account for it.

For example, buy 100 steel at a station with the switch off while trading. Inbound and carrier Stock stay unchanged. If you later decide to use that steel for construction, sell or directly transfer it to a configured carrier: Stock increases by 100 even if the switch is still off. No Inbound entry is required first. You can then withdraw it and deliver normally.

All observed deposits to a configured carrier count as Stock, including goods you regard as personal or trading stock. The switch cannot exclude personal goods stored aboard those carriers or reserve only part of a carrier's stock for construction. Keep that distinction in mind before using the displayed supply totals.

## Movements, mixed loads and removal

| Action | Effect |
|---|---|
| Opted-in MarketBuy at an ordinary station | Add Inbound for that player/destination; no carrier stock change. |
| MarketSell or confirmed owner transfer to a registered carrier | Add Stock, consume that player's tracked ship cargo. |
| MarketBuy or confirmed owner withdrawal from a registered carrier | Reduce Stock, add Outbound; never also add Inbound. |
| ColonisationContribution | Consume the delivering player's cargo; no second stock debit. Construction observations/Raven update To deliver separately. |
| Ordinary-station sale or EjectCargo | Consume tracked cargo, without crediting construction. |
| Ship loss or empty ship Cargo snapshot | Clear that player's tracked ship cargo. |
| Embedded Ship Cargo inventory snapshot | Cap combined Inbound/Outbound to physical quantities; never credit unrelated personal cargo. |
| Manual Stock Add/Remove/Set total or Move from legacy | Change Stock only; never create Inbound/Outbound. |

Commodities are fungible, so mixed personal/tracked batches cannot be physically identified. For a deposit the app consumes Inbound for the actual destination (including Either) first, then Outbound from that carrier, then other tracked lots. For a delivery or other consumption it prefers Outbound, then Inbound; lots at the same priority are consumed oldest first. Direct station-to-construction delivery can therefore clear Inbound without touching carrier stock. Partial loads clear only their amount. Depositing at a different configured carrier clears the ship cargo and credits the actual receiving carrier.

Shared mode separates lots by player identity and shares group totals. One player's delivery cannot clear another's cargo. Events are ordered by observed journal time, with stable receipt order for timestamp ties. Stable IDs prevent duplicates across retries/restarts. Stock movements and their Outbound/return effects commit atomically through the stock-ledger trigger; Inbound and consumption use the durable outbox. Everyone hauling should use 1.28. Older clients cannot report new Inbound intent and display the earlier columns.

## Carrier discovery and first setup

Open **Carriers / review**, select an observed carrier and choose **Use as Squadron** or **Use as Personal**. Discovery is read-only, filtered by commander, and shows journal name/callsign, numeric MarketID and last-seen evidence. Dock and **Rescan journals** if missing. A shared admin clicks **Save shared carriers**; members adopt the registry automatically. Manual MarketID entry is available with confirmation when not observed. Role/ownership is not inferred from discovery. Keep carrier IDs associated with recorded stock/history; they cannot simply be replaced.

Existing local inventory is imported once into Legacy stock; existing shared inventory stays in its original table. Use **Edit inventory → Move from legacy** to assign or split it without changing the combined total. Shared migration requires an admin and no pending changes. Do this once, not on each upgrade. The original local files are retained.

## Corrections and recovery

Edit inventory supports Add/Remove on each carrier and admin-only Set total in shared mode. Set total uses the current version and a UTC cutoff so older delayed movements cannot reapply to corrected stock. Signed automatic underflow is retained as a discrepancy; the table displays unavailable stock as zero and Carriers / review shows the signed balance.

**Clear my Inbound** and **Clear my Outbound** clear only your selected commodity in that flow across all carriers. They leave Stock and other players' cargo unchanged. Use them for unavailable cargo whose removal was missed. Corrections do not add arbitrary ship cargo. Nonempty Cargo events without an embedded Inventory list cannot reconcile every commodity; the app does not apply a newer Cargo.json to an older journal event.

Manual tracking and ambiguous/unregistered carrier movements use Carriers / review. Apply only after confirming the actual carrier, or dismiss. Unknown carrier purchases are not treated as ordinary-station Inbound. Relevant carrier movement review uses the original timestamp and ID. Stock edits never count as construction deliveries.

Shared changes synchronize automatically. If blocked by an edit conflict, open **Settings → Shared inventory → Cancel conflicted edit**, close settings and Refresh; the rejected pending correction is cancelled, not committed server stock. Investigate other connection/configuration errors in the status line. Do not switch credentials/groups with pending changes.

Journal cursors, outboxes and profile locks preserve recovery. Truncated/malformed journals pause tracking instead of silently skipping. Keep original journal filenames when moving PCs; stable IDs use commander, filename and byte position. Do not run two instances for the same local profile. Do not copy one commander's credentials or cargo state to another.

Requirements and cargo synchronization are independent. After delivery the next depot observation/Raven refresh may briefly lag transit consumption. Refresh or open Construction Services to obtain the new requirements; do not manufacture a stock adjustment. Unobserved outsiders' trades, missing journals or unreported losses need reconciliation. This is a journal-derived ledger, not a live query of all carrier storage. No historical stock/trade/delivery replay occurs on first launch.
