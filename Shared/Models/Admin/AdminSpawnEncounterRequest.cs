using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Admin;

/// <summary>
/// Corps JSON de <c>POST /api/admin/game/spawn-encounter</c> — voir demande utilisateur : "faire
/// apparaître à combattre" un monstre d'une espèce/variante/niveau choisis. Le combat démarre
/// immédiatement contre le personnage connecté de l'admin.
/// </summary>
public sealed class AdminSpawnEncounterRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required IReadOnlyList<Guid> MonsterIds { get; init; }
    public required int SpeciesId { get; init; }
    public MonsterVariant Variant { get; init; } = MonsterVariant.Normal;
    public int Level { get; init; } = 1;
}
