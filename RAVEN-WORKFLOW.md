# Raven construction workflow — 1.28

Configure the exact commander and that commander's Raven API key in Settings. Select All active projects or an individual project. Shared carrier access does not replace Raven authentication or project membership.

## Start planned construction

Dock at the intended construction site and open Settings → Start planned construction. Select the intended available Raven plan. The app validates current journal system, numeric MarketID, body and construction type; it creates or reuses the matching project, links the commander and verifies Building status. Correct unknown variants ending in ? in Raven first. Repeated setup must reuse the existing project rather than duplicate it. This feature works independently of Manual/Automatic and the reporting checkbox. It does not add deliveries or inventory.

## Deliver and report

You may leave Collecting for carriers on or off while delivering. It only controls new station purchases becoming Inbound; it does not pause carrier withdrawals, delivery accounting or Raven reporting. Turning it off does not discard tracked loads.

Keep the correct Player profile active. Actual contribution/depot events identify the construction destination; withdrawing cargo does not reserve a project. A delivery clears that player's tracked Outbound (or Inbound when supplied directly) without debiting carrier stock again. Depot observations update remaining requirements from RequiredAmount minus ProvidedAmount, matched by project MarketID. Newer authoritative observations take precedence; stale Raven data must not reintroduce already delivered need. Original totals remain cached internally. CargoDepot, purchases, mining and inventory edits never count as construction contributions.

Enable Report deliveries to Raven Colonial only if this app should upload confirmed new contributions and depot observations. Avoid another uploader for the same commander. Reporting is independent of cargo tracking mode and saves requests before journal acknowledgment/network submission. Historical contributions are not uploaded automatically. During outages queues remain durable; uncertain contribution outcomes require review under Delivery reports rather than blind automatic retries. Requirements refresh independently of stock/cargo synchronization, so brief display lag can occur until the depot/Raven observation arrives.

## Complete construction

Completion requires a new journal ConstructionComplete boolean true and reporting enabled. Zero remaining requirements alone must not complete a project. Final contribution credits must finish or be explicitly resolved first. The app checks Raven before posting completion and verifies afterward; interrupted checks resume through the saved reporting state. Review unresolved outcomes in Delivery reports. Do not clear queues or overwrite inventory to resolve a Raven mismatch.
