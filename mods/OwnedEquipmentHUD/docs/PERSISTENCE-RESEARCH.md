# R.E.P.O. equipment persistence investigation

This is the design record for `Owned Equipment HUD`. The plugin's central rule is:

> Display the result of the game's own `StatsManager.GetItemPurchased(Item)` method for each eligible item definition.

That method is preferable to reconstructing ownership from physical objects or from raw save dictionaries whose exact arithmetic is not publicly documented.

## Runtime objects and their roles

| Runtime source | Role | Used for the displayed total? |
| --- | --- | --- |
| `StatsManager.itemDictionary` | Catalog of `Item` definitions, including modded definitions registered before the read | Yes, for discovery and metadata |
| `StatsManager.GetItemPurchased(Item)` | Game accessor for the run's purchased count for one item definition | Yes, authoritative count |
| `StatsManager.itemsPurchased` | Run/save-backed purchased-item dictionary exposed by the game | Indirectly, through the game accessor; not reimplemented by this mod |
| `StatsManager.itemsPurchasedTotal` | Separate total-purchase dictionary in the run/save model | No; raw semantics are not sufficient to replace the accessor |
| `StatsManager.dictionaryOfDictionaries["item"]` | Exact item-instance records such as `Item Gun Tranq/1` | No; instance records are not a safe type-total calculation |
| `StatsManager.dictionaryOfDictionaries["itemStatBattery"]` | Sparse per-instance charge state for battery-backed items | No; charge is not ownership |
| `ItemManager.purchasedItems` | Items still available to the truck/shop spawn pipeline at a given transition point | No; this is remaining spawn work/stock |
| `ItemManager.spawnedItems` | Physical `ItemAttributes` instances currently tracked in the scene | No; this is not persistent ownership |

The plugin reads `ItemManager.purchasedItems` only when verbose logging is enabled, to make it possible to compare the diagnostic spawn-list value with the authoritative total without accidentally using the former for the HUD.

## Why death can appear to lose an item and then restore it

The important distinction is between:

1. the physical object/inventory instance that exists in the current level, and
2. the run-level purchase state used by the game's next shop/truck transition.

When a player dies, the physical item can leave the current scene or player inventory. That changes what can be found by scanning `ItemAttributes`, `ItemManager.spawnedItems`, or the truck GameObjects. It does not, by itself, prove that the run-level purchase record was destroyed.

At the level-to-shop transition, R.E.P.O. rebuilds the next physical presentation from its purchased-item/spawn pipeline. If the purchase state still reports the item as owned, the item can be instantiated again after the Service Station even though it was absent immediately after the death. This is the observed duplicate-purchase failure mode described in the feature request.

The exact point at which a particular physical item is finally destroyed is not exposed as a stable, documented ownership API. The mod therefore does not label an item as `missing`, `returning`, or `in truck`; it only shows the game's authoritative total. This avoids turning a transient physical absence into a false permanent-loss claim.

## Categories included by the reader

R.E.P.O.'s `Item.itemType` metadata distinguishes reusable equipment from non-equipment entries. The reader excludes the runtime category names:

- `item_upgrade`
- `player_upgrade`
- `power_crystal`
- `healthPack`
- `grenade`
- `mine`

Other category names are accepted, including future or mod-defined categories. This is deliberately category-based rather than a hardcoded list of vanilla display names. Display names are read from the `Item` itself (`itemName`, falling back to the Unity object name), so modded equipment participates automatically when it is registered in `itemDictionary` and uses the normal purchase path.

## Multiplayer and timing

The plugin does not send ownership changes and does not expose a write path. Each client reads its local `StatsManager` after the game's normal load/network synchronization. The reader is marked dirty after the game's purchase, sync, load, reset, shop initialization, and truck-population methods when those methods exist in the installed build.

The list is refreshed only when dirty. The fallback timer checks the dirty flag at most twice per second while in the Service Station. If the authoritative API is not ready or the network state has not arrived, the UI says `Reading run inventory...` rather than estimating from scene objects.

## Confidence and limitations

- High confidence for the displayed value: `GetItemPurchased(Item)` is the same accessor used by existing R.E.P.O. item-management mods to show an owned total.
- High confidence that `purchasedItems` is not a complete ownership total: it is consumed by the truck/item spawn flow and therefore varies with pending physical spawn work.
- The raw save keys confirm that purchase-related dictionaries exist, but their standalone field semantics are not stable enough to duplicate the game's accessor in a separate calculation.
- A reliable per-item `returning` state is not available through the public runtime surface used here.
- Equipment added outside the normal `Item`/shop registration path cannot be discovered without a future integration hook.
