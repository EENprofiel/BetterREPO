using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using VanillaOrModded.Compatibility;
using VanillaOrModded.Configuration;
using VanillaOrModded.Networking;
using VanillaOrModded.Selection;
using VanillaOrModded.UI;

namespace VanillaOrModded;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("REPOLib", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("nickklmao.menulib", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("Patrick.MapVote", BepInDependency.DependencyFlags.SoftDependency)]
internal sealed class Plugin : BaseUnityPlugin
{
    internal const string PluginGuid = "profiel.vanillaormodded";
    internal const string PluginName = "VanillaOrModded";
    internal const string PluginVersion = "1.1.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal static new ManualLogSource Logger { get; private set; } = null!;
    internal static ModConfig Settings { get; private set; } = null!;
    internal static bool DisabledByConflict { get; private set; }

    private Harmony? _harmony;

    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;
        Settings = new ModConfig(Config);

        transform.parent = null;
        gameObject.hideFlags = HideFlags.HideAndDontSave;

        DisabledByConflict = MapVoteCompatibility.TryFindConflict(out string conflictingGuid);
        if (DisabledByConflict)
        {
            Logger.LogWarning($"VanillaOrModded voting is disabled because MapVote is loaded ({conflictingGuid}). Disable one voting mod to use VanillaOrModded.");
        }
        else
        {
            SelectionController.Initialize();
            VoteCoordinator.Initialize();
        }

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded{(DisabledByConflict ? " (voting disabled: MapVote conflict)" : string.Empty)}.");
    }

    private void Update()
    {
        if (DisabledByConflict)
        {
            return;
        }

        VoteCoordinator.Tick();
        VoteUi.TickInput();
    }

    private void OnDestroy()
    {
        if (!DisabledByConflict)
        {
            VoteCoordinator.Shutdown();
        }

        VoteUi.Close();
        ResultHud.Clear();
        _harmony?.UnpatchSelf();
    }
}
