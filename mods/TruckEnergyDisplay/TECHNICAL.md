# Truck Energy Display 1.3.0: implementation and discovery

## Evidence and scope

The build inspects a real, unstripped `Assembly-CSharp.dll` obtained from the public [Laamy/ClydeMenu reference-assembly snapshot](https://github.com/Laamy/ClydeMenu/tree/3bbf20895c62e17894d2f46a7f07523fcfdd030d/ClydeMenu/dlls). It was decompiled with ILSpy 9.1.0.7988, and the shipping IL reader was also executed directly against its method bodies in the test suite.

Reference assembly SHA-256:

`137d6e8475dea976831cc95d7f56f4b7da311e52a57b4c420591a5122f25589f`

The [R.E.P.O.GameLibs.Steam 0.4.4-ngd.0 package](https://www.nuget.org/packages/R.E.P.O.GameLibs.Steam/0.4.4-ngd.0) provides a separate publicized, stripped reference API. A second build against it verifies member compatibility only, not current method behavior. Its Assembly-CSharp SHA-256 is:

`fdd411835ede5602feeb29ab3b79da1cfbbdd5b0a87904fe047f63fc26d28f39`

Neither reference is claimed to be the user's installed game. The DLL validates the installed method bodies at startup. Game assemblies and decompiled game sources are excluded from both deliverables.

## Truck reserve

| Member | Observed purpose | Implementation choice |
| --- | --- | --- |
| `ChargingStation` | Shared charging station component | Uses its singleton and crystal metadata |
| `ChargingStation.chargeFloat` | Normalized active charge | Establishes the normalization expression; not trusted as the shop purchase ledger |
| `ChargingStation.chargeTotal` | Integer charge representation | Loaded from the run stat; can remain stale during shopping |
| `StatsManager.runStats["chargingStationChargeTotal"]` | Saved run reserve, updated by checkout | Authoritative shop energy, read only on host/singleplayer |
| `ChargingStation.Start` | Loads reserve, caps it using owned crystals, normalizes it | Extracts and evaluates the verified arithmetic expression without invoking Start |
| `ChargingStation.maxCrystals` | Owned-crystal computation parameter | Live field read if present in the extracted expression |
| `ChargingStation.energyPerCrystal` | Station loading/drain parameter | Live field read; not assumed to equal the checkout credit |
| `ChargingStation.chargeSegments` | Visual bar segmentation | Never used to infer crystal recharge |

The full snapshot normalizes `chargeTotal` by 100, and both purchase routines clamp to 100. The mod extracts those operands and requires them to agree. It does not use a fabricated `maxEnergy` field or a hardcoded 100-unit fallback.

The shop disables the station's ordinary charging update. Reading only its `chargeFloat` after checkout would leave stale information. The run stat is what purchases update and the next truck reads. It is integer-valued in the inspected build, so inventing fractional precision from the inactive component would be wrong. The pure calculation and compatibility API retain fractional values for other supported models.

## Power Crystal behavior

The inspected `PowerCrystal.Start` registers its physical object with `ItemManager.powerCrystals`. The energy credit occurs in:

- `ExtractionPoint.DestroyTheFirstPhysObjectsInShopList`
- `ExtractionPoint.DestroyAllPhysObjectsInShoppingList`

Both routines check `ItemAttributes.item.itemType == SemiFunc.itemType.power_crystal`, record the purchase, add **17** to the run reserve and cap it at **100** in the inspected binary. These values are discovered from IL at runtime, not embedded into the shipping model.

The station's `energyPerCrystal` field is **10** in this snapshot. Treating it as the checkout increment would disagree with the actual purchase code. The older source bundled alongside the public assembly also differs from the binary, which is why the binary was decompiled and tested directly.

At the next truck's `Start`, the inspected binary additionally computes an owned-crystal limit equivalent to:

```csharp
RoundToInt((float)(owned * energyPerCrystal / maxCrystals) * energyPerCrystal)
```

The first multiplication and division are **integer arithmetic**. The mod extracts the expression and preserves conversion and rounding order. It does not simplify it into a mathematically different floating-point formula. `RoundToInt` is modeled with ties-to-even rounding of a single-precision input.

The owned-item dictionary key is extracted from Start's call to `SemiFunc.StatGetItemsPurchased`. It is not a hardcoded prefab ID. The crystal item definition is identified through `ChargingStation.item` and item-type metadata. A different item definition tagged as a crystal is counted in checkout, but prediction requires an adapter because it may credit a different ownership entry.

## Projection

For the verified positive, constant-per-purchase model:

```text
savedAfterCheckout = min(checkoutCap, currentSavedReserve + selectedCount * checkoutIncrement)
ownedAfterCheckout = currentOwnedCount + selectedCount
projected = min(savedAfterCheckout, stationStartupLimit(ownedAfterCheckout))
```

Both checkout routines must expose the same increment and cap. The minimum number needed is the first nonnegative additional crystal count whose projection reaches the verified maximum. The bounded search supports the station's integer stair-step loading formula. It deliberately avoids assuming that `ceil(missing / checkoutIncrement)` is sufficient.

Example confirmed against the full binary: from zero reserve and zero owned crystals, six purchases credit the ledger to 100 but the next station loads only 60. Ten owned crystals are needed to load 100 with that snapshot's station parameters. This behavior is version-specific, not a universal claim about every R.E.P.O. release.

Buying zero, exact full, fractional adapter values, extra crystals, insufficient funds and removal from checkout are handled. Extra count is only emitted after checking monotonicity and reaching full. Unreachable or oversized calculations fail closed at a computational bound of 10,000 candidate crystals; this bound is unrelated to game capacity.

## Checkout authority

`ShopManager.shoppingList` contains `ItemAttributes`. `ShopManager.ShopCheck` and `PunManager.ShopUpdateCost` remove entries whose `roomVolumeCheck.inExtractionPoint` flag is false. The latter synchronizes the total cost, not an authoritative crystal count.

The reader uses the host's shopping list, filters it through the already-maintained zone flag, ignores destroyed entries, deduplicates instance IDs, and checks the item's metadata. It never calls ShopCheck, CheckSet, overlap queries, or purchase methods. It never scans the shop for crystals. Values/prices of all selected items are summed only to warn about insufficient currency.

Completed purchases modify the run stat and remove entries from the list. A fresh sample reads both, so already credited purchases are not counted again. Re-entering the shop clears cached display/network state and reuses the game's fresh authoritative state.

## Save and multiplayer behavior

The game stores run stats in `StatsManager.dictionaryOfDictionaries`; save/load serializes that state. `SemiFunc.OnSceneSwitch` records the station charge outside the shop and calls normal stat synchronization. Charging behavior runs on the master/singleplayer side. In the inspected binary, segment RPCs convey quantized charge during active charging; the shop purchase list is not reliably conveyed as a shared per-item list to every client.

Therefore the mod reads game state only on the host and sends **display snapshots** to modded clients. It does not create a second writable energy authority or alter game synchronization.

- Photon custom event code 197 with a namespaced protocol marker.
- Clients request a snapshot with a random nonce and completed-level count.
- Only the current master publishes display data; clients reject any other sender.
- Replies echo the client's nonce. Nonces rotate after scene/room/master changes, preventing late packets from previous visits being accepted.
- Packets validate expected types, finite energy values and count bounds.
- Snapshots are targeted to requesting players, reliable and not cached in the room.
- A five-second freshness timeout replaces stale client values with a waiting message.
- Requests are rate-limited; host sampling is 0.25 seconds by default. No per-frame networking.
- On master changes, cached values and subscriptions are cleared. If the game continues and the new master has the mod, clients request fresh data. This does not add host-migration support to the game itself or reconstruct state the game has lost.
- A host without this mod yields no invented client prediction.

Clients cannot write game charge through this protocol. No message handler calls a purchase, inventory, save, charge or currency mutation.

## Mods and upgrades

The inspected purchase path has a constant credit and hardcoded normalized cap in its IL, with no upgrade multiplier in that path. Live station loading parameters are read from the actual component. Shop crystal-price growth changes currency cost, not the energy increment.

The default adapter recognizes only the inspected expression shapes. It does not guess names such as `maxEnergy` or `rechargeAmount`. It checks Harmony patch registrations every two seconds on the relevant station, checkout, ownership-getter and scene-switch methods. Any registered patch conservatively disables vanilla prediction; potentially changed capacity also disables the percentage. This includes harmless patches that cannot be proven harmless from registration metadata. The mod itself installs no Harmony patches and does not rerun another mod's transpilers.

For explicit support, another mod can assign:

```csharp
TruckEnergyCompatibility.Provider = (station, checkout) =>
{
    // Read your own final runtime model and return an EnergySnapshot.
    // Return null to leave handling to the vanilla discovery path.
    // No game changes should be made by a display provider.
    return YourReadOnlyAdapter.CreateSnapshot(station, checkout);
};
```

The callback runs only on the host/singleplayer main thread, receives a read-only checkout view and can report fractional current/max/projected values. Return a fresh snapshot and do not mutate it afterwards. One provider is supported; integrations should coordinate ownership of the callback. Snapshot values are validated before broadcast. A provider cannot be configured by remote clients.

Non-Harmony detours and modifications in arbitrary unobserved code cannot be detected generically. In-game compatibility needs to be confirmed with the actual mod set.

## Validation and remaining checks

The release compiled with .NET SDK 8.0.425 / .NET Standard 2.1 against real BepInEx 5.4.21 and game/Unity/Photon references, and also compiled against the 0.4.4 reference API with zero warnings/errors. No fake/stub game API was written to obtain a successful build.

The 29 executable checks include the real binary's checkout increment, normalization, ownership key and integer loading expression; arithmetic edges; and rejection of non-host, old-master, old-session, wrong-level, NaN and negative-count packets.

Still requiring a live game session: two-player transport, exact font/layout appearance at 1080p/1440p/ultrawide, add/remove/purchase interactions, save reload, repeated shop transitions and actual game behavior on host departure. No such live tests are claimed.
