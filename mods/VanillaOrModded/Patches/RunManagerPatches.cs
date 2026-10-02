using HarmonyLib;
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
}
