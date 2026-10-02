# Technical notes

## Networking architecture

VanillaOrModded uses one reliable Photon custom event code with a namespaced envelope. Every message contains the magic string `VanillaOrModded`, protocol version `1`, a message type, and validated payload fields.

The host opens a one-second preparation window and probes peers. Compatible clients reply to the Photon master client. At the end of preparation, the host snapshots compatible actor numbers that are still present and starts a numbered session. Actor numbers are used only internally for validation; only aggregate category totals are broadcast or displayed.

Vote submission is always routed to the current master client. The host checks the sender supplied by Photon, active session ID, voting state, eligible snapshot, currently connected actor set, and enum range before replacing that sender's previous vote. A changed vote therefore moves exactly one count between categories. Totals are recomputed from the host dictionary.

Clients accept start, totals, and result messages only from the current authoritative Photon master for the active session. A master-client change cancels the local session safely. Disconnect polling removes an eligible actor and their vote on the host. Messages with another protocol version are ignored and the actor is excluded without affecting the lobby.

## Map classification and selection

The map catalog captures base-game `Level` object identity in a first-priority `RunManager.Awake` prefix. REPOLib registers queued custom levels in a last-priority postfix and exposes those exact objects through `Levels.RegisteredLevels`. Objects outside the initial set are classified as custom, including a registration-timing fallback for non-REPOLib loaders.

After a category wins, the host builds one explicit candidate list. RANDOM concatenates vanilla and modded maps before selection, so every map has one entry in the same draw. Modded whitelist and blacklist filters are applied before history filtering. Blacklist wins over whitelist.

The selected object is queued and applied only in the existing `RunManager.SetRunLevel` prefix on the host. The mod assigns `levelCurrent` and lets R.E.P.O.'s normal transition and networking continue.

## Repeat prevention

Played map identifiers are stored newest first. Selection removes the configured number of recent identifiers from the candidate list, then performs one host-side random draw. If the filtered list is empty, it restores the original list. There is no retry/reroll loop.

## State lifecycle

`Idle -> Preparing -> Voting -> Resolving -> Result -> Idle`

Every session clears eligibility and votes, uses a new positive session ID, resolves at most once, and rejects submissions outside `Voting`. Clients retain the highest accepted session ID and reject older or equal `VoteStart` messages from the same master in every UI state. A host migration resets that per-master watermark, and it is also reset when the multiplayer room is left. The result notification is non-blocking and does not delay the game's transition.

Totals and result messages also require the sender to still be the current Photon master, closing the small packet-ordering window during host migration.
