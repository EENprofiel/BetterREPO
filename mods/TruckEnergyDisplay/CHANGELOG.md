# Changelog

## Unreleased

- Adds `Anchor` (four screen corners) and `Opacity` settings. Margins and scale now apply from the chosen corner, are clamped on screen, and update live.

## 1.2.0

- Replaces heuristic field-name and visual-segment fallbacks with runtime IL discovery.
- Uses the host's saved shop reserve, preventing stale post-purchase station values.
- Reproduces checkout credit, cap, and the next truck's owned-crystal loading limit.
- Uses only the authoritative checkout list, zone membership and crystal metadata.
- Adds targeted host snapshots, stale-packet rejection, timeouts and scene/master resets.
- Replaces IMGUI with a compact TextMeshPro shop-only HUD and optional yellow energy bar.
- Adds conservative detection of modified charging logic and a read-only compatibility API.
- Builds against a real game binary and the 0.4.4 reference API. Includes 29 executable checks.

## Earlier versions

1.0.0 and 1.1.0 used heuristic state interpretation. Their claims about inspected fields,
crystal increments and automatic multiplayer consistency are superseded by TECHNICAL.md.
