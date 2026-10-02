using System;
using System.Linq;
using VanillaOrModded.Maps;

namespace VanillaOrModded.Selection;

internal static class SelectionController
{
    private static readonly MapSelectionService Selector = new();
    private static readonly MapHistory History = new();

    private static Level? _pendingLevel;
    private static string? _pendingLevelName;

    internal static void Initialize()
    {
        _pendingLevel = null;
        _pendingLevelName = null;
        History.Clear();
    }

    internal static void ResetForLobby()
    {
        _pendingLevel = null;
        _pendingLevelName = null;
        History.Clear();
    }

    internal static Level? ChooseAndQueue(MapCategory category)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer())
        {
            Plugin.Logger.LogWarning("Ignored a map-selection request on a non-host client.");
            return null;
        }

        RunManager? runManager = RunManager.instance;
        if (runManager == null)
        {
            Plugin.Logger.LogError("Cannot select a map because RunManager.instance is null.");
            return null;
        }

        Level? selected = Selector.Choose(runManager, category, History.NewestFirst);
        if (selected == null)
        {
            Plugin.Logger.LogWarning($"No eligible {category.ToString().ToLowerInvariant()} maps are available. The game's existing selection will be preserved.");
            ClearPendingSelection();
            return null;
        }

        _pendingLevel = selected;
        _pendingLevelName = selected.name;
        if (Plugin.Settings.ShowChosenMapName.Value)
        {
            Plugin.Logger.LogInfo($"Winning category: {category}. Queued map identifier: '{selected.name}'.");
        }
        else
        {
            Plugin.Logger.LogInfo($"Winning category: {category}. A playable map was queued.");
            Plugin.Logger.LogDebug($"Queued map identifier: '{selected.name}'.");
        }
        return selected;
    }

    internal static bool TryApplyPendingSelection(RunManager runManager)
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer() || _pendingLevel == null)
        {
            return false;
        }

        Level? chosen = runManager.levels?.FirstOrDefault(level => ReferenceEquals(level, _pendingLevel));
        chosen ??= runManager.levels?.FirstOrDefault(level => level != null && level.name == _pendingLevelName);

        if (chosen == null)
        {
            Plugin.Logger.LogWarning("The queued level is no longer registered. The game's existing selection will be preserved.");
            ClearPendingSelection();
            return false;
        }

        // Only replace the level assignment at the game's normal commit point.
        // R.E.P.O. remains responsible for the transition and synchronization.
        runManager.levelCurrent = chosen;
        ClearPendingSelection();
        if (Plugin.Settings.ShowChosenMapName.Value)
        {
            Plugin.Logger.LogInfo($"Applied queued next map '{chosen.name}' on the authoritative host.");
        }
        else
        {
            Plugin.Logger.LogDebug($"Applied queued next map '{chosen.name}' on the authoritative host.");
        }
        return true;
    }

    internal static void RecordPlayedLevel(Level? level)
    {
        History.Record(level);
    }

    internal static void ClearPendingSelection()
    {
        _pendingLevel = null;
        _pendingLevelName = null;
    }
}
