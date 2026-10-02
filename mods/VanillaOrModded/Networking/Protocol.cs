namespace VanillaOrModded.Networking;

internal static class Protocol
{
    internal const byte EventCode = 199;
    internal const string Magic = "VanillaOrModded";
    internal const int Version = 1;
}

internal enum MessageType
{
    CapabilityProbe = 1,
    CapabilityHello = 2,
    VoteStart = 3,
    SubmitVote = 4,
    Totals = 5,
    VoteResult = 6
}

internal readonly struct NetworkMessage
{
    internal NetworkMessage(int sender, int protocolVersion, MessageType type, object[] payload)
    {
        Sender = sender;
        ProtocolVersion = protocolVersion;
        Type = type;
        Payload = payload;
    }

    internal int Sender { get; }
    internal int ProtocolVersion { get; }
    internal MessageType Type { get; }
    internal object[] Payload { get; }
}
