using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Network.Packets;

/// <summary>
/// Diffusé à tous les autres joueurs connectés quand un personnage entre dans le monde (voir GDD
/// — visibilité globale des joueurs, même hors groupe), et renvoyé une fois par joueur déjà
/// connecté au nouvel arrivant pour qu'il reconstitue l'état courant (pas de packet "snapshot"
/// séparé : une série de <see cref="PlayerJoinedPacket"/> suffit). Porte aussi le grade (voir
/// GDD/demande utilisateur — "liste des joueurs en ligne avec leur grade") pour que la liste des
/// joueurs en ligne côté client n'ait pas besoin d'un appel séparé.
/// </summary>
public sealed class PlayerJoinedPacket : IPacket
{
    public OpCode OpCode => OpCode.PlayerJoined;

    public required Guid CharacterId { get; init; }
    public required string Name { get; init; }
    public required int PositionX { get; init; }
    public required int PositionY { get; init; }
    public UserRank Rank { get; init; } = UserRank.Joueur;

    public void Write(BinaryWriter writer)
    {
        writer.Write(CharacterId.ToByteArray());
        writer.Write(Name);
        writer.Write(PositionX);
        writer.Write(PositionY);
        writer.Write((byte)Rank);
    }

    public static IPacket Read(BinaryReader reader) => new PlayerJoinedPacket
    {
        CharacterId = new Guid(reader.ReadBytes(16)),
        Name = reader.ReadString(),
        PositionX = reader.ReadInt32(),
        PositionY = reader.ReadInt32(),
        Rank = (UserRank)reader.ReadByte(),
    };
}
