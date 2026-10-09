using VanillaOrModded.Core;
using VanillaOrModded.Maps;

int checks = 0;
void Check(bool result, string name) { if (!result) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }

VoteSession NewVote(params int[] actors)
{
    var session = new VoteSession();
    session.BeginPreparing(1);
    session.BeginVoting(1, actors);
    return session;
}
int First(int count) => 0;
int Last(int count) => count - 1;

// Majority
var majority = NewVote(1, 2, 3);
majority.TryVote(1, MapCategory.Modded);
majority.TryVote(2, MapCategory.Modded);
majority.TryVote(3, MapCategory.Vanilla);
Check(majority.AllVoted, "all eligible voted is detected");
Check(majority.Resolve(First) == MapCategory.Modded, "majority wins");
Check(majority.State == VoteLifecycle.Resolving, "resolve moves to resolving");
Check(majority.Resolve(First) == null, "second resolve is ignored");

// Changed vote moves one count
var change = NewVote(1, 2);
change.TryVote(1, MapCategory.Vanilla);
Check(change.TryVote(1, MapCategory.Random), "changed vote accepted");
Check(!change.TryVote(1, MapCategory.Random), "same vote is not a change");
Check(change.Count(MapCategory.Vanilla) == 0 && change.Count(MapCategory.Random) == 1, "changed vote moves one count");

// Tie
var tie = NewVote(1, 2, 3);
tie.TryVote(1, MapCategory.Vanilla);
tie.TryVote(2, MapCategory.Modded);
tie.TryVote(3, MapCategory.Random);
Check(tie.Resolve(First) == MapCategory.Vanilla, "three way tie uses first tied index");
var tieLast = NewVote(1, 2);
tieLast.TryVote(1, MapCategory.Vanilla);
tieLast.TryVote(2, MapCategory.Modded);
Check(tieLast.Resolve(Last) == MapCategory.Modded, "two way tie only offers tied options");
var tieNever = NewVote(1, 2, 3);
tieNever.TryVote(1, MapCategory.Vanilla);
tieNever.TryVote(2, MapCategory.Vanilla);
tieNever.TryVote(3, MapCategory.Random);
Check(tieNever.Resolve(Last) == MapCategory.Vanilla, "losing option never enters a draw");
bool threw = false;
try { var bad = NewVote(1, 2); bad.TryVote(1, MapCategory.Vanilla); bad.TryVote(2, MapCategory.Modded); bad.Resolve(_ => 5); }
catch (ArgumentOutOfRangeException) { threw = true; }
Check(threw, "invalid tie index is rejected");

// Tie break favor not last
var favor = NewVote(1, 2);
favor.TryVote(1, MapCategory.Vanilla);
favor.TryVote(2, MapCategory.Modded);
Check(favor.Resolve(First, TieBreakMode.FavorNotLast, MapCategory.Vanilla) == MapCategory.Modded, "favor not last skips last winner");
var favorNoTie = NewVote(1, 2);
favorNoTie.TryVote(1, MapCategory.Vanilla);
favorNoTie.TryVote(2, MapCategory.Vanilla);
Check(favorNoTie.Resolve(First, TieBreakMode.FavorNotLast, MapCategory.Vanilla) == MapCategory.Vanilla, "favor not last never overrides a clear winner");
var favorNone = NewVote(1, 2);
favorNone.TryVote(1, MapCategory.Vanilla);
favorNone.TryVote(2, MapCategory.Modded);
Check(favorNone.Resolve(First, TieBreakMode.FavorNotLast, null) == MapCategory.Vanilla, "favor not last without history uses random draw");
var favorRandom = NewVote(1, 2);
favorRandom.TryVote(1, MapCategory.Vanilla);
favorRandom.TryVote(2, MapCategory.Modded);
Check(favorRandom.Resolve(First, TieBreakMode.Random, MapCategory.Vanilla) == MapCategory.Vanilla, "random mode ignores last winner");

