# Solo setup — 1.28.6

Cloudflare is optional for a single PC. Run the supplied executable, configure your exact Raven commander/key and journal folder in Settings, and leave Use shared inventory disabled. Local carrier stock and ship cargo are saved per profile. Multiple PCs or collaborating commanders should use the shared backend; local files do not synchronize themselves.

Discover and configure your personal and/or squadron carrier in Carriers / review. Use Move from legacy once to split old inventory between carriers without duplicating it. Use Edit inventory for actual stock corrections. Keep old stock/settings files when upgrading; existing encrypted keys and planned-construction/reporting queues are retained.

Select Automatic. Enable Collecting for carriers before intended station purchases and choose a destination. Personal purchases while off are ignored. Deposits convert Inbound to Stock; carrier purchases/owner withdrawals convert Stock to Outbound. Deliveries clear tracked ship cargo without a second stock debit. Returns, sales elsewhere, jettison, losses and available ship snapshots remove or reconcile tracked loads. Use Clear my Inbound/Outbound for missed removals. Full rules, columns and examples are in CARRIER-TRACKING.md.

Manual mode holds carrier movements for explicit review and suspends new Inbound purchases. It does not disable depot requirement observations, planned construction or optional Raven reporting. Use All active projects or an individual project independently of the carrier selector. For another commander use Add player with its own credentials; see PLAYER-PROFILES.md. For starting/delivering/completing construction see RAVEN-WORKFLOW.md.

**What the collection switch controls:** it marks new ordinary-station purchases as Inbound while Automatic tracking is active. Leave it off for personal purchases/trading; you do not need to turn it off to deliver. Deposits to a configured carrier always increase Stock, even with the switch off, and carrier withdrawals/deliveries still track normally. Turning it on later does not retroactively mark cargo already aboard. Turning it off does not erase existing loads. Any personal/trading goods deposited on a configured carrier also count as Stock.

For a smaller overlay choose **View → Compact view**. Use Controls to reveal player/tracking/destination settings and click the bottom status strip for details. Columns/totals can be hidden through View; accounting is unchanged. These preferences are saved independently per player. See [Compact view](COMPACT-VIEW.md).
