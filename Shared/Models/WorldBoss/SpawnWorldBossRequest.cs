using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.WorldBoss;

/// <summary>Corps JSON de <c>POST /api/admin/game/spawn-world-boss</c> — voir GDD/demande utilisateur "boss geant mondial", réservé aux comptes admin/fondateur.</summary>
public sealed class SpawnWorldBossRequest
{
    public required string SessionToken { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "refonte du spawn de boss mondial par ID" : identifiant d'une espèce déjà existante du catalogue (voir Server/Persistence/MonsterCatalogSeeder) plutôt qu'un nom libre.</summary>
    public required int SpeciesId { get; init; }
    public required int MaxHealth { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "boss geant mondial [invoque] a un royaume".</summary>
    public KingdomType? TargetKingdom { get; init; }
}
