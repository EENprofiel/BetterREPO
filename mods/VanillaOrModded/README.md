# VanillaOrModded

**VanillaOrModded lets everyone vote on whether the next R.E.P.O. level should be vanilla, modded, or random, then automatically selects a map without repeating the previous level.**

Players vote for a map **type**, never a specific map. The actual level stays a surprise by default.

## The three choices

- **VANILLA** chooses randomly from the currently available official base-game playable maps.
- **MODDED** chooses randomly from the currently installed and registered custom playable maps.
- **RANDOM** combines every eligible vanilla and modded map into one pool, then chooses one map directly. It does not perform a 50/50 category coin flip. If five vanilla and fifteen modded maps are installed, all twenty maps participate in the same draw.

The vote appears in the truck/loading phase before each new playable map selection. The first map of a new save is handled as described in "First map of a new save".

## Multiplayer voting

- The host starts and resolves every vote.
- One compatible player equals one vote.
- Votes may be changed until voting closes. Totals move immediately when a vote changes.
- Only anonymous green vote blocks are synchronized. Player names, Steam names, Steam IDs, and individual vote choices are never displayed.
- The vote ends after five seconds by default (`VotingDuration`, 2 to 60 seconds). Players who did not vote when time ends do not change the result. It also ends earlier when every eligible compatible participant has voted.
- The host can force-finish using the small **HOST: FINISH [F]** button or the **F** key.
- Keyboard shortcuts **1**, **2**, and **3** select Vanilla, Modded, and Random. Mouse selection is fully supported.
- Tied top categories are resolved by the host, using only the tied choices. The `TieBreak` option sets the rule.
- With zero votes, no category or map override is applied. Normal game selection remains untouched.

The host is authoritative. Clients submit only their own category choice; clients never select or apply a map.

### Players without the mod

VanillaOrModded does not block an otherwise valid lobby. A player without the mod simply does not participate and does not prevent early completion. A short capability handshake snapshots compatible participants at vote start. Late joiners wait until the next vote. If an eligible player disconnects, their vote and eligibility are removed.

Custom map packages are separate from this voting mod. Players still need whatever matching map content and dependencies are required to load the custom level selected by the host.

### Version mismatches

The networking envelope contains an explicit protocol version and session ID. Incompatible clients are excluded from voting instead of being kicked or blocking progression. Stale, malformed, wrong-session, late, and non-eligible submissions are ignored.

## Repeat prevention

Repeat prevention is enabled by default with a history length of one. The host builds the winning map pool, removes recently played identifiers, and draws once from what remains. It never rerolls repeatedly.

If filtering would remove every candidate, the original pool is restored so progression cannot break. This also makes a one-map category safe.

Increase `MapHistoryLength` to avoid the previous two or more maps. Set it to `0`, disable `PreventMapRepeats`, or both to turn history filtering off.

Map history is cleared when the multiplayer room is left, so a later lobby cannot inherit exclusions from an earlier run.

## Map detection

VanillaOrModded does not maintain a list of official map names.

At `RunManager.Awake`, it captures the serialized base-game `Level` objects before REPOLib's custom-level registration postfix runs. REPOLib custom maps are then identified through the exact objects in `REPOLib.Modules.Levels.RegisteredLevels`. A playable level appended later by another loader and absent from the captured base pool is conservatively treated as modded.

The current `RunManager.levels` collection remains the source of truth for availability. Lobby and shop scenes are excluded. This automatically recognizes newly added official maps when they are part of the game's original serialized pool. The fallback for non-REPOLib loaders is necessarily registration-timing based because R.E.P.O. exposes no universal cross-loader ownership field.

## Configuration

The configuration file is `BepInEx/config/profiel.vanillaormodded.cfg`.

Host-authoritative gameplay options:

| Option | Default | Meaning |
| --- | ---: | --- |
| `VotingEnabled` | `true` | Enables voting for the lobby. |
| `VotingDuration` | `5` | Vote length in seconds, clamped to 2 through 60. |
| `EndEarlyWhenAllVoted` | `true` | Ends when every snapshotted compatible participant voted. |
| `VoteFirstMapOnNewSave` | `true` | Holds the first map of a new save until the vote ends. |
| `TieBreak` | `Random` | `Random` picks any tied category. `FavorNotLast` skips the category that won the previous vote when another tied option exists. A clear winner is never changed. |
| `PreventMapRepeats` | `true` | Enables map-history filtering. |
| `MapHistoryLength` | `1` | Number of recent maps to exclude, clamped to 0 through 20. |
| `ShowChosenMapName` | `false` | Reveals the selected map in the result notification when enabled. |
| `ModdedMapBlacklist` | empty | Removes matching custom maps. |
| `ModdedMapWhitelist` | empty | Limits custom maps when non-empty. Blacklist still wins. |

For the whitelist and blacklist, separate entries with commas, semicolons, or new lines. Use the Unity level identifier, such as `Level - Example`. The display form without `Level - ` also matches. Identifiers are compared case-insensitively. The chosen identifier is only written at normal log level when `ShowChosenMapName` is enabled; otherwise it is debug-only.

Client-local visual options:

| Option | Default | Meaning |
| --- | ---: | --- |
| `ShowCountdown` | `true` | Shows the remaining vote time in the vote window. |
| `UIScale` | `1.0` | Personal interface scale. |
| `UIOffsetX` | `0` | Personal horizontal offset. |
| `UIOffsetY` | `0` | Personal vertical offset. |

Client-local values never change lobby gameplay.

## MapVote conflict

When the popular `Patrick.MapVote` BepInEx plugin GUID is loaded, VanillaOrModded disables its own voting and map-selection functionality and shows a warning in the truck. This prevents both mods from competing over `RunManager.SetRunLevel`.

## Installation

Install through a R.E.P.O. Thunderstore-compatible mod manager. Manual installation requires BepInExPack, MenuLib 2.5.4, REPOLib 4.2.0, and `VanillaOrModded.dll` inside `BepInEx/plugins/VanillaOrModded/`.

## Building from source

Install the .NET 8 SDK, place MenuLib 2.5.4 at `lib/MenuLib.dll` and REPOLib 4.2.0 at `lib/REPOLib.dll`, then run:

```powershell
dotnet restore
dotnet build -c Release
```

The project restores its publicized R.E.P.O. 0.4.4 and Unity 2022.3.62 compile-time references from NuGet. MenuLib and REPOLib remain external runtime dependencies and are not bundled into the release DLL.

## First map of a new save

R.E.P.O. commits the first map when the host leaves the lobby menu, before the truck HUD exists. VanillaOrModded holds that one `RunManager.ChangeLevel` call, runs the vote, then replays the call so the winning map is applied. A safety timeout always releases the call, so a failed vote cannot block the game.

This applies only when the level counter is zero and the current scene is the lobby menu. Saves with completed levels behave as before. Set `VoteFirstMapOnNewSave` to `false` to turn it off.

**Status:** this hook point was chosen from the game's method signatures and has not been confirmed in game. See `VERIFICATION.md`.

## Tests

The pure vote logic in `Core/` has an executable test project. It needs only the .NET 8 SDK:

```powershell
cd tests
dotnet run
```

## Dependencies

- **BepInExPack 5.4.2305** provides the mod loader.
- **MenuLib 2.5.4** provides UI components matching R.E.P.O.'s visual style.
- **REPOLib 4.2.0** provides authoritative custom-level registration metadata used for map classification.
