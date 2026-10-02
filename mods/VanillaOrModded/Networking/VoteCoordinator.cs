using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VanillaOrModded.Core;
using VanillaOrModded.Maps;
using VanillaOrModded.Selection;
using VanillaOrModded.UI;

namespace VanillaOrModded.Networking;

internal static class VoteCoordinator
{
    private const float CapabilityWindowSeconds = 1f;
    private const float ResultStateSeconds = 5f;
    private const float DisconnectPollSeconds = 0.2f;

    private static readonly VoteSession Session = new();
    private static readonly HashSet<int> CompatibleActors = new();
    private static readonly HashSet<int> WarnedIncompatibleActors = new();

    private static int _nextSessionId;
    private static int _hostActorAtStart;
    private static float _preparationEndsAt;
    private static float _votingEndsAt;
    private static float _resultEndsAt;
    private static float _nextDisconnectPoll;
    private static int _highestAcceptedSessionId;
    private static int _lastKnownMasterActor;

    internal static bool IsVoting => Session.State == VoteLifecycle.Voting;

    internal static void Initialize()
    {
        Session.Reset();
        CompatibleActors.Clear();
        WarnedIncompatibleActors.Clear();
        _highestAcceptedSessionId = 0;
        _lastKnownMasterActor = 0;
        SelectionController.ClearPendingSelection();
        VoteNetwork.Initialize(HandleNetworkMessage);
    }

    internal static void Shutdown()
    {
        VoteNetwork.Shutdown();
        CancelLocalSession("Plugin shutdown");
        CompatibleActors.Clear();
        WarnedIncompatibleActors.Clear();
        _highestAcceptedSessionId = 0;
        _lastKnownMasterActor = 0;
    }

    internal static void OnTruckHudReady()
    {
        VoteNetwork.SendHello();

        if (!VoteNetwork.IsHost || !Plugin.Settings.VotingEnabled.Value)
        {
            return;
        }

        if (Session.State == VoteLifecycle.Result && Time.realtimeSinceStartup >= _resultEndsAt)
        {
            Session.Reset();
        }

        if (Session.State != VoteLifecycle.Idle)
        {
            return;
        }

        BeginPreparation();
    }

    internal static void Tick()
    {
        float now = Time.realtimeSinceStartup;

        if (VoteNetwork.IsMultiplayer && !Photon.Pun.PhotonNetwork.InRoom)
        {
            SelectionController.ResetForLobby();
            _highestAcceptedSessionId = 0;
            _lastKnownMasterActor = 0;
            if (Session.State != VoteLifecycle.Idle)
            {
                CancelLocalSession("Left the multiplayer room");
            }
            return;
        }

        if (VoteNetwork.IsMultiplayer)
        {
            int currentMaster = VoteNetwork.MasterActorNumber;
            if (currentMaster > 0 && _lastKnownMasterActor > 0 && currentMaster != _lastKnownMasterActor)
            {
                // A new master may have its own session counter. Do not let
                // the previous master's watermark reject that fresh stream.
                _highestAcceptedSessionId = 0;
            }

            if (currentMaster > 0)
            {
                _lastKnownMasterActor = currentMaster;
            }
        }

        switch (Session.State)
        {
            case VoteLifecycle.Preparing:
                if (!VoteNetwork.IsHost)
                {
                    CancelLocalSession("Host authority changed during vote preparation");
                }
                else if (now >= _preparationEndsAt)
                {
                    StartHostVote();
                }
                break;

            case VoteLifecycle.Voting:
                if (_hostActorAtStart != VoteNetwork.MasterActorNumber)
                {
                    CancelLocalSession("Host authority changed during voting");
                    ResultHud.Show("VOTE CANCELLED", 3f);
                    return;
                }

                VoteUi.SetCountdown(Mathf.Max(0f, _votingEndsAt - now));
                if (VoteNetwork.IsHost)
                {
                    if (now >= _nextDisconnectPoll)
                    {
                        _nextDisconnectPoll = now + DisconnectPollSeconds;
                        PruneDisconnectedEligibleActors();
                    }

                    if (Session.State == VoteLifecycle.Voting && now >= _votingEndsAt)
                    {
                        ResolveHostVote();
                    }
                }
                break;

            case VoteLifecycle.Result:
                if (now >= _resultEndsAt)
                {
                    Session.Reset();
                }
                break;
        }
    }

