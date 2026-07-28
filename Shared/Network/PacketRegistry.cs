using Aetheria.Shared.Network.Packets;

namespace Aetheria.Shared.Network;

/// <summary>
/// Associe chaque <see cref="OpCode"/> à la fonction de désérialisation du packet
/// correspondant. L'enregistrement se fait une seule fois, au premier accès à la classe.
/// </summary>
public static class PacketRegistry
{
    private static readonly Dictionary<OpCode, Func<BinaryReader, IPacket>> Readers = new()
    {
        [OpCode.Ping] = PingPacket.Read,
        [OpCode.Pong] = PongPacket.Read,
        [OpCode.EnterWorldRequest] = EnterWorldRequestPacket.Read,
        [OpCode.EnterWorldAccepted] = EnterWorldAcceptedPacket.Read,
        [OpCode.EnterWorldRejected] = EnterWorldRejectedPacket.Read,
        [OpCode.PlayerMove] = PlayerMovePacket.Read,
        [OpCode.PlayerPositionUpdate] = PlayerPositionUpdatePacket.Read,
        [OpCode.PlayerJoined] = PlayerJoinedPacket.Read,
        [OpCode.PlayerLeft] = PlayerLeftPacket.Read,
        [OpCode.ChatMessage] = ChatMessagePacket.Read,
    };

    public static IPacket Read(OpCode opCode, BinaryReader reader)
    {
        if (!Readers.TryGetValue(opCode, out var read))
        {
            throw new InvalidOperationException($"Aucun packet enregistré pour l'opcode '{opCode}'.");
        }

        return read(reader);
    }
}
