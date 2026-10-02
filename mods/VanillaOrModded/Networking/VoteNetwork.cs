using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace VanillaOrModded.Networking;

internal static class VoteNetwork
{
    private static readonly RaiseEventOptions RaiseOthers = new() { Receivers = ReceiverGroup.Others };
    private static readonly RaiseEventOptions RaiseMaster = new() { Receivers = ReceiverGroup.MasterClient };
    private static readonly SendOptions Reliable = SendOptions.SendReliable;

    private static Action<NetworkMessage>? _handler;
    private static bool _subscribed;

    internal static bool IsMultiplayer => SemiFunc.IsMultiplayer();
    internal static bool IsHost => SemiFunc.IsMasterClientOrSingleplayer();
    internal static int LocalActorNumber => IsMultiplayer ? PhotonNetwork.LocalPlayer?.ActorNumber ?? 0 : 1;
    internal static int MasterActorNumber => IsMultiplayer ? PhotonNetwork.MasterClient?.ActorNumber ?? 0 : 1;

    internal static void Initialize(Action<NetworkMessage> handler)
    {
        _handler = handler;
        if (_subscribed)
        {
            return;
        }

        PhotonNetwork.NetworkingClient.EventReceived += OnEventReceived;
        _subscribed = true;
    }

    internal static void Shutdown()
    {
        if (_subscribed)
        {
            PhotonNetwork.NetworkingClient.EventReceived -= OnEventReceived;
            _subscribed = false;
        }

        _handler = null;
    }

    internal static IReadOnlyCollection<int> CurrentActors()
    {
        if (!IsMultiplayer)
        {
            return new[] { 1 };
        }

        return PhotonNetwork.CurrentRoom?.Players.Keys.ToArray() ?? Array.Empty<int>();
    }

    internal static bool IsActorPresent(int actorNumber)
    {
        return CurrentActors().Contains(actorNumber);
    }

    internal static void SendProbe()
    {
        Send(MessageType.CapabilityProbe, RaiseOthers);
    }

    internal static void SendHello()
    {
        if (!IsMultiplayer)
        {
            return;
        }

        Send(MessageType.CapabilityHello, RaiseMaster);
    }

    internal static void SendToHost(MessageType type, params object[] payload)
    {
        Send(type, RaiseMaster, payload);
    }

    internal static void SendToClients(MessageType type, params object[] payload)
    {
        Send(type, RaiseOthers, payload);
    }

    private static void Send(MessageType type, RaiseEventOptions options, params object[] payload)
    {
        if (!IsMultiplayer || !PhotonNetwork.InRoom)
        {
            return;
        }

        object[] envelope = new object[3 + payload.Length];
        envelope[0] = Protocol.Magic;
        envelope[1] = Protocol.Version;
        envelope[2] = (int)type;
        Array.Copy(payload, 0, envelope, 3, payload.Length);

        PhotonNetwork.RaiseEvent(Protocol.EventCode, envelope, options, Reliable);
    }

    private static void OnEventReceived(EventData eventData)
    {
        if (eventData.Code != Protocol.EventCode || eventData.CustomData is not object[] envelope || envelope.Length < 3)
        {
            return;
        }

        try
        {
            if (envelope[0] is not string magic || !string.Equals(magic, Protocol.Magic, StringComparison.Ordinal))
            {
                return;
            }

            int protocolVersion = Convert.ToInt32(envelope[1]);
            int rawType = Convert.ToInt32(envelope[2]);
            if (!Enum.IsDefined(typeof(MessageType), rawType))
            {
                return;
            }

            object[] payload = envelope.Skip(3).ToArray();
            _handler?.Invoke(new NetworkMessage(eventData.Sender, protocolVersion, (MessageType)rawType, payload));
        }
        catch (Exception exception)
        {
            Plugin.Logger.LogWarning($"Ignored a malformed VanillaOrModded network message: {exception.GetType().Name}.");
        }
    }
}
