namespace Aetheria.Shared.Models.Combat;

/// <summary>Corps JSON de <c>POST /api/kingdoms/wars/queue</c> (voir GDD/demande utilisateur — bâtiment "Guerre", UI "prêt"). Pas de liste de créatures : le personnage engage son équipe active (voir <c>CombatService.StartFriendlyTeamDuelAsync</c>).</summary>
public sealed class QueueForWarRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
