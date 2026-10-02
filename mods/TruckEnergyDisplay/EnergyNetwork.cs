using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace TruckEnergyDisplay;

internal sealed class EnergyNetwork : IDisposable
{
    private const byte EventCode = 197;
    private readonly Dictionary<int, string> _subscribers = new();
    private readonly Dictionary<int, float> _requests = new();
    private Room? _room;
    private int _master;
    private string _nonce = Guid.NewGuid().ToString("N");
    private float _nextRequest;
    private float _received = -100;
    private EnergySnapshot? _snapshot;
    private EnergySnapshot? _hostSnapshot;
    internal EnergyNetwork() { PhotonNetwork.NetworkingClient.EventReceived += Receive; }
    public void Dispose() { PhotonNetwork.NetworkingClient.EventReceived -= Receive; Reset(); }
    internal void Reset()
    {
        _subscribers.Clear(); _requests.Clear(); _snapshot = null; _hostSnapshot = null;
        _received = -100; _nextRequest = 0; _nonce = Guid.NewGuid().ToString("N");
    }
    internal void RefreshContext()
    {
        Room? room = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom : null;
        int master = PhotonNetwork.MasterClient?.ActorNumber ?? 0;
        if (!ReferenceEquals(room, _room) || master != _master) { Reset(); _room = room; _master = master; }
    }
    internal void Publish(EnergySnapshot snapshot)
    {
        _hostSnapshot = snapshot;
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || !GameEnergyReader.InShop) return;
        foreach (var pair in _subscribers) {
            if (PhotonNetwork.CurrentRoom.GetPlayer(pair.Key) == null) continue;
            Send(pair.Key, SnapshotProtocol.Encode(pair.Value, RunManager.instance.levelsCompleted, snapshot));
        }
    }
    internal EnergySnapshot ClientRead()
    {
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient && _master > 0 && Time.unscaledTime >= _nextRequest) {
            _nextRequest = Time.unscaledTime + 2;
            Send(_master, new object[] { SnapshotProtocol.Marker, "request", _nonce, RunManager.instance.levelsCompleted });
        }
        if (_snapshot != null && Time.unscaledTime - _received < 5) return _snapshot;
        return new EnergySnapshot { Status = "Waiting for host. Host needs this mod." };
    }
    private static void Send(int actor, object[] data)
    {
        // No cached events or game-owned room properties. Payloads are namespaced.
        PhotonNetwork.RaiseEvent(EventCode, data, new RaiseEventOptions { TargetActors = new[] { actor }, CachingOption = EventCaching.DoNotCache }, SendOptions.SendReliable);
    }
    private void Receive(EventData message)
    {
        try {
            if (message.Code != EventCode || !PhotonNetwork.InRoom || !GameEnergyReader.InShop) return;
            RefreshContext();
            if (message.CustomData is not object[] a || a.Length < 2 || !Equals(a[0], SnapshotProtocol.Marker)) return;
            if (Equals(a[1], "request")) {
                if (!PhotonNetwork.IsMasterClient || a.Length != 4 || a[2] is not string nonce || nonce.Length != 32 ||
                    a[3] is not int level || level != RunManager.instance.levelsCompleted ||
                    message.Sender <= 0 || PhotonNetwork.CurrentRoom.GetPlayer(message.Sender) == null) return;
                if (_requests.TryGetValue(message.Sender, out float last) && Time.unscaledTime - last < 0.5f) return;
                _requests[message.Sender] = Time.unscaledTime; _subscribers[message.Sender] = nonce;
                if (_hostSnapshot != null) Send(message.Sender, SnapshotProtocol.Encode(nonce, level, _hostSnapshot));
            } else if (!PhotonNetwork.IsMasterClient && SnapshotProtocol.Decode(a, message.Sender,
                PhotonNetwork.MasterClient?.ActorNumber ?? 0, _nonce, RunManager.instance.levelsCompleted, out var snapshot)) {
                _snapshot = snapshot; _received = Time.unscaledTime;
            }
        } catch (Exception ex) { Plugin.Log.LogDebug("Ignored HUD message: " + ex.Message); }
    }
}
