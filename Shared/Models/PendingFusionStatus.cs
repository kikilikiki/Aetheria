namespace Aetheria.Shared.Models;

/// <summary>
/// État de la fusion en attente d'un personnage (voir GDD/demande utilisateur — "la fusion doit
/// ajouter un temps et une validation avant de le faire") — <c>null</c> si aucune fusion n'est en
/// cours. Le survivant/la créature consommée sont déjà déterminés au lancement (voir
/// <c>FusionService.StartAsync</c>), la fusion réelle n'a lieu qu'à la récupération une fois le
/// délai écoulé (voir <c>FusionService.ClaimAsync</c>).
/// </summary>
public sealed class PendingFusionStatus
{
    public required Guid SurvivorMonsterId { get; init; }
    public required Guid ConsumedMonsterId { get; init; }
    public required string SurvivorName { get; init; }
    public required string ConsumedName { get; init; }
    public required int ResultingLevel { get; init; }
    public required DateTime CompletesAtUtc { get; init; }
    public required bool IsReady { get; init; }
}
