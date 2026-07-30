namespace Aetheria.Shared.Models;

/// <summary>
/// État de la reproduction en attente d'un personnage (voir GDD/demande utilisateur — "la couveuse
/// doit ajouter un temps et une validation avant de le faire") — <c>null</c> si aucune reproduction
/// n'est en cours. L'espèce du bébé est déjà déterminée au lancement (voir
/// <c>BreedingService.StartAsync</c>) pour que le délai reflète sa force réelle, la naissance
/// n'a lieu qu'à la récupération une fois le délai écoulé (voir <c>BreedingService.ClaimAsync</c>).
/// </summary>
public sealed class PendingBreedStatus
{
    public required Guid ParentMonsterId1 { get; init; }
    public required Guid ParentMonsterId2 { get; init; }
    public required string OffspringSpeciesName { get; init; }
    public required DateTime CompletesAtUtc { get; init; }
    public required bool IsReady { get; init; }
}
