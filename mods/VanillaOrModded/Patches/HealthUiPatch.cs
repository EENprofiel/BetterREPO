using HarmonyLib;
using VanillaOrModded.Maps;
using VanillaOrModded.Networking;
using VanillaOrModded.Selection;
using VanillaOrModded.UI;

namespace VanillaOrModded.Patches;

[HarmonyPatch(typeof(HealthUI), nameof(HealthUI.Start))]
internal static class HealthUiPatch
{
    [HarmonyPostfix]
    private static void OnHealthUiStarted()
    {
        Level? current = RunManager.instance?.levelCurrent;
        if (current == null)
        {
            return;
        }

        if (Plugin.DisabledByConflict)
        {
            if (current.name == MapCatalog.LobbyLevelName)
            {
                ResultHud.Show("VANILLAORMODDED DISABLED\nMAPVOTE IS INSTALLED", 8f);
            }
            return;
        }

        SelectionController.RecordPlayedLevel(current);
        if (current.name == MapCatalog.LobbyLevelName)
        {
            VoteCoordinator.OnTruckHudReady();
        }
    }
}
