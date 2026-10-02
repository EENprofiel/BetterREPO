using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BetterReviveHealth
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.betterrevivehealth.plugin";
        internal const string PluginName = "Better Revive Health";
        internal const string PluginVersion = "1.0.0";

        internal const int MinimumReviveHealth = 1;
        internal const int MaximumReviveHealth = 10000;

        internal static ManualLogSource Log { get; private set; }
        internal static ConfigEntry<int> ReviveHealth { get; private set; }

        private void Awake()
        {
            Log = Logger;

            ReviveHealth = Config.Bind(
                "Revive",
                "ReviveHealth",
                20,
                new ConfigDescription(
                    "Amount of health players receive when revived after an extraction. Clamped to the player's maximum health.",
                    new AcceptableValueRange<int>(MinimumReviveHealth, MaximumReviveHealth)));

            NormalizeConfiguration();

            var harmony = new Harmony(PluginGuid);
            if (!ExtractionReviveHealthPatch.TryInstall(harmony))
            {
                Logger.LogError("[BetterReviveHealth] Could not install the verified PlayerAvatar.ReviveRPC patch. The mod will remain inactive.");
                return;
            }

            Logger.LogInfo($"[BetterReviveHealth] Loaded. ReviveHealth = {ReviveHealth.Value}");
        }

        internal static int GetConfiguredReviveHealth()
        {
            return Clamp(ReviveHealth.Value, MinimumReviveHealth, MaximumReviveHealth);
        }

        private static void NormalizeConfiguration()
        {
            var normalized = GetConfiguredReviveHealth();
            if (normalized != ReviveHealth.Value)
            {
                ReviveHealth.Value = normalized;
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Min(Math.Max(value, minimum), maximum);
        }
    }
}
