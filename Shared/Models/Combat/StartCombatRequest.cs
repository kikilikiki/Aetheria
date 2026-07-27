namespace Aetheria.Shared.Models.Combat;

/// <summary>
/// Corps JSON de <c>POST /api/combat/start</c> : le joueur engage un monstre sauvage d'une
/// espèce donnée avec son personnage et jusqu'à 4 créatures (voir GDD — Combats, mode Solo).
/// </summary>
public sealed class StartCombatRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required IReadOnlyList<Guid> MonsterIds { get; init; }
    public required int WildSpeciesId { get; init; }
}
