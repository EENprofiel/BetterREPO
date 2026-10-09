using BepInEx.Configuration;
using VanillaOrModded.Core;

namespace VanillaOrModded.Configuration;

internal sealed class ModConfig
{
    internal ConfigEntry<bool> VotingEnabled { get; }
    internal ConfigEntry<float> VotingDuration { get; }
    internal ConfigEntry<bool> EndEarlyWhenAllVoted { get; }
    internal ConfigEntry<bool> VoteFirstMapOnNewSave { get; }
    internal ConfigEntry<TieBreakMode> TieBreak { get; }
    internal ConfigEntry<bool> PreventMapRepeats { get; }
    internal ConfigEntry<int> MapHistoryLength { get; }
    internal ConfigEntry<bool> ShowChosenMapName { get; }
    internal ConfigEntry<string> ModdedMapBlacklist { get; }
    internal ConfigEntry<string> ModdedMapWhitelist { get; }

    internal ConfigEntry<bool> ShowCountdown { get; }
    internal ConfigEntry<float> UiScale { get; }
    internal ConfigEntry<float> UiOffsetX { get; }
    internal ConfigEntry<float> UiOffsetY { get; }

    internal ModConfig(ConfigFile config)
    {
        VotingEnabled = config.Bind(
            "Gameplay (Host)", "VotingEnabled", true,
            "Enable map-type voting. Only the host's value affects the lobby.");

        VotingDuration = config.Bind(
            "Gameplay (Host)", "VotingDuration", 5f,
            new ConfigDescription(
                "Voting duration in seconds. Only the host's value affects the lobby.",
                new AcceptableValueRange<float>(VoteRules.MinDurationSeconds, VoteRules.MaxDurationSeconds)));

        EndEarlyWhenAllVoted = config.Bind(
            "Gameplay (Host)", "EndEarlyWhenAllVoted", true,
            "Finish as soon as every eligible compatible participant has voted.");

        VoteFirstMapOnNewSave = config.Bind(
            "Gameplay (Host)", "VoteFirstMapOnNewSave", true,
            "Hold the first map of a new save until the vote ends. Set to false to restore the old behavior if this causes problems. Only the host's value affects the lobby.");

        TieBreak = config.Bind(
            "Gameplay (Host)", "TieBreak", TieBreakMode.Random,
            "How tied top categories are resolved. Random picks any tied option. FavorNotLast avoids the category that won the previous vote when another tied option exists.");

        PreventMapRepeats = config.Bind(
            "Gameplay (Host)", "PreventMapRepeats", true,
            "Exclude recently played maps when at least one other valid candidate remains.");

        MapHistoryLength = config.Bind(
            "Gameplay (Host)", "MapHistoryLength", 1,
            new ConfigDescription(
                "Number of recently played maps to exclude. Set to 0 to disable history filtering.",
                new AcceptableValueRange<int>(0, 20)));

        ShowChosenMapName = config.Bind(
            "Gameplay (Host)", "ShowChosenMapName", false,
            "Reveal the randomly chosen map in the result message. The default keeps it a surprise.");

        ModdedMapBlacklist = config.Bind(
            "Gameplay (Host)", "ModdedMapBlacklist", string.Empty,
            "Comma, semicolon, or newline-separated modded map identifiers to exclude. Use the Unity level name shown in logs, for example 'Level - Example'. Display names without 'Level - ' also match.");

        ModdedMapWhitelist = config.Bind(
            "Gameplay (Host)", "ModdedMapWhitelist", string.Empty,
            "Optional comma, semicolon, or newline-separated modded map identifiers to allow. Empty allows every otherwise eligible modded map. Blacklist entries still win.");

        ShowCountdown = config.Bind(
            "UI (Local)", "ShowCountdown", true,
            "Show the remaining vote time in the vote window. Only changes your own display.");

        UiScale = config.Bind(
            "UI (Local)", "UIScale", 1f,
            new ConfigDescription("Personal voting UI scale.", new AcceptableValueRange<float>(0.75f, 1.5f)));

        UiOffsetX = config.Bind(
            "UI (Local)", "UIOffsetX", 0f,
            new ConfigDescription("Personal horizontal UI offset.", new AcceptableValueRange<float>(-400f, 400f)));

        UiOffsetY = config.Bind(
            "UI (Local)", "UIOffsetY", 0f,
            new ConfigDescription("Personal vertical UI offset.", new AcceptableValueRange<float>(-250f, 250f)));
    }
}
