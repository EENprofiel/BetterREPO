# Better Revive Health

Small BepInEx plugin for R.E.P.O. that changes the health assigned by the
normal extraction-point revival mechanic.

## Configuration

After the first launch, edit `BepInEx/config/com.betterrevivehealth.plugin.cfg`:

```ini
[Revive]

# Amount of health players receive when revived after an extraction.
# Clamped to the player's maximum health.
ReviveHealth = 20
```

The accepted configuration range is 1 to 10000. The applied value is always
clamped again to the revived player's current `maxHealth`, so health upgrades
and max-health changes from other mods are respected.

## Exact patch and scope

The extraction point itself advances through `ExtractionPoint.StateSet` when it
reaches its completed state. The mod intentionally does not patch that state
transition. The actual per-player post-revival health boundary is the verified
R.E.P.O. method
`PlayerAvatar.ReviveRPC(bool _revivedByTruck)` with a Harmony postfix.
Normal extraction-point revival calls this RPC with `_revivedByTruck = false`.
Truck revival calls it with `true` and is intentionally skipped.

The postfix then:

1. runs only on the host (or in singleplayer);
2. reads the revived object's real `PlayerHealth.health` and
   `PlayerHealth.maxHealth` values;
3. calculates `min(ReviveHealth, maxHealth)`; and
4. adds only the missing amount through the game's existing
   `PlayerHealth.HealOther(int, bool)` networked health method.

It does not patch general `SetHealth`, `Heal`, medkits, extraction state,
maximum health, revival requirements, or the timing of revival. The current
health check makes the operation idempotent, so a duplicate delivery cannot
continually reset a revived player.

The host check uses R.E.P.O.'s `SemiFunc.IsMasterClientOrSingleplayer()`
helper, with a fail-closed Photon master-client fallback if that helper is not
available. Because the host is the only process that applies the change and
`HealOther(..., true)` is the game's own networked health path, the host's
configuration determines the result for remote players. Clients do not need a
different configuration and cannot apply a client-only value. Clients may
still install the plugin for convenience, but their postfix exits before
changing health.

If a future game build reuses `ReviveRPC(false)` for another revival system,
that system shares the same vanilla path and cannot be separated from the
extraction revival using the current method signature. The patch deliberately
does not broaden itself to unrelated health or respawn methods.

## Build

The project intentionally does not reference the game's `Assembly-CSharp.dll`
at compile time. It validates the discovered game types and signatures when
the plugin starts. The build uses the standard BepInEx 5 core assemblies and
the game's `UnityEngine.dll` for the `BaseUnityPlugin` base type.

From a shell with the .NET SDK installed:

```powershell
dotnet build BetterReviveHealth.csproj -c Release `
  -p:BepInExDir="C:\Program Files (x86)\Steam\steamapps\common\REPO\BepInEx"
```

The build output is `bin/Release/BetterReviveHealth.dll`. A Thunderstore-ready
layout is also prepared under `dist/`:

```text
dist/
├── BepInEx/
│   └── plugins/
│       └── BetterReviveHealth/
│           └── BetterReviveHealth.dll
├── README.md
└── manifest.json
```

Install the DLL in `BepInEx/plugins/BetterReviveHealth/` on the host. The
manifest declares BepInExPack 5.4.2305 as the package dependency.
