using System;

namespace TruckEnergyDisplay;

/// <summary>Immutable display data. No game state is stored or modified here.</summary>
public sealed class EnergySnapshot
{
    public double Current { get; set; }
    public double Maximum { get; set; }
    public bool HasEnergy { get; set; }
    public bool HasPrediction { get; set; }
    public int Buying { get; set; }
    public int Needed { get; set; }
    public int StillNeeded { get; set; }
    public int Extra { get; set; }
    public double Projected { get; set; }
    public bool Affordable { get; set; } = true;
    public string Status { get; set; } = "Waiting for truck data";

    internal bool Valid() => Status != null && Status.Length <= 160 &&
        EnergyMath.Finite(Current) && EnergyMath.Finite(Maximum) && EnergyMath.Finite(Projected) &&
        (!HasEnergy || (Current >= 0 && Maximum > 0 && Current <= Maximum)) &&
        Buying >= 0 && Buying <= EnergyMath.Limit &&
        (!HasPrediction || (HasEnergy && EnergyMath.Finite(Projected) && Projected >= 0 &&
        Projected <= Maximum && Needed >= 0 && Needed <= EnergyMath.Limit &&
        StillNeeded >= 0 && StillNeeded <= EnergyMath.Limit && Extra >= 0 && Extra <= Buying));
}

internal static class EnergyMath
{
    // Computational safety bound, not a game capacity or crystal value.
    internal const int Limit = 10000;
    internal static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    internal static bool Full(double x, double max) => x >= max;

    internal static double Project(double current, double maximum, double increment, int owned,
        int buying, Func<int, double> ownershipCap)
    {
        if (!Finite(current) || !Finite(maximum) || !Finite(increment) || current < 0 ||
            maximum <= 0 || increment <= 0 || buying < 0 || owned < 0)
            throw new InvalidOperationException("Invalid energy model");
        // Both verified purchase routines add an integer increment and clamp after each item.
        // For positive increments this equals a single addition followed by a clamp.
        double saved = Math.Min(maximum, current + buying * increment);
        double cap = ownershipCap(checked(owned + buying));
        if (!Finite(cap) || cap < 0) throw new InvalidOperationException("Invalid ownership cap");
        return Math.Max(0, Math.Min(saved, cap));
    }

    internal static int Required(double maximum, Func<int, double> project)
    {
        // Reproduce the loading limit too; ceil(missing / increment) alone can underbuy.
        // A bounded scan avoids assuming that a modded ownership formula is continuous.
        for (int n = 0; n <= Limit; n++)
            if (Full(project(n), maximum)) return n;
        throw new InvalidOperationException("Full charge is not reachable within the prediction limit");
    }

    internal static EnergySnapshot Calculate(double current, double maximum, double increment,
        int owned, int buying, Func<int, double> ownershipCap, bool affordable)
    {
        Func<int, double> project = n => Project(current, maximum, increment, owned, n, ownershipCap);
        int need = Required(maximum, project);
        var result = new EnergySnapshot {
            Current = current, Maximum = maximum, HasEnergy = true, HasPrediction = true,
            Buying = buying, Needed = need, Projected = project(buying), Affordable = affordable,
            StillNeeded = Required(maximum, n => project(checked(buying + n))),
            Extra = 0, Status = "After checkout and truck loading"
        };
        // Extra only has a precise meaning for identical crystals and a monotone projection.
        bool monotone = true;
        double previous = project(0);
        for (int n = 1; n <= buying; n++) {
            double next = project(n);
            if (next < previous) monotone = false;
            previous = next;
        }
        if (monotone && Full(result.Projected, maximum)) result.Extra = Math.Max(0, buying - need);
        return result;
    }
}
