namespace Aetheria.Shared.Network.Packets;

/// <summary>Diffusé par le serveur (autoritaire) pour synchroniser la position d'un joueur.</summary>
public sealed class PlayerPositionUpdatePacket : IPacket
{
    public OpCode OpCode => OpCode.PlayerPositionUpdate;

    public required Guid CharacterId { get; init; }
    public required int PositionX { get; init; }
    public required int PositionY { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(CharacterId.ToByteArray());
        writer.Write(PositionX);
        writer.Write(PositionY);
    }

    public static IPacket Read(BinaryReader reader) => new PlayerPositionUpdatePacket
    {
        CharacterId = new Guid(reader.ReadBytes(16)),
        PositionX = reader.ReadInt32(),
        PositionY = reader.ReadInt32(),
    };
}
