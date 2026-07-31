namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/reroll-nature</c> — voir GDD/demande utilisateur "Talents/capacités passives uniques par monstre (comme les 'natures' Pokémon)".</summary>
public sealed class RerollNatureRequest
{
    public required string SessionToken { get; init; }
    public required Guid MonsterId { get; init; }
    public required int ItemId { get; init; }
}
