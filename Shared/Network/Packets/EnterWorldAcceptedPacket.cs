using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Network.Packets;

/// <summary>Confirmation que le personnage est entré dans le monde, avec sa position de départ et son grade (voir GDD — liste des joueurs en ligne).</summary>
public sealed class EnterWorldAcceptedPacket : IPacket
{
    public OpCode OpCode => OpCode.EnterWorldAccepted;

    public required Guid CharacterId { get; init; }
    public required int PositionX { get; init; }
    public required int PositionY { get; init; }
    public UserRank Rank { get; init; } = UserRank.Joueur;

    /// <summary>Voir GDD/demande utilisateur — "le panel admin en jeu [est] pour les admins" : le flag technique IsAdmin (distinct du grade Fondateur) doit aussi donner accès au panel admin en jeu.</summary>
    public bool IsAdmin { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(CharacterId.ToByteArray());
        writer.Write(PositionX);
        writer.Write(PositionY);
        writer.Write((byte)Rank);
        writer.Write(IsAdmin);
    }

    public static IPacket Read(BinaryReader reader) => new EnterWorldAcceptedPacket
    {
        CharacterId = new Guid(reader.ReadBytes(16)),
        PositionX = reader.ReadInt32(),
        PositionY = reader.ReadInt32(),
        Rank = (UserRank)reader.ReadByte(),
        IsAdmin = reader.ReadBoolean(),
    };
}
