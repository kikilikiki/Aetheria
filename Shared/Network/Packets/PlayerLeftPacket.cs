namespace Aetheria.Shared.Network.Packets;

/// <summary>Diffusé à tous les autres joueurs connectés quand un personnage quitte le monde (déconnexion).</summary>
public sealed class PlayerLeftPacket : IPacket
{
    public OpCode OpCode => OpCode.PlayerLeft;

    public required Guid CharacterId { get; init; }

    public void Write(BinaryWriter writer) => writer.Write(CharacterId.ToByteArray());

    public static IPacket Read(BinaryReader reader) => new PlayerLeftPacket
    {
        CharacterId = new Guid(reader.ReadBytes(16)),
    };
}
