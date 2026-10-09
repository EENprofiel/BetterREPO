# Owned Equipment HUD

`Owned Equipment HUD` is a lightweight BepInEx mod for R.E.P.O. that shows the reusable equipment the current run owns while players are in the Service Station.

Example:

```text
OWNED EQUIPMENT
Tranq Gun             x2
Handgun               x1
Pocket Cart           x1
```

The list is informational only. It does not purchase, spawn, delete, or modify any equipment.

## Persistence investigation

The mod does not count GameObjects in the truck or the current scene.

R.E.P.O. exposes an ownership accessor on `StatsManager` named `GetItemPurchased(Item)`. Current R.E.P.O. mods that need to show owned item totals use that accessor. The HUD calls it for every dynamically discovered `Item` definition and uses the returned value as the displayed total.

The runtime also exposes `StatsManager.itemsPurchased` and `itemsPurchasedTotal` in the run-save/statistics model. Their raw save semantics are not stable enough to substitute for the game's own accessor, so the plugin uses the accessor rather than reimplementing save arithmetic. `StatsManager.itemDictionary` supplies the item definitions and metadata needed to call it.

`ItemManager.purchasedItems` is deliberately not used as the ownership source. The game consumes that list while populating the next truck, so it represents remaining spawn work/stock at that point in the transition, not the complete run-owned inventory. `ItemManager.spawnedItems` is likewise only a collection of physical scene instances. The mod only reads `purchasedItems` in verbose debug logs to make this distinction visible.

This explains the observed death case: a physical item can be absent from the level and from the current truck instance list without the run-level purchased count being decremented. During the level-to-shop transition, the game can still have that purchased item in its restore/spawn pipeline and recreate it in the next truck. The HUD therefore reports the authoritative total and does not invent an `In truck` or `Returning` split.

The more detailed field-by-field investigation is in [`docs/PERSISTENCE-RESEARCH.md`](docs/PERSISTENCE-RESEARCH.md).

## Discovery and modded equipment

The mod reads the game's `StatsManager.itemDictionary` at runtime. It uses each item's own `itemName`, `name`, `itemType`, and `disabled` metadata. There is no hardcoded vanilla item dictionary.

The game's item category metadata is used to exclude player/item upgrades, health packs, power crystals, grenades, and mines. Unknown/new categories are allowed so a modded reusable item that registers through the normal `Item`/shop system appears without a mod-specific integration. Valuables and ordinary loot are not part of this purchased `Item` category path; if a future mod registers a non-equipment item as an `Item`, its category must be classified by the game or it may be treated as an unknown equipment category.

## Multiplayer

Each installed client reads the game's own `StatsManager` state. The game synchronizes that state through its normal network sync path, and the mod refreshes after `ItemPurchase`, `ReceiveSyncData`, loading, shop initialization, and truck population. The mod never writes to ownership and never accepts a client request to change it.

If the game's own synchronization has not completed yet, the HUD temporarily shows `Reading run inventory...` rather than estimating from local physical objects.

## Configuration

The generated config is `BepInEx/config/lucas.repo.owned-equipment-hud.cfg`:

```ini
[Display]
ShowEquipmentList = true
ShowOwnedCountOnShopItems = true
DisplayMode = Text
CompactLayout = false
MaxVisibleRows = 12
HideZeroCountItems = true

[Debug]
VerboseEquipmentLogging = false
```

The list is anchored to the upper-right safe area, scales with the screen height, and becomes scrollable when there are more rows than `MaxVisibleRows`. The optional shop-item context line appears when the player is directly looking at an item in the Service Station:

```text
Tranq Gun  |  Owned: 2
After purchase: 3
```

The context line is also based on `StatsManager.GetItemPurchased(Item)`. It is only a preview of one additional accepted purchase, not a promise that the team can afford the item or that the shop has not otherwise changed.

## Purchase limit

When the game exposes a purchase limit for an item (`Item.maxPurchase` and `Item.maxPurchaseAmount`, both checked against the game's assembly), the list shows it after the owned count, for example `x2/3`. The shop context line shows `Owned: 2/3`, and `Limit reached` instead of the after-purchase count when the team is at the limit. Items with no limit show only the count. The owned count always comes from `StatsManager.GetItemPurchased(Item)`.

## Icons and compact layout

`DisplayMode` selects how rows are drawn:

- `Text`: item names (default, same as 1.0.0).
- `Icons`: the item's icon plus the count. The game's `Item` has no icon field. The icon is `ItemAttributes.icon` on the item's prefab (when `hasIcon` is set). It is also learned when you look at the item in the shop.
- `Both`: icon, name and count.

An item with no icon always falls back to its name. `CompactLayout = true` uses a smaller panel with tighter rows. In every mode the panel is clamped to the screen: its width never exceeds the screen width, and the number of visible rows is reduced when the screen is too short, so the list scrolls instead of leaving the screen. The panel is anchored to the upper-right corner, so it stays in view on 1080p, 1440p and ultrawide screens.

## Debug logging

With `VerboseEquipmentLogging = true`, the mod logs the item identity, game category, authoritative `GetItemPurchased` result, the purchase limit, the icon name, and the separate `ItemManager.purchasedItems` remaining-spawn count. The latter is diagnostic only and is never displayed as ownership.

## Limitations

- The mod requires the R.E.P.O. game to expose the normal `StatsManager.GetItemPurchased(Item)` accessor and `itemDictionary`.
- It does not claim to identify which specific player is carrying an item.
- It does not show a reliable `returning`/`missing` state because the game's authoritative total does not expose that distinction through a documented public API.
- If an equipment mod does not register its item through R.E.P.O.'s normal `Item`/shop system, it cannot be discovered automatically.
- A disabled or uninstalled modded item cannot be resolved to a display entry, even if old run data references it.
- The raw save dictionaries are useful evidence for debugging, but this plugin intentionally does not derive ownership by adding or subtracting those fields itself.

## Building

Install the .NET SDK and set `RepoGameDir` to the R.E.P.O. installation directory. The project references BepInEx and Unity assemblies from that installation and intentionally does not reference or bundle `Assembly-CSharp.dll`.

PowerShell:

```powershell
dotnet build -c Release -p:RepoGameDir="C:\Path\To\R.E.P.O."
```

Bash:

```bash
dotnet build -c Release -p:RepoGameDir="/path/to/REPO"
```

The compiled `OwnedEquipmentHUD.dll` and `OwnedEquipmentHUD-1.1.0.zip` are written to `dist/`. The zip contains the DLL, manifest, icon, license, README, changelog, and persistence research. Copy the DLL to `BepInEx/plugins/` or install the archive with your mod manager.

## Compatibility note

The game-facing types are resolved reflectively instead of being compiled into the plugin. This keeps the plugin from shipping game code and allows it to tolerate harmless field visibility or assembly changes. The mod still intentionally fails closed when the authoritative ownership accessor is not available.

## Tests

`tests/` holds a console test project for the logic that does not need Unity or the game: purchase limit parsing and text, icon or text fallback, and panel layout on 720p, 1080p, 1440p and ultrawide screens. Run it with `dotnet run --project tests`.
