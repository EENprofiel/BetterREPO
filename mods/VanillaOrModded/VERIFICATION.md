# VanillaOrModded 1.0.0 verification

Verification performed on 2026-09-10.

## Build

- Compiled as a `netstandard2.1` PE32 Mono/.NET assembly with the .NET 8.0.425 Roslyn compiler.
- Compiled against `R.E.P.O.GameLibs.Steam` 0.4.4-ngd.0, Unity 2022.3.62, BepInEx 5.4.21, HarmonyX 2.7.0, MenuLib 2.5.4, and REPOLib 4.2.0.
- BepInEx analyzers enabled.
- Nullable analysis enabled.
- Compiler warnings treated as errors.
- Final build: 0 warnings, 0 errors.
- DLL SHA-256: `c91a18ffc8f2a54a5afa524d5def629cc2ae4b6e32a76f823a346990608a81bf`

No game stubs were used.

## Automated core checks

The standalone core harness completed 12 checks:

1. Solo vote acceptance and all-voted completion.
2. Four-player 3-to-1 Modded win.
3. Vote change removes one old tally and adds one new tally.
4. Tie RNG receives only the tied top categories.
5. Zero votes resolves without a winner or RNG call.
6. Disconnect removes eligibility and the disconnected actor's vote.
7. A non-snapshotted late joiner cannot vote.
8. Previous-map exclusion occurs before selection.
9. A sole excluded candidate is restored safely.
10. History length two excludes both recent maps.
11. RANDOM is represented as one combined per-map pool.
12. A post-resolution vote is rejected.

Result: `PASS: 12 core acceptance tests`

## Acceptance-scenario review

| Scenario | Verification |
| --- | --- |
| Solo | Automated vote/session check; host-only selection path compiles against game API. |
| Four players | Automated 1 Vanilla / 3 Modded resolution. |
| Vote change | Automated exact count movement. |
| Tie | Automated tied-top-only selection. |
| Random | Static implementation review confirms `vanilla.Concat(modded)` before one draw; combined-pool invariant checked by harness. |
| Repeat protection | Automated pre-draw history filtering. |
| Only one map | Automated fallback to original pool. |
| Disconnect | Automated session mutation; host runtime polling compiles against Photon room actor APIs. |
| Late join | Automated rejection outside snapshotted eligibility. |
| Unmodded client | Protocol sends optional Photon events only; eligibility comes exclusively from compatible handshakes. No BepInEx dependency is declared as everyone-required. |
| No votes | Automated null winner; coordinator clears pending override and leaves `SetRunLevel` untouched. |
| MapVote installed | Exact `Patrick.MapVote` loaded-plugin GUID check disables coordinator and selection patch behavior. |
| Solo repeat | Covered by previous-map filter and sole-map fallback checks. |
| History 2 | Automated X/Y exclusion leaving Z. |
| UI | Source review confirms centered heading/timer, hard-left labels, right-anchored green rectangles, selected-row border, red final 1.25 seconds, and no identity text. |

## Static safety review

- Every gameplay message carries magic marker, protocol version, message type, and session ID where applicable.
- Host validates Photon sender, current room membership, session state, session ID, eligibility, and option range.
- Clients accept authoritative state only from the current/snapshotted master client.
- Aggregate totals are broadcast; the vote dictionary is never sent to clients.
- Host selection is queued once and applied only through the existing `RunManager.SetRunLevel` commit hook.
- RANDOM draws from the combined map list, not from a category coin flip.
- History removes candidates before drawing and restores the original pool only when necessary.
- History is cleared when the multiplayer room is left.
- MapVote is declared as a soft dependency, so the loaded-plugin conflict check runs after MapVote initialization.
- Selected map identifiers are debug-only unless `ShowChosenMapName` is enabled.
- Client capability preparation uses a one-second response window.
- Clients reject stale or equal session starts using a state-independent session watermark.
- Host migration resets that watermark for the new authoritative master.
- Source, metadata, config identity, release documents, and DLL strings were scanned for the retired private branding with no matches.

## Runtime validation still recommended

R.E.P.O. itself is not installed in the build container, so an in-game host plus client pass is still recommended before publication. In particular, visually inspect the popup at multiple resolutions and complete one vanilla and one modded transition. This is not represented as completed gameplay testing.
