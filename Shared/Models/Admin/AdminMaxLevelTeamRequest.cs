namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/max-level-team</c> — voir GDD/demande utilisateur "une touche pour mettre niveau max toute son équipe ou celle d'un joueur".</summary>
public sealed class AdminMaxLevelTeamRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
}
