using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VanillaOrModded.Core;

namespace VanillaOrModded.Maps;

internal sealed class MapSelectionService
{
    internal Level? Choose(RunManager runManager, MapCategory category, IReadOnlyList<string> history)
    {
        List<Level> candidates = MapCatalog.GetLevels(runManager, category).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        candidates = HistoryFilter.Apply(
            candidates,
            history,
            level => level.name,
            Plugin.Settings.PreventMapRepeats.Value,
            Plugin.Settings.MapHistoryLength.Value);

        return candidates[Random.Range(0, candidates.Count)];
    }
}
