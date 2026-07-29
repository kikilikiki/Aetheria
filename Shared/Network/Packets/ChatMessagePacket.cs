using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Network.Packets;

/// <summary>
/// Message de discussion. Envoyé par le client avec <see cref="SenderName"/> vide et
/// <see cref="Rank"/> par défaut (le serveur les renseigne avant diffusion — voir
/// <c>PlayerSession.HandleChatMessage</c>) ; renvoyé par le serveur, renseigné, à tous les
/// destinataires du canal (tout le monde pour <see cref="ChatChannel.Global"/>, seulement les
/// membres de la même guilde pour <see cref="ChatChannel.Guild"/>).
/// </summary>
public sealed class ChatMessagePacket : IPacket
{
    public OpCode OpCode => OpCode.ChatMessage;

    public required string SenderName { get; init; }
    public required string Message { get; init; }
    public ChatChannel Channel { get; init; } = ChatChannel.Global;
    public UserRank Rank { get; init; } = UserRank.Joueur;

    /// <summary>Destinataire d'un message privé (voir <see cref="ChatChannel.Prive"/>) — envoyé par le client (le nom du destinataire choisi), ignoré pour les autres canaux.</summary>
    public string TargetCharacterName { get; init; } = string.Empty;

    /// <summary>Voir GDD/demande utilisateur — "les grades coûtent [...] badge/couleur de pseudo" : palier de grade payant de l'expéditeur (0 = aucun), renseigné par le serveur avant diffusion — voir <c>PremiumService.BadgeTag</c>.</summary>
    public int SenderGradeTier { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(SenderName);
        writer.Write(Message);
        writer.Write((byte)Channel);
        writer.Write((byte)Rank);
        writer.Write(TargetCharacterName);
        writer.Write(SenderGradeTier);
    }

    public static IPacket Read(BinaryReader reader) => new ChatMessagePacket
    {
        SenderName = reader.ReadString(),
        Message = reader.ReadString(),
        Channel = (ChatChannel)reader.ReadByte(),
        Rank = (UserRank)reader.ReadByte(),
        TargetCharacterName = reader.ReadString(),
        SenderGradeTier = reader.ReadInt32(),
    };
}
