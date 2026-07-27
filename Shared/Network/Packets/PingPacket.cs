namespace Aetheria.Shared.Network.Packets;

/// <summary>Heartbeat envoyé par le client pour vérifier que la connexion est vivante.</summary>
public sealed class PingPacket : IPacket
{
    public OpCode OpCode => OpCode.Ping;

    public required long TimestampUtcTicks { get; init; }

    public void Write(BinaryWriter writer) => writer.Write(TimestampUtcTicks);

    public static IPacket Read(BinaryReader reader) => new PingPacket
    {
        TimestampUtcTicks = reader.ReadInt64(),
    };
}
