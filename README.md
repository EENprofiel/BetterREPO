# BetterREPO

Source repository for the **BetterREPO** R.E.P.O. modpack published on Thunderstore.

BetterREPO is a curated multiplayer pack focused on quality-of-life improvements, smoother co-op progression, extra levels, shop improvements, late joining, and a handful of fun additions without turning R.E.P.O. into a completely different game.

> Players should install and update BetterREPO through **r2modman / Thunderstore Mod Manager**. This repository is the maintenance source for the package.

## Repository layout

This repository holds the modpack and the source for the mods built alongside it.

```text
mods/                    One folder per mod (source, README, build files)
  VanillaOrModded/
  TruckEnergyDisplay/
  OwnedEquipmentHUD/
  BetterReviveHealth/
package/                 Exact files packaged for Thunderstore
  manifest.json          Package metadata, version and pinned dependencies
  README.md              Thunderstore page content
  CHANGELOG.md           Public release history
  icon.png               Thunderstore icon
  config/                Curated configuration shipped with the pack
scripts/
  validate.py            Package validation
  build.py               Creates the upload-ready ZIP in dist/
  sync_profile.py        Syncs an r2modman .r2z export into the tracked package
.github/workflows/
  build.yml              CI validation, build artifacts and tagged GitHub releases
MAINTENANCE.md            Release and versioning workflow
```

See [mods/README.md](mods/README.md) for the list of mods, how to build them and how to add a new one.

## Current version

**1.3.1**

The `package/` directory is the source of truth for the Thunderstore package.

## Quick maintenance flow

```bash
python scripts/sync_profile.py "/path/to/latest-profile.r2z"
python scripts/validate.py
python scripts/build.py
```

Then review the Git diff, update `package/CHANGELOG.md`, bump `version_number` in `package/manifest.json`, commit, and tag the release as `vX.Y.Z`.

See [MAINTENANCE.md](MAINTENANCE.md) for the full workflow.

## Links

- [BetterREPO on Thunderstore](https://thunderstore.io/c/repo/p/profiel/BetterREPO/)