// No votes and timeout
var none = NewVote(1, 2);
Check(!none.AllVoted, "no votes is not all voted");
Check(none.Resolve(First) == null, "zero votes returns no winner");
var partial = NewVote(1, 2, 3);
partial.TryVote(2, MapCategory.Random);
Check(!partial.AllVoted, "missing voters do not count as all voted");
Check(partial.Resolve(First) == MapCategory.Random, "timeout closes with received votes");
Check(VoteRules.IsExpired(10f, 10f) && !VoteRules.IsExpired(9.9f, 10f), "timeout fires at deadline");

// Duration validation
Check(VoteRules.ClampDuration(0f) == 2f, "duration below minimum is clamped");
Check(VoteRules.ClampDuration(500f) == 60f, "duration above maximum is clamped");
Check(VoteRules.ClampDuration(12f) == 12f, "valid duration is kept");
Check(VoteRules.ClampDuration(float.NaN) == 5f, "NaN duration falls back to default");
Check(VoteRules.NormalizeTieBreak((TieBreakMode)99) == TieBreakMode.Random, "unknown tie break falls back to random");

// Late join, ineligible, disconnect
var late = NewVote(1, 2);
Check(!late.TryVote(3, MapCategory.Vanilla), "late joiner cannot vote");
Check(!late.TryVote(2, (MapCategory)42), "undefined option rejected");
late.TryVote(2, MapCategory.Modded);
Check(late.RemoveActor(2) && late.Count(MapCategory.Modded) == 0, "disconnect removes eligibility and vote");
Check(!late.RemoveActor(2), "removing twice reports no change");

// Voting outside the Voting state
var idle = new VoteSession();
Check(!idle.TryVote(1, MapCategory.Vanilla), "idle session rejects votes");
var prep = new VoteSession();
prep.BeginPreparing(2);
Check(!prep.TryVote(1, MapCategory.Vanilla), "preparing session rejects votes");

// Host change
var hostChange = NewVote(1, 2);
hostChange.TryVote(1, MapCategory.Vanilla);
hostChange.Reset();
Check(hostChange.State == VoteLifecycle.Idle && hostChange.Votes.Count == 0 && hostChange.EligibleActors.Count == 0, "reset clears votes and eligibility");
Check(!hostChange.TryVote(1, MapCategory.Vanilla), "reset session rejects stale votes");
hostChange.BeginPreparing(1);
hostChange.BeginVoting(1, new[] { 5, 6 });
Check(hostChange.TryVote(5, MapCategory.Modded) && !hostChange.TryVote(1, MapCategory.Modded), "new host session uses its own eligible set");
var actorZero = NewVote(0, -1, 4);
Check(actorZero.EligibleActors.Count == 1, "non positive actor numbers are not eligible");

// History filtering
string Id(string s) => s;
var pool = new List<string> { "A", "B", "C" };
Check(HistoryFilter.Apply(pool, new[] { "A" }, Id, true, 1).SequenceEqual(new[] { "B", "C" }), "history removes previous map");
Check(HistoryFilter.Apply(pool, new[] { "A", "B" }, Id, true, 1).SequenceEqual(new[] { "B", "C" }), "history length one only uses newest entry");
Check(HistoryFilter.Apply(pool, new[] { "A", "B" }, Id, true, 2).SequenceEqual(new[] { "C" }), "history length two removes two maps");
Check(HistoryFilter.Apply(pool, new[] { "a" }, Id, true, 1).SequenceEqual(new[] { "B", "C" }), "history match ignores case");
Check(HistoryFilter.Apply(pool, new[] { "A" }, Id, false, 1).Count == 3, "disabled history keeps all maps");
Check(HistoryFilter.Apply(pool, new[] { "A" }, Id, true, 0).Count == 3, "history length zero keeps all maps");
Check(HistoryFilter.Apply(new List<string> { "A" }, new[] { "A" }, Id, true, 1).SequenceEqual(new[] { "A" }), "single map pool is restored");
Check(HistoryFilter.Apply(new List<string>(), new[] { "A" }, Id, true, 1).Count == 0, "empty pool stays empty");

Console.WriteLine($"{checks} checks passed.");
