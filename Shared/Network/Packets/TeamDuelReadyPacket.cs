namespace Aetheria.Shared.Network.Packets;

/// <summary>
/// Envoyé au DÉFIEUR une fois que TOUS les membres de l'équipe ciblée ont accepté (voir GDD/
/// demande utilisateur — "si la personne est en team tout les membres doivent accepter"). Son
/// client appelle alors <c>POST /api/pvp/team-challenge</c> avec ces deux listes de personnages
/// (voir <c>ChallengeTeamDuelAsync</c>) — le serveur constitue lui-même chaque équipe de combat à
/// partir de l'équipe active (<c>IsInActiveTeam</c>) de chaque personnage, sans sélection
/// manuelle côté client (impossible à coordonner entre plusieurs joueurs humains en même temps).
/// </summary>
public sealed class TeamDuelReadyPacket : IPacket
{
    public OpCode OpCode => OpCode.TeamDuelReady;

    public required IReadOnlyList<Guid> ChallengerTeamCharacterIds { get; init; }
    public required IReadOnlyList<Guid> TargetTeamCharacterIds { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(ChallengerTeamCharacterIds.Count);
        foreach (var id in ChallengerTeamCharacterIds)
        {
            writer.Write(id.ToString());
        }

        writer.Write(TargetTeamCharacterIds.Count);
        foreach (var id in TargetTeamCharacterIds)
        {
            writer.Write(id.ToString());
        }
    }

    public static IPacket Read(BinaryReader reader)
    {
        var challengerCount = reader.ReadInt32();
        var challengerIds = new List<Guid>(challengerCount);
        for (var i = 0; i < challengerCount; i++)
        {
            challengerIds.Add(Guid.Parse(reader.ReadString()));
        }

        var targetCount = reader.ReadInt32();
        var targetIds = new List<Guid>(targetCount);
        for (var i = 0; i < targetCount; i++)
        {
            targetIds.Add(Guid.Parse(reader.ReadString()));
        }

        return new TeamDuelReadyPacket { ChallengerTeamCharacterIds = challengerIds, TargetTeamCharacterIds = targetIds };
    }
}
