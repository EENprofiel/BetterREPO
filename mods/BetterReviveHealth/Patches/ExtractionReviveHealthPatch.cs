using System;
using System.Reflection;
using HarmonyLib;

namespace BetterReviveHealth
{
    /// <summary>
    /// Patches only the post-revival RPC used by the game's normal revive path.
    /// All game-specific types are resolved and checked at startup.
    /// </summary>
    internal static class ExtractionReviveHealthPatch
    {
        private static readonly BindingFlags StaticFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo _playerHealthField;
        private static FieldInfo _healthField;
        private static FieldInfo _maxHealthField;
        private static MethodInfo _healOtherMethod;
        private static MethodInfo _masterClientOrSingleplayerMethod;
        private static PropertyInfo _photonInRoomProperty;
        private static PropertyInfo _photonIsMasterClientProperty;
        private static bool _authorityWarningShown;

        internal static bool TryInstall(Harmony harmony)
        {
            var playerAvatarType = AccessTools.TypeByName("PlayerAvatar");
            var playerHealthType = AccessTools.TypeByName("PlayerHealth");
            if (playerAvatarType == null || playerHealthType == null)
            {
                PluginLog.LogError("PlayerAvatar or PlayerHealth was not found in the loaded game assemblies.");
                return false;
            }

            // The bool signature is intentional. ReviveRPC(bool _revivedByTruck)
            // is the verified post-revival RPC used by the current game.
            var reviveRpc = AccessTools.Method(playerAvatarType, "ReviveRPC", new[] { typeof(bool) });
            if (reviveRpc == null)
            {
                PluginLog.LogError("PlayerAvatar.ReviveRPC(bool) was not found.");
                return false;
            }

            _playerHealthField = AccessTools.Field(playerAvatarType, "playerHealth");
            _healthField = AccessTools.Field(playerHealthType, "health");
            _maxHealthField = AccessTools.Field(playerHealthType, "maxHealth");
            _healOtherMethod = AccessTools.Method(playerHealthType, "HealOther", new[] { typeof(int), typeof(bool) });

            if (_playerHealthField == null || _healthField == null || _maxHealthField == null || _healOtherMethod == null)
            {
                PluginLog.LogError(
                    "The verified PlayerHealth members (playerHealth, health, maxHealth, HealOther(int, bool)) could not be resolved.");
                return false;
            }

            ResolveAuthorityChecks();

            harmony.Patch(
                reviveRpc,
                postfix: new HarmonyMethod(typeof(ExtractionReviveHealthPatch), nameof(ReviveRpcPostfix)));

            return true;
        }

        private static void ReviveRpcPostfix(object __instance, bool __0)
        {
            // Truck revivals are intentionally excluded. In the vanilla path,
            // extraction-point revivals call ReviveRPC(false).
            if (__0 || __instance == null || !IsHostOrSingleplayer())
            {
                return;
            }

            try
            {
                var playerHealth = _playerHealthField.GetValue(__instance);
                if (playerHealth == null)
                {
                    return;
                }

                var maxHealth = ReadInt(_maxHealthField, playerHealth);
                if (maxHealth <= 0)
                {
                    return;
                }

                var configuredHealth = Plugin.GetConfiguredReviveHealth();
                var targetHealth = Math.Min(configuredHealth, maxHealth);
                var oldHealth = ReadInt(_healthField, playerHealth);

                // This also makes the patch idempotent if the same RPC is
                // delivered more than once. Never lower a larger health value
                // provided by the game or another compatible mod.
                if (oldHealth >= targetHealth)
                {
                    return;
                }

                var healthToAdd = targetHealth - oldHealth;
                _healOtherMethod.Invoke(playerHealth, new object[] { healthToAdd, true });
            }
            catch (Exception exception)
            {
                PluginLog.LogError($"Failed to apply extraction revive health: {exception.Message}");
            }
        }

        private static int ReadInt(FieldInfo field, object instance)
        {
            return Convert.ToInt32(field.GetValue(instance));
        }

        private static bool IsHostOrSingleplayer()
        {
            try
            {
                if (_masterClientOrSingleplayerMethod != null)
                {
                    return Convert.ToBoolean(_masterClientOrSingleplayerMethod.Invoke(null, null));
                }

                // Fail closed if the game's helper is unavailable. The
                // fallback is still resolved dynamically so clients cannot
                // accidentally apply their own local config.
                if (_photonInRoomProperty != null && _photonIsMasterClientProperty != null)
                {
                    var inRoom = Convert.ToBoolean(_photonInRoomProperty.GetValue(null, null));
                    return !inRoom || Convert.ToBoolean(_photonIsMasterClientProperty.GetValue(null, null));
                }
            }
            catch (Exception exception)
            {
                if (!_authorityWarningShown)
                {
                    _authorityWarningShown = true;
                    PluginLog.LogWarning($"Host-authority check failed; revive health changes are disabled: {exception.Message}");
                }
            }

            if (!_authorityWarningShown)
            {
                _authorityWarningShown = true;
                PluginLog.LogWarning("No host-authority check was available; revive health changes are disabled.");
            }

            return false;
        }

        private static void ResolveAuthorityChecks()
        {
            var semiFuncType = AccessTools.TypeByName("SemiFunc");
            _masterClientOrSingleplayerMethod = semiFuncType == null
                ? null
                : AccessTools.Method(semiFuncType, "IsMasterClientOrSingleplayer");

            if (_masterClientOrSingleplayerMethod != null)
            {
                return;
            }

            var photonNetworkType = AccessTools.TypeByName("Photon.Pun.PhotonNetwork")
                                    ?? AccessTools.TypeByName("PhotonNetwork");
            if (photonNetworkType == null)
            {
                return;
            }

            _photonInRoomProperty = photonNetworkType.GetProperty("InRoom", StaticFlags);
            _photonIsMasterClientProperty = photonNetworkType.GetProperty("IsMasterClient", StaticFlags);
        }

        private static BepInEx.Logging.ManualLogSource PluginLog
        {
            get { return Plugin.Log; }
        }
    }
}
