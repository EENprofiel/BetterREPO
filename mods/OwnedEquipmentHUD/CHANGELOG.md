# Changelog

## 1.1.0

- Show the purchase limit next to the owned count (for example `x2/3`) when the game exposes one. Items without a limit show only the count.
- Shop context line shows the limit and `Limit reached` when the team is at the limit.
- Added `DisplayMode` (`Text`, `Icons`, `Both`) using each item's icon. Items without an icon fall back to text.
- Added `CompactLayout` and clamped the panel to the screen so it stays visible on 1080p, 1440p and ultrawide.
- Verbose logging now prints the purchase limit and icon.

## 1.0.0

- Added a Service Station HUD for run-owned reusable equipment.
- Uses `StatsManager.GetItemPurchased(Item)` rather than scene objects or truck objects.
- Added dynamic item discovery for normal and modded shop equipment.
- Added optional focused shop-item owned/after-purchase counts.
- Added configurable row limit, zero-count filtering, and verbose diagnostics.
