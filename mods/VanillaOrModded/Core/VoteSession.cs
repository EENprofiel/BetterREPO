using System;
using System.Collections.Generic;
using System.Linq;
using VanillaOrModded.Maps;

namespace VanillaOrModded.Core;

internal sealed class VoteSession
{
    private readonly HashSet<int> _eligibleActors = new();
    private readonly Dictionary<int, MapCategory> _votes = new();

    internal VoteLifecycle State { get; private set; } = VoteLifecycle.Idle;
    internal int SessionId { get; private set; }
    internal IReadOnlyCollection<int> EligibleActors => _eligibleActors;
    internal IReadOnlyDictionary<int, MapCategory> Votes => _votes;
    internal bool AllVoted => _eligibleActors.Count > 0 && _votes.Count == _eligibleActors.Count;

    internal void BeginPreparing(int sessionId)
    {
        SessionId = sessionId;
        _eligibleActors.Clear();
        _votes.Clear();
        State = VoteLifecycle.Preparing;
    }

    internal void BeginVoting(int sessionId, IEnumerable<int> eligibleActors)
    {
        SessionId = sessionId;
        _eligibleActors.Clear();
        _eligibleActors.UnionWith(eligibleActors.Where(actor => actor > 0));
        _votes.Clear();
        State = VoteLifecycle.Voting;
    }

    internal bool TryVote(int actorNumber, MapCategory option)
    {
        if (State != VoteLifecycle.Voting || !_eligibleActors.Contains(actorNumber) || !Enum.IsDefined(typeof(MapCategory), option))
        {
            return false;
        }

        if (_votes.TryGetValue(actorNumber, out MapCategory existing) && existing == option)
        {
            return false;
        }

        _votes[actorNumber] = option;
        return true;
    }

    internal bool RemoveActor(int actorNumber)
    {
        bool removed = _eligibleActors.Remove(actorNumber);
        _votes.Remove(actorNumber);
        return removed;
    }

    internal int Count(MapCategory option)
    {
        return _votes.Values.Count(value => value == option);
    }

    internal MapCategory? Resolve(Func<int, int> chooseIndex)
    {
        if (State != VoteLifecycle.Voting)
        {
            return null;
        }

        State = VoteLifecycle.Resolving;
        if (_votes.Count == 0)
        {
            return null;
        }

        int highest = Enum.GetValues(typeof(MapCategory))
            .Cast<MapCategory>()
            .Max(Count);

        List<MapCategory> tiedTop = Enum.GetValues(typeof(MapCategory))
            .Cast<MapCategory>()
            .Where(option => Count(option) == highest)
            .ToList();

        int index = chooseIndex(tiedTop.Count);
        if (index < 0 || index >= tiedTop.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(chooseIndex), "The RNG returned an invalid tie index.");
        }

        return tiedTop[index];
    }

    internal void EnterResult()
    {
        State = VoteLifecycle.Result;
    }

    internal void Reset()
    {
        _eligibleActors.Clear();
        _votes.Clear();
        State = VoteLifecycle.Idle;
    }
}