    internal static void SubmitLocalVote(MapCategory option)
    {
        if (Session.State != VoteLifecycle.Voting || !Session.EligibleActors.Contains(VoteNetwork.LocalActorNumber))
        {
            return;
        }

        VoteUi.SetSelected(option);
        if (VoteNetwork.IsHost)
        {
            AcceptHostVote(VoteNetwork.LocalActorNumber, Session.SessionId, option);
        }
        else
        {
            VoteNetwork.SendToHost(MessageType.SubmitVote, Session.SessionId, (int)option);
        }
    }

    internal static void ForceFinish()
    {
        if (VoteNetwork.IsHost && Session.State == VoteLifecycle.Voting)
        {
            ResolveHostVote();
        }
    }

    private static void BeginPreparation()
    {
        _nextSessionId = _nextSessionId == int.MaxValue ? 1 : _nextSessionId + 1;
        Session.BeginPreparing(_nextSessionId);
        _highestAcceptedSessionId = Math.Max(_highestAcceptedSessionId, _nextSessionId);

        CompatibleActors.RemoveWhere(actor => !VoteNetwork.IsActorPresent(actor));
        CompatibleActors.Add(VoteNetwork.LocalActorNumber);
        _preparationEndsAt = Time.realtimeSinceStartup + CapabilityWindowSeconds;
        VoteNetwork.SendProbe();
        Plugin.Logger.LogDebug($"Preparing vote session {_nextSessionId}; probing compatible peers.");
    }

    private static void StartHostVote()
    {
        HashSet<int> presentActors = new(VoteNetwork.CurrentActors());
        int[] eligible = CompatibleActors
            .Where(presentActors.Contains)
            .Where(actor => actor > 0)
            .OrderBy(actor => actor)
            .ToArray();

        if (!eligible.Contains(VoteNetwork.LocalActorNumber))
        {
            eligible = eligible.Append(VoteNetwork.LocalActorNumber).OrderBy(actor => actor).ToArray();
        }

        float duration = Mathf.Clamp(Plugin.Settings.VotingDuration.Value, 2f, 60f);
        Session.BeginVoting(Session.SessionId, eligible);
        _hostActorAtStart = VoteNetwork.LocalActorNumber;
        _votingEndsAt = Time.realtimeSinceStartup + duration;
        _nextDisconnectPoll = Time.realtimeSinceStartup;

        VoteUi.Open(isHost: true);
        VoteUi.SetTotals(0, 0, 0);
        VoteUi.SetCountdown(duration);
        VoteNetwork.SendToClients(MessageType.VoteStart, Session.SessionId, duration, eligible);
        Plugin.Logger.LogInfo($"Vote session {Session.SessionId} started with {eligible.Length} compatible participant(s).");
    }

    private static void AcceptHostVote(int sender, int sessionId, MapCategory option)
    {
        if (sessionId != Session.SessionId || !VoteNetwork.IsActorPresent(sender) || !Session.TryVote(sender, option))
        {
            return;
        }

        BroadcastTotals();
        if (Plugin.Settings.EndEarlyWhenAllVoted.Value && Session.AllVoted)
        {
            ResolveHostVote();
        }
    }

    private static void BroadcastTotals()
    {
        int vanilla = Session.Count(MapCategory.Vanilla);
        int modded = Session.Count(MapCategory.Modded);
        int random = Session.Count(MapCategory.Random);
        VoteUi.SetTotals(vanilla, modded, random);
        VoteNetwork.SendToClients(MessageType.Totals, Session.SessionId, vanilla, modded, random);
    }

