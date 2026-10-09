using HarmonyLib;
using VanillaOrModded.Networking;
using VanillaOrModded.Maps;
using VanillaOrModded.Selection;

namespace VanillaOrModded.Patches;

[HarmonyPatch(typeof(RunManager))]
internal static class RunManagerPatches
{
    [HarmonyPatch(nameof(RunManager.Awake))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void CaptureInitialLevels(RunManager __instance)
    {
        if (!Plugin.DisabledByConflict)
        {
            MapCatalog.CaptureInitialGameLevels(__instance);
        }
    }

    [HarmonyPatch(nameof(RunManager.SetRunLevel))]
    [HarmonyPrefix]
    private static bool OverrideQueuedLevel(RunManager __instance)
    {
        if (Plugin.DisabledByConflict)
        {
            return true;
        }

        // Returning false mirrors SetRunLevel's completed outcome because the
        // authoritative host assignment has already been made.
        return !SelectionController.TryApplyPendingSelection(__instance);
    }

    private static bool _replaying;

    // The game commits the first map of a new save in ChangeLevel (via
    // SetRunLevel) when leaving the lobby menu, before the truck HUD exists.
    // Hold that one call, run the vote, then replay the call unchanged.
    [HarmonyPatch(nameof(RunManager.ChangeLevel))]
    [HarmonyPrefix]
    private static bool HoldFirstMapForVote(
        RunManager __instance,
        bool _completedLevel,
        bool _levelFailed,
        RunManager.ChangeLevelType _changeLevelType)
    {
        if (_replaying)
        {
            return true;
        }

        if (VoteCoordinator.IsFirstMapGatePending)
        {
            return false;
        }

        if (Plugin.DisabledByConflict ||
            !Plugin.Settings.VoteFirstMapOnNewSave.Value ||
            !Plugin.Settings.VotingEnabled.Value ||
            _completedLevel ||
            _levelFailed ||
            (_changeLevelType != RunManager.ChangeLevelType.Normal && _changeLevelType != RunManager.ChangeLevelType.RunLevel) ||
            !SemiFunc.IsMasterClientOrSingleplayer() ||
            __instance.levelCurrent == null ||
            __instance.levelCurrent != __instance.levelLobbyMenu ||
            __instance.levelsCompleted != 0)
        {
            return true;
        }

        bool started = VoteCoordinator.BeginFirstMapGate(() =>
        {
            _replaying = true;
            try
            {
                __instance.ChangeLevel(_completedLevel, _levelFailed, _changeLevelType);
            }
            finally
            {
                _replaying = false;
            }
        });

        if (started)
        {
            Plugin.Logger.LogInfo("Holding the first map of a new save until the vote ends.");
        }

        return !started;
    }
}
