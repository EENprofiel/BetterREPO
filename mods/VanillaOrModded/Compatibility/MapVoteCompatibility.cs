using System;
using BepInEx.Bootstrap;

namespace VanillaOrModded.Compatibility;

internal static class MapVoteCompatibility
{
    // The public Patrick-MapVote package currently exposes this BepInEx GUID.
    // Exact GUID matching avoids unreliable DLL filename heuristics.
    private static readonly string[] KnownMapVoteGuids =
    {
        "Patrick.MapVote"
    };

    internal static bool TryFindConflict(out string guid)
    {
        foreach (string knownGuid in KnownMapVoteGuids)
        {
            foreach (string loadedGuid in Chainloader.PluginInfos.Keys)
            {
                if (string.Equals(knownGuid, loadedGuid, StringComparison.OrdinalIgnoreCase))
                {
                    guid = loadedGuid;
                    return true;
                }
            }
        }

        guid = string.Empty;
        return false;
    }
}
