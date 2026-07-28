namespace Aetheria.Shared.Network.Packets;

/// <summary>
/// Diffusé à tous les autres joueurs connectés quand un personnage entre dans le monde (voir GDD
/// — visibilité globale des joueurs, même hors groupe), et renvoyé une fois par joueur déjà
/// connecté au nouvel arrivant pour qu'il reconstitue l'état courant (pas de packet "snapshot"
/// séparé : une série de <see cref="PlayerJoinedPacket"/> suffit).
/// </summary>
public sealed class PlayerJoinedPacket : IPacket
{
    public OpCode OpCode => OpCode.PlayerJoined;

    public required Guid CharacterId { get; init; }
    public required string Name { get; init; }
    public required int PositionX { get; init; }
    public required int PositionY { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(CharacterId.ToByteArray());
        writer.Write(Name);
        writer.Write(PositionX);
        writer.Write(PositionY);
    }

    public static IPacket Read(BinaryReader reader) => new PlayerJoinedPacket
    {
        CharacterId = new Guid(reader.ReadBytes(16)),
        Name = reader.ReadString(),
        PositionX = reader.ReadInt32(),
        PositionY = reader.ReadInt32(),
    };
}
