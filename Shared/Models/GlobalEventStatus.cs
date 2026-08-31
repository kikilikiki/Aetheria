namespace Aetheria.Shared.Models;

/// <summary>
/// Voir GDD/demande utilisateur — "indicateurs visuels quand double XP/loot sont actifs" : état
/// public des minuteurs globaux (voir Server/World/GlobalEventService), interrogé par le Client
/// pour afficher un badge tant qu'actif. Étendu (demande utilisateur) avec le multiplicateur d'XP
/// mondial escaladant et le boost variantes/capture.
/// </summary>
public sealed record GlobalEventStatus(
    bool IsDoubleXpActive,
    DateTime? DoubleXpUntilUtc,
    bool IsDoubleLootActive,
    DateTime? DoubleLootUntilUtc,
    double GlobalXpMultiplier = 1.0,
    DateTime? GlobalXpUntilUtc = null,
    double RareBoostMultiplier = 1.0,
    DateTime? RareBoostUntilUtc = null);
