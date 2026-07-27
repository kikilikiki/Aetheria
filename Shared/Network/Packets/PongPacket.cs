namespace Aetheria.Shared.Network.Packets;

/// <summary>Réponse du serveur à un <see cref="PingPacket"/>, renvoie l'horodatage d'origine.</summary>
public sealed class PongPacket : IPacket
{
    public OpCode OpCode => OpCode.Pong;

    public required long TimestampUtcTicks { get; init; }

    public void Write(BinaryWriter writer) => writer.Write(TimestampUtcTicks);

    public static IPacket Read(BinaryReader reader) => new PongPacket
    {
        TimestampUtcTicks = reader.ReadInt64(),
    };
}
