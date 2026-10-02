using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TruckEnergyDisplay;

/// <summary>Optional host-side adapter for mods that replace the game's charging algorithm.</summary>
public static class TruckEnergyCompatibility
{
    /// <summary>Called on Unity's main thread. Return a fresh, immutable-after-return snapshot, or null to use vanilla discovery.</summary>
    public static Func<ChargingStation, IReadOnlyList<ItemAttributes>, EnergySnapshot?>? Provider { get; set; }
}

internal sealed class GameEnergyReader
{
    private static readonly FieldInfo? ShoppingList = AccessTools.Field(typeof(ShopManager), "shoppingList");
    private static readonly FieldInfo? Price = AccessTools.Field(typeof(ItemAttributes), "value");
    private static readonly FieldInfo? RoomCheck = AccessTools.Field(typeof(ItemAttributes), "roomVolumeCheck");
    private static readonly FieldInfo? InZone = AccessTools.Field(typeof(RoomVolumeCheck), "inExtractionPoint");
    private readonly Dictionary<string, FieldInfo> _fields = new();
    private Mechanics? _mechanics;
    private string _discoveryError = "";
    private string _patchError = "";
    private string _lastWarning = "";
    private float _nextPatchCheck;
    private readonly List<ItemAttributes> _checkout = new();
    private readonly HashSet<int> _seen = new();

    internal void Reset() { _nextPatchCheck = 0; _checkout.Clear(); _seen.Clear(); }
    internal static bool InShop => RunManager.instance && SemiFunc.RunIsShop();
    internal static bool Ready => InShop && StatsManager.instance && ShopManager.instance &&
        ChargingStation.instance && GameDirector.instance && GameDirector.instance.currentState == GameDirector.gameState.Main;

    internal EnergySnapshot Read()
    {
        var result = new EnergySnapshot();
        if (!Ready) return result;
        try {
            var station = ChargingStation.instance;
            if (ShoppingList?.GetValue(ShopManager.instance) is not List<ItemAttributes> list || Price == null || RoomCheck == null || InZone == null)
                throw new InvalidOperationException("Checkout state unavailable");
            _checkout.Clear(); _seen.Clear();
            long totalPrice = 0;
            int buying = 0;
            bool customCrystal = false;
            foreach (var item in list) {
                if (!item || !item.item || !_seen.Add(item.GetInstanceID())) continue;
                // This is only a filter of the authoritative purchase list. No scene scan,
                // overlap query or invocation of ShopCheck/CheckSet is performed by this mod.
                object? check = RoomCheck.GetValue(item);
                if (check == null || !Equals(InZone.GetValue(check), true)) continue;
                _checkout.Add(item);
                totalPrice += Math.Max(0, Convert.ToInt32(Price.GetValue(item)));
                if (item.item.itemType != SemiFunc.itemType.power_crystal) continue;
                buying++;
                if (item.item != station.item) customCrystal = true;
            }
            if (buying > EnergyMath.Limit) throw new InvalidOperationException("Checkout is too large to predict");
            var provider = TruckEnergyCompatibility.Provider;
            if (provider != null) {
                var custom = provider(station, _checkout.AsReadOnly());
                if (custom != null) {
                    if (!custom.Valid()) throw new InvalidOperationException("Compatibility adapter returned invalid data");
                    return custom;
                }
            }
            if (_mechanics == null && _discoveryError.Length == 0) {
                try {
                    _mechanics = MechanicsDiscovery.Discover(typeof(ChargingStation), typeof(ExtractionPoint));
                    Plugin.Log.LogInfo($"Verified runtime charge model: checkout +{_mechanics.Increment}, maximum {_mechanics.Maximum}; owned-crystal loading expression extracted from IL.");
                } catch (Exception ex) { _discoveryError = ex.Message; }
            }
            result.Buying = buying;
            result.Affordable = totalPrice <= SemiFunc.StatGetRunCurrency();
            if (!StatsManager.instance.runStats.TryGetValue(MechanicsDiscovery.ChargeKey, out int current))
                throw new InvalidOperationException("Saved station energy unavailable");
            // In the shop Start/Update do not follow purchase changes. The run stat is the
            // authoritative reserve that checkout updates and the next station loads.
            result.Current = current;
            if (_mechanics == null) throw new InvalidOperationException(_discoveryError);
            result.Maximum = _mechanics.Maximum;
            result.HasEnergy = current >= 0 && current <= result.Maximum;
            if (!result.HasEnergy) throw new InvalidOperationException("Charge is outside the verified capacity");
            CheckPatches();
            if (_patchError.Length != 0) { result.HasEnergy = false; throw new InvalidOperationException(_patchError); }
            if (customCrystal) throw new InvalidOperationException("Custom crystal requires an adapter");
            if (!station.item || station.item.itemType != SemiFunc.itemType.power_crystal)
                throw new InvalidOperationException("Station crystal metadata unavailable");
            if (!StatsManager.instance.itemsPurchased.TryGetValue(_mechanics.OwnedItemKey, out int owned))
                throw new InvalidOperationException("Station crystal ownership unavailable");
            double cap(int count) => _mechanics.Cap.Evaluate(name => {
                if (name == "chargeInt") return count;
                if (!_fields.TryGetValue(name, out var field)) {
                    field = AccessTools.Field(typeof(ChargingStation), name) ?? throw new MissingFieldException(name);
                    _fields.Add(name, field);
                }
                return Convert.ToDouble(field.GetValue(station));
            });
            return EnergyMath.Calculate(current, _mechanics.Maximum, _mechanics.Increment, owned, buying, cap, result.Affordable);
        } catch (Exception ex) {
            result.HasPrediction = false;
            result.Status = ex.Message;
            if (_lastWarning != ex.Message) { _lastWarning = ex.Message; Plugin.Log.LogWarning("Prediction unavailable: " + ex.Message); }
            return result;
        }
    }

    private void CheckPatches()
    {
        if (Time.unscaledTime < _nextPatchCheck) return;
        _nextPatchCheck = Time.unscaledTime + 2;
        _patchError = "";
        var methods = new List<MethodBase>(_mechanics!.Methods);
        foreach (var pair in new[] { (typeof(StatsManager), "ItemPurchase"), (typeof(StatsManager), "GetItemPurchased"),
                     (typeof(SemiFunc), "StatGetItemsPurchased"), (typeof(SemiFunc), "OnSceneSwitch") }) {
            var method = AccessTools.Method(pair.Item1, pair.Item2); if (method != null) methods.Add(method);
        }
        foreach (var method in methods) {
            var patches = Harmony.GetPatchInfo(method);
            if (patches == null || patches.Owners.Count == 0) continue;
            _patchError = "Charging logic modified; adapter needed";
            // Do not execute third-party transpilers a second time just to make a prediction.
            Plugin.Log.LogDebug($"Prediction blocked by patches on {method.DeclaringType?.Name}.{method.Name}: {string.Join(", ", patches.Owners)}");
            break;
        }
    }
}
