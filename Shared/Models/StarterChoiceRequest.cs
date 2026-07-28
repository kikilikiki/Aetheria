namespace Aetheria.Shared.Models;

/// <summary>
/// Corps JSON de <c>POST /api/characters/{id}/starter</c> — premier compagnon offert au
/// personnage (voir <c>Docs/GameDesign.md</c>, scène d'introduction façon "choix du starter").
/// Contrairement à <c>POST /api/monsters/capture</c>, aucun jet de réussite : un choix garanti,
/// une seule fois par personnage.
/// </summary>
public sealed class StarterChoiceRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required int SpeciesId { get; init; }
}
