namespace Aetheria.Shared.Network.Packets;

/// <summary>
/// Voir GDD/demande utilisateur — "ajouter les demandes en duel pour le pvp" : envoyé par le
/// serveur au joueur défié suite à la commande de tchat <c>/duel &lt;pseudo&gt;</c> (voir
/// <c>PlayerSession.HandleChatMessage</c>). Le client affiche une invite accepter/refuser (voir
/// <see cref="DuelResponsePacket"/>).
/// </summary>
public sealed class DuelInvitePacket : IPacket
{
    public OpCode OpCode => OpCode.DuelInvite;

    public required string FromCharacterName { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "si la personne est en team tout les membres doivent accepter" : nombre de membres (dont soi-même) qui doivent accepter côté défié — 1 pour un duel solo classique.</summary>
    public int TargetTeamSize { get; init; } = 1;

    public void Write(BinaryWriter writer)
    {
        writer.Write(FromCharacterName);
        writer.Write(TargetTeamSize);
    }

    public static IPacket Read(BinaryReader reader) => new DuelInvitePacket
    {
        FromCharacterName = reader.ReadString(),
        TargetTeamSize = reader.ReadInt32(),
    };
}
