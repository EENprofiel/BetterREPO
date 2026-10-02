using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace TruckEnergyDisplay;

internal sealed class Mechanics
{
    internal double Maximum;
    internal double Increment;
    internal CapExpression Cap = null!;
    internal string OwnedItemKey = "";
    internal MethodBase[] Methods = Array.Empty<MethodBase>();
}

internal static class MechanicsDiscovery
{
    internal const string ChargeKey = "chargingStationChargeTotal";
    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    internal static Mechanics Discover(Type station, Type extraction)
    {
        var start = station.GetMethod("Start", All) ?? throw new MissingMethodException("ChargingStation.Start");
        var one = extraction.GetMethod("DestroyTheFirstPhysObjectsInShopList", All) ?? throw new MissingMethodException("Single checkout routine");
        var all = extraction.GetMethod("DestroyAllPhysObjectsInShoppingList", All) ?? throw new MissingMethodException("Bulk checkout routine");
        var a = Purchase(IlReader.Read(one));
        var b = Purchase(IlReader.Read(all));
        if (a.increment != b.increment || a.maximum != b.maximum) throw new InvalidOperationException("Checkout routines disagree");
        var body = IlReader.Read(start);
        double scale = Normalization(body);
        if (scale != a.maximum) throw new InvalidOperationException("Checkout cap and station normalization disagree");
        var watched = new List<MethodBase> { start, one, all };
        foreach (string name in new[] { "Update", "ChargeAreaCheck", "CrystalsItShouldHave", "ChargingStationSegmentChangedRPC" }) {
            var m = station.GetMethod(name, All); if (m != null) watched.Add(m);
        }
        string? ownedKey = null;
        for (int i = 0; i + 2 < body.Count; i++)
            if (body[i].Op == OpCodes.Ldstr && body[i+1].Method("StatGetItemsPurchased") &&
                body[i+2].Op == OpCodes.Stfld && body[i+2].Field("ChargingStation", "chargeInt")) ownedKey = body[i].Value as string;
        if (string.IsNullOrEmpty(ownedKey)) throw new InvalidOperationException("Station crystal ownership key unavailable");
        return new Mechanics { Maximum = scale, Increment = a.increment, Cap = Capacity(body), OwnedItemKey = ownedKey!, Methods = watched.ToArray() };
    }

    private static (double increment, double maximum) Purchase(List<Il> il)
    {
        var additions = new List<double>();
        var limits = new List<double>();
        for (int i = 0; i + 4 < il.Count; i++) {
            // dictionary[key] = dictionary[key] + constant
            if (il[i].Text(ChargeKey) && il[i+1].Method("get_Item") &&
                il[i+2].Number(out double increment, out bool fp) && !fp && increment > 0 &&
                il[i+3].Op == OpCodes.Add && il[i+4].Method("set_Item")) additions.Add(increment);
            // if (dictionary[key] > max) dictionary[key] = max
            if (i + 9 < il.Count && il[i].Text(ChargeKey) && il[i+1].Method("get_Item") &&
                il[i+2].Number(out double max, out bool mf) && !mf && max > 0 &&
                (il[i+3].Op == OpCodes.Ble || il[i+3].Op == OpCodes.Ble_S) &&
                il[i+4].Field("StatsManager", "instance") && il[i+5].Field("StatsManager", "runStats") &&
                il[i+6].Text(ChargeKey) && il[i+7].Number(out double assigned, out _) && max == assigned &&
                il[i+8].Method("set_Item") && Convert.ToInt32(il[i+3].Value) == il[i+9].Offset) limits.Add(max);
        }
        if (additions.Count != 1 || limits.Count != 1)
            throw new InvalidOperationException("Unrecognized crystal checkout arithmetic");
        return (additions[0], limits[0]);
    }

    private static double Normalization(List<Il> il)
    {
        var matches = new List<double>();
        for (int i = 0; i + 4 < il.Count; i++)
            if (il[i].Op == OpCodes.Ldfld && il[i].Field("ChargingStation", "chargeTotal") &&
                il[i+1].Op == OpCodes.Conv_R4 && il[i+2].Number(out double max, out _) && max > 0 &&
                il[i+3].Op == OpCodes.Div && il[i+4].Op == OpCodes.Stfld && il[i+4].Field("ChargingStation", "chargeFloat")) matches.Add(max);
        if (matches.Count != 1) throw new InvalidOperationException("Unrecognized charge normalization");
        return matches[0];
    }

    private static CapExpression Capacity(List<Il> il)
    {
        // Match the actual minimum-of-saved-charge-and-owned-crystals assignment.
        for (int i = 0; i + 13 < il.Count; i++) {
            if (!il[i].StoreLocal || il[i+1].Op != OpCodes.Ldarg_0 ||
                !il[i+2].Field("StatsManager", "instance") || !il[i+3].Field("StatsManager", "runStats") ||
                !il[i+4].Text(ChargeKey) || !il[i+5].Method("get_Item") ||
                il[i+6].Op != OpCodes.Stfld || !il[i+6].Field("ChargingStation", "chargeTotal") ||
                il[i+7].Op != OpCodes.Ldarg_0 || !il[i+8].Field("ChargingStation", "chargeTotal") ||
                !il[i+9].LoadLocal || il[i+9].Local() != il[i].Local() ||
                (il[i+10].Op != OpCodes.Ble && il[i+10].Op != OpCodes.Ble_S) ||
                il[i+11].Op != OpCodes.Ldarg_0 || !il[i+12].LoadLocal || il[i+12].Local() != il[i].Local() ||
                il[i+13].Op != OpCodes.Stfld || !il[i+13].Field("ChargingStation", "chargeTotal") ||
                i+14 >= il.Count || Convert.ToInt32(il[i+10].Value) != il[i+14].Offset) continue;
            int p = i - 1;
            return CapExpression.Parse(il, ref p);
        }
        throw new InvalidOperationException("Unrecognized station loading capacity");
    }
}
