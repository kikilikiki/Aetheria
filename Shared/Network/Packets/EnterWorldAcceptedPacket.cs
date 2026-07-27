namespace Aetheria.Shared.Network.Packets;

/// <summary>Confirmation que le personnage est entré dans le monde, avec sa position de départ.</summary>
public sealed class EnterWorldAcceptedPacket : IPacket
{
    public OpCode OpCode => OpCode.EnterWorldAccepted;

    public required Guid CharacterId { get; init; }
    public required int PositionX { get; init; }
    public required int PositionY { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(CharacterId.ToByteArray());
        writer.Write(PositionX);
        writer.Write(PositionY);
    }

    public static IPacket Read(BinaryReader reader) => new EnterWorldAcceptedPacket
    {
        CharacterId = new Guid(reader.ReadBytes(16)),
        PositionX = reader.ReadInt32(),
        PositionY = reader.ReadInt32(),
    };
}
