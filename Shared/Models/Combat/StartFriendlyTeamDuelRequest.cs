namespace Aetheria.Shared.Models.Combat;

/// <summary>
/// Corps JSON de <c>POST /api/pvp/team-challenge</c> : démarre un duel amical entre deux groupes
/// de joueurs (voir GDD/demande utilisateur — "propose un pvp, si la personne est en team tout
/// les membres doivent accepter"). Contrairement à l'ancien défi PvP direct (une créature choisie
/// par joueur), aucune
/// liste de créatures n'est fournie par le client : chaque personnage engage son équipe active
/// (<c>EquippedSlot</c>), résolue côté serveur (voir <c>CombatService.StartFriendlyTeamDuelAsync</c>).
/// </summary>
public sealed class StartFriendlyTeamDuelRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required IReadOnlyList<Guid> ChallengerTeamCharacterIds { get; init; }
    public required IReadOnlyList<Guid> TargetTeamCharacterIds { get; init; }
}
