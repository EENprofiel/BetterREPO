# Mods

Source code for the individual mods that accompany the BetterREPO modpack. Each mod is a standalone project in its own folder and is released independently of the modpack in `package/`.

| Folder | Version | What it does |
| --- | --- | --- |
| [VanillaOrModded](VanillaOrModded/) | 1.0.0 | Host-authoritative anonymous multiplayer voting between vanilla, modded or random maps. |
| [TruckEnergyDisplay](TruckEnergyDisplay/) | 1.2.0 | Shop HUD for the shared truck charging station energy and Power Crystal purchase prediction. |
| [OwnedEquipmentHUD](OwnedEquipmentHUD/) | 1.0.0 | Shop HUD showing reusable equipment the team already owns this run. |
| [BetterReviveHealth](BetterReviveHealth/) | 1.0.0 | Host-configured health for players revived at an extraction point. |

None of these have been live tested in a multiplayer game from this repository yet. See each mod's own README and changelog for its remaining checks.

## Layout of a mod folder

```text
mods/<ModName>/
  README.md              What the mod does, config and install notes
  CHANGELOG.md           Release history (where present)
  manifest.json          Thunderstore manifest (where present)
  <ModName>.csproj       Build project
  *.cs                   Plugin source
  lib/                   Build-only reference DLLs, not committed (see lib/README.md)
```

Per-mod `.gitignore` files and the root `.gitignore` keep `bin/`, `obj/`, `dist/` and DLLs out of Git.

## Building

The mods target BepInEx plugins (`netstandard2.1`, .NET 8 SDK) and compile against game and dependency assemblies that are not redistributed here. For each mod:

1. Install the .NET 8 SDK.
2. Place the reference assemblies the mod's README asks for in its `lib/` folder (or set `RepoGameDir` for the mods that read it).
3. Run `dotnet build -c Release` inside the mod folder.

TruckEnergyDisplay also has an executable check project in `tests/` and a `package.py` that builds the Thunderstore ZIP.

## Adding a new mod

1. Create `mods/<ModName>/` with a README, csproj, source and (if it will be published) a `manifest.json`.
2. Add a row to the table above and to the root `README.md`.
3. Keep game assemblies and built DLLs out of Git.

## Conventions to keep

- Existing mods keep their current config GUIDs and Thunderstore identities. Do not rename or rebrand them while moving things around.
- VanillaOrModded is a separate public mod. It is not part of the BetterREPO modpack and must not be mentioned in `package/` (the Thunderstore README, changelog, manifest or configs).
- Informational HUD mods must not change purchases, inventory, currency, energy, saves or game-owned network state.
