namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/{id}/set-active-team</c> — voir GDD/demande utilisateur, bâtiment pour "déplacer ce que l'on a dans notre team".</summary>
public sealed class SetMonsterActiveTeamRequest
{
    public required string SessionToken { get; init; }
    public required Guid MonsterId { get; init; }
    public required bool IsInActiveTeam { get; init; }
}
