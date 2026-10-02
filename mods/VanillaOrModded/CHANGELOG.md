# Changelog

## 1.0.0

- Fresh public release as **VanillaOrModded** by **profiel**.
- Replaced the private host-only selector with synchronized multiplayer map-type voting.
- Added host-authoritative capability handshakes, protocol validation, session IDs, anonymous totals, vote changes, disconnect cleanup, late-join exclusion, and graceful unmodded-client behavior.
- Added Vanilla, Modded, and combined-pool Random resolution with host-authoritative tie breaking.
- Added configurable no-repeat history with a safe single-map fallback.
- Added host-authoritative voting, timing, history, map-name visibility, blacklist, and whitelist settings.
- Added client-local UI scale and position settings.
- Added mouse selection, 1/2/3 shortcuts, selected-row highlighting, green vote blocks, red final countdown, and a host force-finish control.
- Added exact loaded-plugin GUID detection for MapVote and safe self-disable behavior.
- Added a soft dependency on `Patrick.MapVote` so conflict detection runs after MapVote has loaded.
- Reset map history when leaving a multiplayer room.
- Kept selected map identifiers out of normal logs unless `ShowChosenMapName` is enabled.
- Increased the capability handshake window to one second.
- Hardened stale vote-session rejection across all client UI states.
- Kept session recovery safe across a Photon host migration by scoping the session watermark to the current master.
- Required synchronized totals and results to come from the current Photon master during host migration.
- Removed the unused vote-cancel protocol message.
- Retained the proven base-game object capture plus REPOLib registration-object map classification.
- The first automatically selected map on a brand-new save remains a known limitation by design.
