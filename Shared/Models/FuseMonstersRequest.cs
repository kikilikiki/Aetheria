namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/fuse</c> — voir GDD/demande utilisateur "un batiment pour fusionner des monstres". <see cref="SurvivorMonsterId"/> survit avec le niveau fusionné, <see cref="ConsumedMonsterId"/> est consommée.</summary>
public sealed class FuseMonstersRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required Guid SurvivorMonsterId { get; init; }
    public required Guid ConsumedMonsterId { get; init; }
}
