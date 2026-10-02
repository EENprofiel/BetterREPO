using System.Collections.Generic;
using System.Linq;

namespace VanillaOrModded.Maps;

internal sealed class MapHistory
{
    private readonly LinkedList<string> _newestFirst = new();

    internal IReadOnlyList<string> NewestFirst => _newestFirst.ToList();

    internal void Record(Level? level)
    {
        if (!MapCatalog.IsPlayableMap(level))
        {
            return;
        }

        string identifier = level!.name;
        if (_newestFirst.First?.Value == identifier)
        {
            return;
        }

        _newestFirst.AddFirst(identifier);
        while (_newestFirst.Count > 20)
        {
            _newestFirst.RemoveLast();
        }
    }

    internal void Clear()
    {
        _newestFirst.Clear();
    }
}
