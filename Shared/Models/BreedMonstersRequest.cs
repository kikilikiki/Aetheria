namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/breed</c> — voir GDD/demande utilisateur "un batiment pour faire de la reproduction avec heritage de statistiques". Les deux parents survivent.</summary>
public sealed class BreedMonstersRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required Guid ParentMonsterId1 { get; init; }
    public required Guid ParentMonsterId2 { get; init; }
}
