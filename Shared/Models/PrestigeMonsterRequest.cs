namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/{id}/prestige</c> — voir GDD/demande utilisateur "Prestige après niveau maximum".</summary>
public sealed class PrestigeMonsterRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required Guid MonsterId { get; init; }
}
