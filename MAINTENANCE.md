# BetterREPO maintenance guide

Thunderstore is the distribution channel. This repository is the source of truth.

## Versioning

- PATCH: config/documentation/compatibility tweaks and dependency version bumps.
- MINOR: add, remove, or replace a mod/map, or meaningful gameplay/balance changes.
- MAJOR: large redesigns or updates where a clean r2modman profile is recommended.

## Update workflow

1. Export the current r2modman profile as `.r2z`.
2. Run `python scripts/sync_profile.py "/path/to/profile.r2z"`.
3. Review `git diff`.
4. Bump `version_number` in `package/manifest.json`.
5. Add the new version to `package/CHANGELOG.md`.
6. Run `python scripts/validate.py` and `python scripts/build.py`.
7. Commit and push to `main`.
8. Create and push a matching tag such as `v1.1.0`.
9. GitHub Actions builds the ZIP and creates a GitHub Release.
10. Upload the generated ZIP to Thunderstore.

The sync script only accepts Thunderstore-shaped package identifiers, excludes the installed BetterREPO package itself and other local/non-package entries, and only refreshes tracked config files when their effective settings actually change. Unknown configs and local profile files are ignored intentionally.

## Removing mods

Removing a dependency from the modpack does not guarantee that an already-installed copy disappears from an existing r2modman profile. Mention removals clearly in the changelog. For disruptive removals, consider a major version and recommend a clean profile.

## Mods

Mod source lives in `mods/<ModName>/`, separate from the modpack in `package/`. Mods are versioned, built and published on their own schedule; the modpack's version and CI only cover `package/`. See [mods/README.md](mods/README.md) for build notes. Changing a mod does not require a modpack release unless the pack's pinned dependency on it changes.
