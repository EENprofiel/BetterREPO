using System;
using System.Collections.Generic;
using System.Linq;

namespace VanillaOrModded.Core;

internal static class HistoryFilter
{
    internal static List<T> Apply<T>(
        IEnumerable<T> candidates,
        IEnumerable<string> newestFirstHistory,
        Func<T, string> identifier,
        bool enabled,
        int historyLength)
    {
        List<T> original = candidates.ToList();
        if (!enabled || historyLength <= 0 || original.Count == 0)
        {
            return original;
        }

        HashSet<string> excluded = new(
            newestFirstHistory.Take(Math.Max(0, historyLength)),
            StringComparer.OrdinalIgnoreCase);

        List<T> filtered = original
            .Where(candidate => !excluded.Contains(identifier(candidate)))
            .ToList();

        return filtered.Count > 0 ? filtered : original;
    }
}
