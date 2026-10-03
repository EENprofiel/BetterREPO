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

## Releasing a mod

Each mod has its own version and its own tag: `<ModName>-vX.Y.Z`, for example `VanillaOrModded-v1.0.1`. The modpack keeps tags like `v1.2.0`.

1. Change the version in three places: `manifest.json`, `PluginVersion` in the plugin source, and the csproj `<Version>` if it has one.
2. Add a `## X.Y.Z` section to the mod's `CHANGELOG.md`.
3. Run `python scripts/check_mods.py`. It must pass.
4. Merge to `main`, then create and push the tag: `git tag VanillaOrModded-v1.0.1 && git push origin VanillaOrModded-v1.0.1`.
5. The `Mod release` workflow checks that the tag, manifest and changelog agree, builds the mod, and creates a GitHub release with `<ModName>-X.Y.Z.zip` attached.

Only VanillaOrModded can be built in CI. For the other mods, build locally and then:

1. `dotnet build -c Release` in the mod folder.
2. `python scripts/package_mod.py <ModName>`. This writes `dist/<ModName>-X.Y.Z.zip`.
3. `gh release create <ModName>-vX.Y.Z dist/<ModName>-X.Y.Z.zip --generate-notes`.

The workflow stops with a clear error if one of these mods is tagged and pushed without a local release.

### Thunderstore upload

Without a token, upload the ZIP by hand at thunderstore.io. To upload from CI, add the repository secret `THUNDERSTORE_TOKEN` (a Thunderstore service account token). The workflow then runs `tcli publish` after the GitHub release. Namespace and categories per mod are in `scripts/mods.json`.
