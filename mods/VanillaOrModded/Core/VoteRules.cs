using System;

namespace VanillaOrModded.Core;

internal static class VoteRules
{
    internal const float MinDurationSeconds = 2f;
    internal const float MaxDurationSeconds = 60f;
    internal const float DefaultDurationSeconds = 5f;

    internal static float ClampDuration(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds))
        {
            return DefaultDurationSeconds;
        }

        return Math.Min(MaxDurationSeconds, Math.Max(MinDurationSeconds, seconds));
    }

    internal static TieBreakMode NormalizeTieBreak(TieBreakMode mode)
    {
        return Enum.IsDefined(typeof(TieBreakMode), mode) ? mode : TieBreakMode.Random;
    }

    internal static bool IsExpired(float now, float endsAt)
    {
        return now >= endsAt;
    }
}