    private static void PruneDisconnectedEligibleActors()
    {
        HashSet<int> present = new(VoteNetwork.CurrentActors());
        bool changed = false;
        foreach (int actor in Session.EligibleActors.ToArray())
        {
            if (!present.Contains(actor))
            {
                changed |= Session.RemoveActor(actor);
                CompatibleActors.Remove(actor);
            }
        }

        if (!changed)
        {
            return;
        }

        BroadcastTotals();
        if (Plugin.Settings.EndEarlyWhenAllVoted.Value && Session.AllVoted)
        {
            ResolveHostVote();
        }
    }

    private static void ResolveHostVote()
    {
        if (!VoteNetwork.IsHost || Session.State != VoteLifecycle.Voting)
        {
            return;
        }

        MapCategory? winner = Session.Resolve(tiedCount => UnityEngine.Random.Range(0, tiedCount));
        Level? selected = winner.HasValue ? SelectionController.ChooseAndQueue(winner.Value) : null;
        if (!winner.HasValue)
        {
            SelectionController.ClearPendingSelection();
        }

        VoteUi.Close();
        string? visibleMapName = selected != null && Plugin.Settings.ShowChosenMapName.Value
            ? MapCatalog.DisplayName(selected)
            : null;
        ShowResult(winner, selected != null, visibleMapName);

        VoteNetwork.SendToClients(
            MessageType.VoteResult,
            Session.SessionId,
            winner.HasValue ? (int)winner.Value : -1,
            selected != null,
            visibleMapName ?? string.Empty);

        Session.EnterResult();
        _resultEndsAt = Time.realtimeSinceStartup + ResultStateSeconds;
    }

    private static void ShowResult(MapCategory? winner, bool selectionSucceeded, string? visibleMapName)
    {
        if (!winner.HasValue)
        {
            ResultHud.Show("NO VOTES\nGAME SELECTION UNCHANGED", ResultStateSeconds);
            return;
        }

        string message = $"{winner.Value.ToString().ToUpperInvariant()} WINS";
        if (!selectionSucceeded)
        {
            message += "\nNO ELIGIBLE MAPS";
        }
        else if (!string.IsNullOrWhiteSpace(visibleMapName))
        {
            message += $"\n{visibleMapName}";
        }

        ResultHud.Show(message, ResultStateSeconds);
    }

    private static void HandleNetworkMessage(NetworkMessage message)
    {
        if (message.ProtocolVersion != Protocol.Version)
        {
            HandleProtocolMismatch(message);
            return;
        }

        switch (message.Type)
        {
            case MessageType.CapabilityProbe:
                if (!VoteNetwork.IsHost && message.Sender == VoteNetwork.MasterActorNumber)
                {
                    VoteNetwork.SendHello();
                }
                break;

            case MessageType.CapabilityHello:
                if (VoteNetwork.IsHost && VoteNetwork.IsActorPresent(message.Sender))
                {
                    CompatibleActors.Add(message.Sender);
                }
                break;

            case MessageType.VoteStart:
                HandleClientVoteStart(message);
                break;

            case MessageType.SubmitVote:
                HandleClientVoteSubmission(message);
                break;

            case MessageType.Totals:
                HandleClientTotals(message);
                break;

            case MessageType.VoteResult:
                HandleClientResult(message);
                break;
        }
    }

    private static void HandleProtocolMismatch(NetworkMessage message)
    {
        if (VoteNetwork.IsHost && message.Type == MessageType.CapabilityHello && VoteNetwork.IsActorPresent(message.Sender))
        {
            if (WarnedIncompatibleActors.Add(message.Sender))
            {
                Plugin.Logger.LogWarning($"A client uses incompatible VanillaOrModded protocol {message.ProtocolVersion}; they will not participate in votes.");
            }
        }
        else if (!VoteNetwork.IsHost && message.Sender == VoteNetwork.MasterActorNumber && WarnedIncompatibleActors.Add(message.Sender))
        {
            ResultHud.Show("INCOMPATIBLE VANILLAORMODDED VERSION\nVOTING DISABLED FOR YOU", 5f);
        }
    }

