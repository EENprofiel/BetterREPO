# Mods

Source code for the individual mods that accompany the BetterREPO modpack. Each mod is a standalone project in its own folder and is released independently of the modpack in `package/`.

| Folder | Version | What it does |
| --- | --- | --- |
| [VanillaOrModded](VanillaOrModded/) | 1.0.0 | Host-authoritative anonymous multiplayer voting between vanilla, modded or random maps. |
| [TruckEnergyDisplay](TruckEnergyDisplay/) | 1.2.0 | Shop HUD for the shared truck charging station energy and Power Crystal purchase prediction. |
| [OwnedEquipmentHUD](OwnedEquipmentHUD/) | 1.1.0 | Shop HUD showing reusable equipment the team already owns this run. |
| [BetterReviveHealth](BetterReviveHealth/) | 1.0.0 | Host-configured health for players revived at an extraction point. |

None of these have been live tested in a multiplayer game from this repository yet. See each mod's own README and changelog for its remaining checks.

## Layout of a mod folder

Every mod has the same set of top-level files:

```text
mods/<ModName>/
  README.md              What the mod does, config and install notes
  CHANGELOG.md           Release history
  LICENSE                MIT license text
  manifest.json          Thunderstore manifest (the only Thunderstore metadata file)
  icon.png               Thunderstore icon, 256x256 PNG
  <ModName>.csproj       Build project
  <source files>         Plugin source (see below)
  lib/                   Build-only reference DLLs, not committed (see lib/README.md)
```

The manifest `version_number` must match the plugin version in code and the newest entry in `CHANGELOG.md`.

Source files are not forced into one folder shape. Each mod keeps the structure that fits its size: flat files in the mod root, `src/`, or topic folders such as `Patches/`. Do not move source files only to make the folders look alike.

Per-mod `.gitignore` files and the root `.gitignore` keep `bin/`, `obj/`, `dist/` and DLLs out of Git.

## Building

The mods target BepInEx plugins (`netstandard2.1`, .NET 8 SDK). Install the .NET 8 SDK and run `dotnet build -c Release` inside the mod folder.

- By default TruckEnergyDisplay, OwnedEquipmentHUD and BetterReviveHealth compile against BepInEx, Unity and R.E.P.O. stub packages from NuGet (`mods/CloudRefs.props`). This works in the cloud and in CI. The stubs have no method bodies, so a successful build proves the code compiles. It does not prove the mod works in the game.
- To build against a real install, pass `-p:RepoGameDir=<game dir>` (or set `REPO_GAME_DIR`).
- VanillaOrModded also needs `MenuLib.dll` and `REPOLib.dll` in its `lib/` folder (see `lib/README.md`).

TruckEnergyDisplay also has an executable check project in `tests/` and a `package.py` that builds the Thunderstore ZIP.

## Continuous integration

`.github/workflows/mods.yml` runs on every pull request that touches `mods/`:

| Check | Covers |
| --- | --- |
| Layout and versions | All four mods. Required files, 256x256 icon, manifest, changelog heading and code version agree (`scripts/check_mods.py`). |
| Build and test all mods | A Release build of every mod against the NuGet stub packages (see Building), with MenuLib and REPOLib downloaded from Thunderstore for VanillaOrModded. Then every test project in `mods/*/tests/` or `mods/*/*.Tests/`, which includes the TruckEnergyDisplay checks. |

The stubs have no method bodies. CI proves that the code compiles and that the tests pass. It cannot prove that a mod works in the game. The check of TruckEnergyDisplay against the real game assembly only runs when you pass a game folder locally. In-game testing stays a manual step.

## Releasing a mod

See "Releasing a mod" in [MAINTENANCE.md](../MAINTENANCE.md). Tags look like `VanillaOrModded-v1.0.0`.

## Adding a new mod

1. Create `mods/<ModName>/` with a README, CHANGELOG, LICENSE, icon, manifest.json, csproj and source.
2. Add a row to the table above and to the root `README.md`.
3. Add the mod to `scripts/mods.json`.
4. Keep game assemblies and built DLLs out of Git.

## Conventions to keep

- Existing mods keep their current config GUIDs and Thunderstore identities. Do not rename or rebrand them while moving things around.
- VanillaOrModded is a separate public mod. It is not part of the BetterREPO modpack and must not be mentioned in `package/` (the Thunderstore README, changelog, manifest or configs).
- Informational HUD mods must not change purchases, inventory, currency, energy, saves or game-owned network state.
