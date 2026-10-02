# Truck Energy Display 1.2.0

A small, read-only R.E.P.O. HUD showing the shared truck charging-station reserve while you are in the Service Station. It does not display weapon batteries.

```text
TRUCK ENERGY
63%

Need: 4 crystals
Buying: 3 -> 93%
Still needed: 1
After checkout and truck loading
```

This is an illustrative layout. The numbers come from the running game's verified charging and purchase logic, not from a hardcoded percentage per crystal.

## Install

Requires BepInExPack 5.4.2100. Install the ZIP with your mod manager, or extract the `BepInEx` directory from the ZIP into the R.E.P.O. directory.

For manual installation the DLL belongs at:

`BepInEx/plugins/TruckEnergyDisplay/TruckEnergyDisplay.dll`

Replace/remove any older TruckEnergyDisplay DLL. Do not install two versions at once. Keep your existing config: the requested display switches retain their names. Legacy `DisplayMode`, `Enabled`, `ShowCrystalEquivalent`, and the old centered-position offsets are no longer used. This version is deliberately shop-only; use `ShowTruckEnergy` to hide it.

**Multiplayer: the host and every player who wants the HUD must install 1.2.0.** Players without the mod can still join normally. A client whose host does not have the mod waits for host data instead of guessing from a local shopping list. Hiding the host's HUD does not disable synchronization.

## What the display means

- **Truck energy:** the host's saved charging-station reserve. In the shop, checkout changes this run value; the station component can still contain its pre-purchase value.
- **Need:** additional normal Power Crystals needed to reach full after checkout and the next truck's initialization.
- **Buying:** crystals currently selected in the game's actual checkout list and still flagged inside its purchase zone. Shelved crystals, crystals elsewhere in the room, and previously purchased inventory are not counted.
- **Buying N -> FULL / percentage:** hypothetical energy after buying those selected crystals and loading the next truck. This is conditional on completing the purchase.
- **Still needed:** additional normal crystals required beyond the selection.
- **Extra:** only shown when the projection is full and the counted normal crystals have a consistent, monotone effect. It means extra for filling this reserve, not necessarily that the purchase has no other value.
- **Not enough money:** the selected checkout, including other items, exceeds available currency. The prediction still describes the hypothetical complete purchase.

The inspected game credits energy during checkout and applies an additional owned-crystal limit when the next truck loads. Those two steps can give different results. The mod reproduces both. See `TECHNICAL.md` for the exact evidence and arithmetic.

No crystals are consumed, moved or purchased by this mod. It never writes energy, inventory, currency, save data or game-owned network properties.

## Configuration

Created after first launch at:

`BepInEx/config/com.lucasdoddema.truckenergydisplay.cfg`

```ini
[Display]
ShowTruckEnergy = true
ShowCrystalRequirement = true
ShowCheckoutPrediction = true
ShowEnergyBar = true
ShowPercentage = true
ShowRawEnergy = false
RightMargin = 40
TopMargin = 220
Scale = 1

[Performance]
RefreshInterval = 0.25
```

The TextMeshPro panel is anchored toward the upper right, below the top HUD area. It borrows an existing game UI font, has no input handling, and scales against screen height. Change the margins if it overlaps another mod's HUD. Ultrawide positioning uses the right screen edge, with bounds clamping.

The HUD is hidden outside the Service Station and before the main gameplay state is ready. Values refresh four times per second by default; text is rebuilt only when its displayed content changes. Crystal and gameplay objects are never searched for across the scene. The only scene search is a font lookup when creating the HUD.

## Compatibility and limits

The vanilla adapter reads method IL from the installed game's assembly at runtime. It checks both checkout routines, charge normalization, and the station's owned-crystal loading expression. It reads the expression's live station fields. It does not infer crystal value from visual bar segments or assume that `maxCrystals * energyPerCrystal` is the energy cap.

Unrecognized charging logic, custom crystal definitions, or Harmony patches on watched charging/purchase methods disable the affected display/prediction with a diagnostic in `LogOutput.log`. Capacity-changing patches can also make the percentage unavailable. This conservative rule includes some harmless logging patches. Other mod authors can provide a read-only adapter through `TruckEnergyCompatibility.Provider`; see `TECHNICAL.md`.

Arbitrary native detours, non-Harmony code replacement, or modifications outside the inspected data path cannot be detected generically. There is no claim of universal compatibility.

## Validation

- Release build: successful, zero warnings/errors against the inspected full assembly and against the R.E.P.O. 0.4.4 reference API.
- 29 executable checks cover calculations, fractional values, full/empty reserves, overbuying, checkout changes, unsupported cases, packet validation and direct IL discovery from the inspected full game assembly.
- The full assembly used for behavioral inspection is a public reference snapshot, not a copy of your installed game. The 0.4.4 reference package has stripped method bodies, so API compatibility is verified separately from behavioral verification.
- No live Unity game session or two-player lobby was available here. UI placement, loading behavior and multiplayer transport still need an in-game smoke test. Unsupported runtime IL is reported instead of silently using the reference snapshot's constants.

## Build from source

Requires .NET SDK 8+, the installed game's managed assemblies, and BepInEx's core directory. Game and third-party assemblies are not redistributed in the source ZIP.

```powershell
dotnet build -c Release -p:RepoGameDir="C:\Program Files (x86)\Steam\steamapps\common\REPO"
```

With BepInEx in a separate mod-manager profile:

```powershell
dotnet build -c Release -p:ManagedDir="C:\Games\REPO\REPO_Data\Managed" -p:BepInExCoreDir="C:\YourProfile\BepInEx\core"
```

Output: `bin/Release/netstandard2.1/TruckEnergyDisplay.dll`.

```powershell
dotnet run --project tests/Tests.csproj -c Release
```

The optional assembly inspection tests are tied to the documented reference snapshot:

```powershell
dotnet run --project tests/Tests.csproj -c Release -- "C:\path\to\reference\Managed"
```

Use `python package.py` after building to create the installable ZIP and source ZIP under `dist`.