    private static void HandleClientVoteStart(NetworkMessage message)
    {
        if (VoteNetwork.IsHost || message.Sender != VoteNetwork.MasterActorNumber || message.Payload.Length != 3)
        {
            return;
        }

        if (!TryInt(message.Payload[0], out int sessionId) ||
            !TryFloat(message.Payload[1], out float duration) ||
            message.Payload[2] is not int[] eligible ||
            sessionId <= 0 || duration < 2f || duration > 60f)
        {
            return;
        }

        if (_hostActorAtStart > 0 && message.Sender != _hostActorAtStart)
        {
            // Session IDs are monotonic per master. A new master can safely
            // restart at one after a host migration.
            _highestAcceptedSessionId = 0;
        }

        if (sessionId <= _highestAcceptedSessionId)
        {
            return;
        }

        _highestAcceptedSessionId = sessionId;
        if (!eligible.Contains(VoteNetwork.LocalActorNumber))
        {
            return;
        }

        Session.BeginVoting(sessionId, eligible);
        _hostActorAtStart = message.Sender;
        _votingEndsAt = Time.realtimeSinceStartup + duration;
        VoteUi.Open(isHost: false);
        VoteUi.SetTotals(0, 0, 0);
        VoteUi.SetCountdown(duration);
    }

    private static void HandleClientVoteSubmission(NetworkMessage message)
    {
        if (!VoteNetwork.IsHost || message.Payload.Length != 2 ||
            !TryInt(message.Payload[0], out int sessionId) ||
            !TryInt(message.Payload[1], out int rawOption) ||
            !Enum.IsDefined(typeof(MapCategory), rawOption))
        {
            return;
        }

        AcceptHostVote(message.Sender, sessionId, (MapCategory)rawOption);
    }

    private static void HandleClientTotals(NetworkMessage message)
    {
        if (VoteNetwork.IsHost || Session.State != VoteLifecycle.Voting || message.Sender != _hostActorAtStart || message.Sender != VoteNetwork.MasterActorNumber || message.Payload.Length != 4 ||
            !TryInt(message.Payload[0], out int sessionId) || sessionId != Session.SessionId ||
            !TryInt(message.Payload[1], out int vanilla) ||
            !TryInt(message.Payload[2], out int modded) ||
            !TryInt(message.Payload[3], out int random) ||
            vanilla < 0 || modded < 0 || random < 0 ||
            vanilla + modded + random > Session.EligibleActors.Count)
        {
            return;
        }

        VoteUi.SetTotals(vanilla, modded, random);
    }

    private static void HandleClientResult(NetworkMessage message)
    {
        if (VoteNetwork.IsHost || Session.State != VoteLifecycle.Voting || message.Sender != _hostActorAtStart || message.Sender != VoteNetwork.MasterActorNumber || message.Payload.Length != 4 ||
            !TryInt(message.Payload[0], out int sessionId) || sessionId != Session.SessionId ||
            !TryInt(message.Payload[1], out int rawWinner) ||
            message.Payload[2] is not bool selectionSucceeded ||
            message.Payload[3] is not string visibleMapName ||
            (rawWinner != -1 && !Enum.IsDefined(typeof(MapCategory), rawWinner)))
        {
            return;
        }

        MapCategory? winner = rawWinner == -1 ? null : (MapCategory)rawWinner;
        VoteUi.Close();
        ShowResult(winner, selectionSucceeded, string.IsNullOrWhiteSpace(visibleMapName) ? null : visibleMapName);
        Session.EnterResult();
        _resultEndsAt = Time.realtimeSinceStartup + ResultStateSeconds;
    }

    private static void CancelLocalSession(string reason)
    {
        if (Session.State != VoteLifecycle.Idle)
        {
            Plugin.Logger.LogDebug(reason);
        }

        SelectionController.ClearPendingSelection();
        Session.Reset();
        VoteUi.Close();
    }

    private static bool TryInt(object value, out int result)
    {
        try
        {
            result = Convert.ToInt32(value);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }

    private static bool TryFloat(object value, out float result)
    {
        try
        {
            result = Convert.ToSingle(value);
            return true;
        }
        catch
        {
            result = 0f;
            return false;
        }
    }
}
