using System;

namespace TruckEnergyDisplay;

internal static class SnapshotProtocol
{
    internal const string Marker = "com.lucasdoddema.truckenergydisplay/v2";
    internal static object[] Encode(string nonce, int level, EnergySnapshot s) => new object[] {
        Marker, "snapshot", nonce, level, s.HasEnergy, s.Current, s.Maximum, s.HasPrediction,
        s.Buying, s.Needed, s.Projected, s.StillNeeded, s.Extra, s.Affordable,
        s.Status.Length > 160 ? s.Status.Substring(0, 160) : s.Status
    };
    internal static bool Decode(object? payload, int sender, int master, string expectedNonce,
        int expectedLevel, out EnergySnapshot snapshot)
    {
        snapshot = new EnergySnapshot();
        if (master <= 0 || sender != master || expectedNonce.Length != 32 || payload is not object[] a ||
            a.Length != 15 || !Equals(a[0], Marker) || !Equals(a[1], "snapshot") ||
            !Equals(a[2], expectedNonce) || a[3] is not int level || level != expectedLevel ||
            a[4] is not bool hasEnergy || a[5] is not double current || a[6] is not double maximum ||
            a[7] is not bool prediction || a[8] is not int buying || a[9] is not int needed ||
            a[10] is not double projected || a[11] is not int still || a[12] is not int extra ||
            a[13] is not bool affordable || a[14] is not string status || status.Length > 160) return false;
        snapshot = new EnergySnapshot {
            HasEnergy = hasEnergy, Current = current, Maximum = maximum, HasPrediction = prediction,
            Buying = buying, Needed = needed, Projected = projected, StillNeeded = still,
            Extra = extra, Affordable = affordable, Status = status
        };
        return snapshot.Valid();
    }
}
