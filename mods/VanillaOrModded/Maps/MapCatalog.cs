using System;
using System.Collections.Generic;
using System.Linq;
using REPOLib.Modules;

namespace VanillaOrModded.Maps;

internal static class MapCatalog
{
    internal const string LobbyLevelName = "Level - Lobby";
    internal const string ShopLevelName = "Level - Shop";

    // Captured in a first-priority RunManager.Awake prefix, before REPOLib's
    // last-priority postfix appends queued custom levels.
    private static readonly HashSet<Level> InitialGameLevels = new();

    internal static void CaptureInitialGameLevels(RunManager runManager)
    {
        InitialGameLevels.Clear();
        if (runManager.levels == null)
        {
            Plugin.Logger.LogWarning("RunManager.levels was null while capturing the base-game level set.");
            return;
        }

        foreach (Level level in runManager.levels.Where(level => level != null))
        {
            InitialGameLevels.Add(level);
        }

        Plugin.Logger.LogDebug($"Captured {InitialGameLevels.Count} base-game level objects before custom registration.");
    }

    internal static IReadOnlyList<Level> GetLevels(RunManager runManager, MapCategory category)
    {
        if (runManager.levels == null)
        {
            return Array.Empty<Level>();
        }

        HashSet<Level> repoLibLevels = new(Levels.RegisteredLevels);
        HashSet<string> whitelist = ParseIdentifiers(Plugin.Settings.ModdedMapWhitelist.Value);
        HashSet<string> blacklist = ParseIdentifiers(Plugin.Settings.ModdedMapBlacklist.Value);

        IEnumerable<Level> vanilla = runManager.levels
            .Where(IsPlayableMap)
            .Where(level => !IsModded(level, repoLibLevels));

        IEnumerable<Level> modded = runManager.levels
            .Where(IsPlayableMap)
            .Where(level => IsModded(level, repoLibLevels))
            .Where(level => IsAllowedModdedMap(level, whitelist, blacklist));

        IEnumerable<Level> selected = category switch
        {
            MapCategory.Vanilla => vanilla,
            MapCategory.Modded => modded,
            MapCategory.Random => vanilla.Concat(modded),
            _ => Array.Empty<Level>()
        };

        return selected.Distinct().ToList();
    }

    internal static bool IsPlayableMap(Level? level)
    {
        return level != null &&
               !string.Equals(level.name, LobbyLevelName, StringComparison.Ordinal) &&
               !string.Equals(level.name, ShopLevelName, StringComparison.Ordinal);
    }

    internal static string DisplayName(Level level)
    {
        const string prefix = "Level - ";
        return level.name.StartsWith(prefix, StringComparison.Ordinal)
            ? level.name.Substring(prefix.Length)
            : level.name;
    }

    private static bool IsModded(Level level, HashSet<Level> repoLibLevels)
    {
        if (repoLibLevels.Contains(level))
        {
            return true;
        }

        if (InitialGameLevels.Contains(level))
        {
            return false;
        }

        // Fallback for legacy/custom loaders: a playable object appended after
        // the original serialized pool is treated as custom.
        return true;
    }

    private static bool IsAllowedModdedMap(Level level, HashSet<string> whitelist, HashSet<string> blacklist)
    {
        bool whitelisted = whitelist.Count == 0 || Matches(level, whitelist);
        bool blacklisted = Matches(level, blacklist);
        return whitelisted && !blacklisted;
    }

    private static bool Matches(Level level, HashSet<string> identifiers)
    {
        return identifiers.Contains(level.name) || identifiers.Contains(DisplayName(level));
    }

    private static HashSet<string> ParseIdentifiers(string value)
    {
        return value
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
